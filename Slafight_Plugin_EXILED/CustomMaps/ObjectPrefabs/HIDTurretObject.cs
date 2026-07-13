using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using InventorySystem.Items.MicroHID.Modules;
using MEC;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.Patches;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class HIDTurretObject : ObjectPrefab
{
    private static readonly HashSet<int> TurretNpcIds = [];
    private static readonly Dictionary<int, RoleTypeId> PendingTurretRoleChanges = new();
    private static bool _eventsRegistered;
    private static CoroutineHandle _powerTimeoutHandle;
    private static float _powerEnabledUntil;
    private static float _powerRestartReadyAt;

    private const float UpdateInterval = 1f / 30f;
    private const float TargetRetentionMargin = 0.25f;
    private const float HidPrimaryRange = 6f;
    private const float MinimumNpcSpacing = 0.1f;
    private const float MaximumSafeNpcSpacing = HidPrimaryRange - 0.5f;
    private const float CoverageMargin = 0.25f;
    private const float NpcCountRetentionMargin = 0.5f;
    private const float IdleAimDistance = 25f;
    private const float ReserveNpcDepth = 100f;
    private const float PendingRoleChangeTimeout = Npc.SpawnSetRoleDelay + 1f;

    /// <summary>
    /// Turret中心からターゲットを捕捉する最大距離。
    /// </summary>
    [Header("Turret Settings")]
    public float TotalRange { get; set; } = 30f;

    /// <summary>
    /// 中心NPCのTurret基準ローカル座標。NPCの足元位置として扱う。
    /// </summary>
    public Vector3 CenterNpcLocalOffset { get; set; } = new(0f, 1.5f, 0.5f);

    /// <summary>
    /// NPC間隔の上限。実際の間隔は対象との距離と必要NPC数から動的に決まる。
    /// HIDの実射程に隙間ができない範囲へ実行時に補正される。
    /// </summary>
    public float NpcOffsetDistance { get; set; } = MaximumSafeNpcSpacing;

    /// <summary>
    /// 生成時に確保してラウンド中使い回すNPC数。
    /// </summary>
    public int NpcPoolSize { get; set; } = 8;

    public override bool FollowMarkerTransform => false;

    private SchematicObject? _schematicObject;
    private CoroutineHandle _updateHandle;
    private readonly List<TurretNpcState> _npcs = [];
    private Player? _currentTarget;
    private int _activeNpcCount = 1;

    public static bool IsPowerEnabled { get; private set; }
    public static float PowerRemainingSeconds =>
        IsPowerEnabled ? Mathf.Max(0f, _powerEnabledUntil - Time.time) : 0f;
    public static float PowerRestartCooldownRemaining =>
        IsPowerEnabled ? 0f : Mathf.Max(0f, _powerRestartReadyAt - Time.time);
    public static int InstanceCount => InstanceManager.GetAll().OfType<HIDTurretObject>().Count();

    public static void RegisterEvents()
    {
        if (_eventsRegistered)
            return;

        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
        Exiled.Events.Handlers.Scp096.AddingTarget += OnScp096AddingTarget;
        _eventsRegistered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_eventsRegistered)
            return;

        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
        Exiled.Events.Handlers.Scp096.AddingTarget -= OnScp096AddingTarget;
        ResetPowerState();
        TurretNpcIds.Clear();
        PendingTurretRoleChanges.Clear();
        _eventsRegistered = false;
    }

    public static bool EnablePower(float durationSeconds)
    {
        if (InstanceCount <= 0 || PowerRestartCooldownRemaining > 0f)
            return false;

        if (_powerTimeoutHandle.IsRunning)
            Timing.KillCoroutines(_powerTimeoutHandle);

        float duration = Mathf.Max(1f, durationSeconds);
        IsPowerEnabled = true;
        _powerEnabledUntil = Time.time + duration;
        _powerTimeoutHandle = Timing.CallDelayed(duration, () => DisablePower());
        return true;
    }

    public static void DisablePower(float restartCooldownSeconds = 60f)
    {
        bool wasEnabled = IsPowerEnabled;

        if (_powerTimeoutHandle.IsRunning)
            Timing.KillCoroutines(_powerTimeoutHandle);

        _powerTimeoutHandle = default;
        _powerEnabledUntil = 0f;
        IsPowerEnabled = false;

        if (wasEnabled && restartCooldownSeconds > 0f)
            _powerRestartReadyAt = Time.time + restartCooldownSeconds;

        foreach (HIDTurretObject turret in InstanceManager.GetAll().OfType<HIDTurretObject>().ToList())
            turret.EnterStandby();
    }

    public static void ResetPowerState()
    {
        DisablePower(0f);
        _powerRestartReadyAt = 0f;
    }

    protected override void OnCreate()
    {
        _schematicObject = SpawnManagedSchematic("HIDTurretSchem");
        if (_schematicObject == null)
        {
            Log.Error("[HIDTurretObject] Failed to spawn schematic 'HIDTurretSchem'.");
            Destroy();
            return;
        }

        SpawnNpcPool();
        if (_npcs.Count == 0)
        {
            Log.Error("[HIDTurretObject] Failed to create the turret NPC pool.");
            Destroy();
            return;
        }

        ScheduleDelayed(Npc.SpawnSetRoleDelay + 0.1f, StartUpdating);
        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        if (_updateHandle.IsRunning)
            Timing.KillCoroutines(_updateHandle);

        foreach (TurretNpcState state in _npcs)
        {
            SetNpcFiring(state, false);
            int npcId = state.Npc.Id;
            state.Npc.Destroy();
            PendingTurretRoleChanges.Remove(npcId);
            Timing.CallDelayed(NpcEffectCleanupState.DestroyDelay + 0.1f, () =>
            {
                TurretNpcIds.Remove(npcId);
                InternalNpcRegistry.Unregister(npcId);
            });
        }

        _npcs.Clear();
        _currentTarget = null;
        _schematicObject = null;

        base.OnDestroy();
    }

    private void StartUpdating()
    {
        bool anyInitialized = false;
        foreach (TurretNpcState state in _npcs)
        {
            if (state.IsInitialized || TryInitializeNpc(state))
                anyInitialized = true;
        }

        if (!anyInitialized)
        {
            Log.Error("[HIDTurretObject] Failed to initialize turret NPCs.");
            Destroy();
            return;
        }

        AimAtIdleDirection();
        _updateHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private static bool TryInitializeNpc(TurretNpcState state)
    {
        Npc npc = state.Npc;
        if (npc?.ReferenceHub == null)
            return false;

        npc.HideNpcFromClientPlayerList($"HIDTurret:{state.Index}:post-spawn");
        npc.IsNoclipPermitted = true;
        npc.IsNoclipEnabled = true;
        npc.IsGodModeEnabled = true;
        npc.IsSpectatable = false;
        npc.EnableEffect(EffectType.Fade, 255);
        npc.InfoArea = 0;

        npc.ClearInventory();
        npc.CurrentItem = Item.Create(ItemType.MicroHID);
        if (npc.CurrentItem is not MicroHid microHid)
            return false;

        microHid.Energy = 1f;
        microHid.IsBroken = false;
        microHid.LastReceived = InputSyncModule.SyncData.None;
        state.IsInitialized = true;
        return true;
    }

    private IEnumerator<float> UpdateCoroutine()
    {
        while (_schematicObject != null && _npcs.Count > 0)
        {
            if (!IsPowerEnabled)
            {
                EnterStandby();
                yield return Timing.WaitForSeconds(UpdateInterval);
                continue;
            }

            _currentTarget = SelectTarget(_currentTarget);
            if (_currentTarget == null)
            {
                SetActiveNpcCount(1);
                StopFiring();
                AimAtIdleDirection();
                yield return Timing.WaitForSeconds(UpdateInterval);
                continue;
            }

            Vector3 targetPoint = GetTargetPoint(_currentTarget);
            RotateTurretTowards(targetPoint);
            float targetDistance = Vector3.Distance(GetCenterNpcPosition(), targetPoint);
            SetActiveNpcCount(GetRequiredNpcCount(targetDistance, _activeNpcCount));
            AlignNpcsOnBeam(targetPoint, targetDistance);

            for (int i = 0; i < _npcs.Count; i++)
            {
                TurretNpcState state = _npcs[i];
                SetNpcFiring(state, i < _activeNpcCount && state.IsInitialized);
                RechargeNpc(state.Npc);
            }

            yield return Timing.WaitForSeconds(UpdateInterval);
        }
    }

    private Player? SelectTarget(Player? currentTarget)
    {
        float configuredRange = Mathf.Max(0f, TotalRange);
        if (IsValidTarget(currentTarget, configuredRange + TargetRetentionMargin))
            return currentTarget;

        Player? nearestTarget = null;
        float nearestSqrDistance = configuredRange * configuredRange;

        foreach (Player player in Player.List)
        {
            if (!IsTargetCandidate(player))
                continue;

            float sqrDistance = (player.Position - Position).sqrMagnitude;
            if (sqrDistance > nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearestTarget = player;
        }

        return nearestTarget;
    }

    private bool IsValidTarget(Player? player, float range)
        => IsTargetCandidate(player) &&
           (player!.Position - Position).sqrMagnitude <= range * range;

    private static bool IsTargetCandidate(Player? player)
        => player != null &&
           player is not Npc &&
           player.IsAlive &&
           player.GetTeam() == CTeam.SCPs;

    private void RotateTurretTowards(Vector3 targetPoint)
    {
        Vector3 horizontalDirection = targetPoint - Position;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude <= 0.0001f)
            return;

        Rotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
    }

    private void AlignNpcsOnBeam(Vector3 targetPoint, float targetDistance)
    {
        if (_npcs.Count == 0)
            return;

        Vector3 centerPosition = GetCenterNpcPosition();
        Vector3 beamDirection = targetPoint - centerPosition;
        if (beamDirection.sqrMagnitude <= 0.0001f)
            beamDirection = Rotation * Vector3.forward;
        else
            beamDirection.Normalize();

        float spacing = GetDynamicNpcSpacing(targetDistance, _activeNpcCount);
        for (int i = 0; i < _activeNpcCount; i++)
        {
            Npc npc = _npcs[i].Npc;
            npc.Position = centerPosition + beamDirection * (spacing * i);
            AimNpc(npc, targetPoint);
        }

        ParkReserveNpcs(centerPosition);
    }

    private int GetRequiredNpcCount(float targetDistance, int currentCount)
    {
        float maxSpacing = GetMaximumNpcSpacing();
        float uncoveredDistance = Mathf.Max(0f, targetDistance - (HidPrimaryRange - CoverageMargin));
        int requiredCount = 1 + Mathf.CeilToInt(uncoveredDistance / maxSpacing);

        if (requiredCount < currentCount)
        {
            float previousCountCapacity =
                HidPrimaryRange - CoverageMargin + Mathf.Max(0, currentCount - 2) * maxSpacing;
            if (targetDistance > previousCountCapacity - NpcCountRetentionMargin)
                return currentCount;
        }

        return Mathf.Clamp(requiredCount, 1, _npcs.Count);
    }

    private float GetDynamicNpcSpacing(float targetDistance, int npcCount)
    {
        if (npcCount <= 1)
            return 0f;

        float requiredReach = Mathf.Max(0f, targetDistance - (HidPrimaryRange - CoverageMargin));
        return Mathf.Clamp(requiredReach / (npcCount - 1), MinimumNpcSpacing, GetMaximumNpcSpacing());
    }

    private void AimAtIdleDirection()
    {
        if (_npcs.Count == 0)
            return;

        Vector3 centerPosition = GetCenterNpcPosition();
        Vector3 forward = Rotation * Vector3.forward;
        Vector3 targetPoint = centerPosition + Vector3.up * 1.6f + forward * IdleAimDistance;

        _npcs[0].Npc.Position = centerPosition;
        AimNpc(_npcs[0].Npc, targetPoint);
        ParkReserveNpcs(centerPosition);
    }

    private Vector3 GetCenterNpcPosition()
        => Position + Rotation * CenterNpcLocalOffset;

    private static Vector3 GetTargetPoint(Player target)
        => target.CameraTransform != null
            ? target.CameraTransform.position
            : target.Position + Vector3.up;

    private static void AimNpc(Npc npc, Vector3 targetPoint)
    {
        if (npc.ReferenceHub.roleManager.CurrentRole is not IFpcRole fpcRole)
            return;

        Vector3 direction = targetPoint - npc.CameraTransform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Vector3 euler = rotation.eulerAngles;
        float horizontal = euler.y;
        float vertical = -Mathf.DeltaAngle(0f, euler.x);
        FpcMouseLook mouseLook = fpcRole.FpcModule.MouseLook;

        // Dedicated mode is not treated as a dummy by FpcMouseLook.UpdateRotation,
        // so update both current and received sync angles before applying rotation.
        mouseLook.CurrentHorizontal = horizontal;
        mouseLook.CurrentVertical = vertical;
        mouseLook._syncHorizontal = horizontal;
        mouseLook._syncVertical = vertical;
        mouseLook.UpdateRotation();
    }

    private void StopFiring()
    {
        foreach (TurretNpcState state in _npcs)
            SetNpcFiring(state, false);
    }

    private void EnterStandby()
    {
        _currentTarget = null;
        SetActiveNpcCount(1);
        StopFiring();
        AimAtIdleDirection();
    }

    private static void SetNpcFiring(TurretNpcState state, bool shouldFire)
    {
        if (state.IsFiring == shouldFire)
            return;

        if (state.Npc.CurrentItem is not MicroHid microHid)
        {
            state.IsFiring = false;
            return;
        }

        microHid.LastReceived = shouldFire
            ? InputSyncModule.SyncData.Primary
            : InputSyncModule.SyncData.None;
        state.IsFiring = shouldFire;
    }

    private static void RechargeNpc(Npc? npc)
    {
        if (npc?.CurrentItem is not MicroHid microHid)
            return;

        if (microHid.IsBroken)
            microHid.IsBroken = false;

        microHid.Energy = 1f;
    }

    private float GetMaximumNpcSpacing()
        => Mathf.Clamp(NpcOffsetDistance, MinimumNpcSpacing, MaximumSafeNpcSpacing);

    private void SetActiveNpcCount(int requiredCount)
    {
        int newCount = Mathf.Clamp(requiredCount, 1, _npcs.Count);
        if (newCount < _activeNpcCount)
        {
            for (int i = newCount; i < _activeNpcCount; i++)
                SetNpcFiring(_npcs[i], false);
        }

        _activeNpcCount = newCount;
    }

    private void SpawnNpcPool()
    {
        int poolSize = Mathf.Max(1, NpcPoolSize);
        for (int index = 0; index < poolSize; index++)
        {
            Npc? npc = Npc.Spawn($"H.I.D Turret", RoleTypeId.Tutorial, true, GetCenterNpcPosition());
            if (npc == null)
            {
                Log.Error($"[HIDTurretObject] Failed to spawn turret NPC {index}.");
                continue;
            }

            var state = new TurretNpcState(npc, index);
            int capturedIndex = index;
            _npcs.Add(state);
            TurretNpcIds.Add(npc.Id);
            InternalNpcRegistry.Register(npc, InternalNpcCategory.HidTurret);
            AllowNextTurretRoleChange(npc.Id, RoleTypeId.Tutorial);
            npc.HideNpcFromClientPlayerList($"HIDTurret:{index}:spawn");
            ScheduleDelayed(Npc.SpawnSetRoleDelay + 0.1f, () =>
            {
                if (_npcs.Contains(state) && !state.IsInitialized && !TryInitializeNpc(state))
                    Log.Error($"[HIDTurretObject] Failed to initialize turret NPC {capturedIndex}.");
            });
        }
    }

    private static void OnScp096AddingTarget(AddingTargetEventArgs ev)
    {
        if (ev?.Target == null)
            return;

        if (TurretNpcIds.Contains(ev.Target.Id))
            ev.IsAllowed = false;
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        if (!TurretNpcIds.Contains(ev.Player.Id))
            return;

        if (TryConsumeTurretRoleChange(ev.Player.Id, ev.NewRole))
            return;

        ev.IsAllowed = false;
    }

    private static void AllowNextTurretRoleChange(int npcId, RoleTypeId role)
    {
        PendingTurretRoleChanges[npcId] = role;

        Timing.CallDelayed(PendingRoleChangeTimeout, () =>
        {
            if (PendingTurretRoleChanges.TryGetValue(npcId, out RoleTypeId pendingRole) && pendingRole == role)
                PendingTurretRoleChanges.Remove(npcId);
        });
    }

    private static bool TryConsumeTurretRoleChange(int npcId, RoleTypeId requestedRole)
    {
        if (!PendingTurretRoleChanges.TryGetValue(npcId, out RoleTypeId pendingRole))
            return false;

        if (pendingRole != requestedRole)
            return false;

        PendingTurretRoleChanges.Remove(npcId);
        return true;
    }

    private void ParkReserveNpcs(Vector3 centerPosition)
    {
        Vector3 reservePosition = centerPosition + Vector3.down * ReserveNpcDepth;
        for (int i = _activeNpcCount; i < _npcs.Count; i++)
        {
            TurretNpcState state = _npcs[i];
            SetNpcFiring(state, false);
            state.Npc.Position = reservePosition + Vector3.down * (i * MinimumNpcSpacing);
        }
    }

    private sealed class TurretNpcState
    {
        public TurretNpcState(Npc npc, int index)
        {
            Npc = npc;
            Index = index;
        }

        public Npc Npc { get; }
        public int Index { get; }
        public bool IsInitialized { get; set; }
        public bool IsFiring { get; set; }
    }
}
