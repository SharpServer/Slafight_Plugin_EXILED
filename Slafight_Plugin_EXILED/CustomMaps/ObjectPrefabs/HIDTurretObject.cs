using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.Handlers;
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
using Item = Exiled.API.Features.Items.Item;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class HIDTurretObject : ObjectPrefab
{
    private static readonly HashSet<int> TurretNpcIds = [];
    private static bool _eventsRegistered;
    private static CoroutineHandle _autoActivationHandle;

    private const float UpdateInterval = 1f / 30f;

    /// <summary>
    /// 自動起動条件を判定する間隔（秒）。
    /// </summary>
    private const float AutoActivationCheckInterval = 2f;

    /// <summary>
    /// 自動起動に必要な、生存者に占めるSCPチームの比率。SCPチーム対その他が 7:3 の状態を指す。
    /// </summary>
    private const float ScpDominanceRatio = 0.7f;

    private const float TargetRetentionMargin = 0.25f;
    private const float HidPrimaryRange = 6f;
    private const float MinimumNpcSpacing = 0.1f;
    private const float MaximumSafeNpcSpacing = HidPrimaryRange - 0.5f;
    private const float CoverageMargin = 0.25f;
    private const float NpcCountRetentionMargin = 0.5f;
    private const float IdleAimDistance = 25f;
    private const float ReserveNpcDepth = 100f;

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

    /// <summary>
    /// NPCプールを生成して稼働中かどうか。電源投入で true、電源断・破棄で false になる。
    /// </summary>
    private bool _isOperating;

    /// <summary>
    /// タレットが稼働状態かどうか。自動起動条件を一度でも満たすとラウンド終了まで解除されない。
    /// </summary>
    public static bool IsPowerEnabled { get; private set; }

    public static int InstanceCount => GetInstances().Count;

    private static List<HIDTurretObject> GetInstances()
        => ObjectPrefabInstances.GetAll().OfType<HIDTurretObject>().ToList();

    public static void RegisterEvents()
    {
        if (_eventsRegistered)
            return;

        Scp096.AddingTarget += OnScp096AddingTarget;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ResetPowerState;
        Exiled.Events.Handlers.Server.RoundStarted += ResetPowerState;
        _autoActivationHandle = Timing.RunCoroutine(AutoActivationCoroutine());
        _eventsRegistered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_eventsRegistered)
            return;

        Scp096.AddingTarget -= OnScp096AddingTarget;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetPowerState;
        Exiled.Events.Handlers.Server.RoundStarted -= ResetPowerState;

        if (_autoActivationHandle.IsRunning)
            Timing.KillCoroutines(_autoActivationHandle);

        _autoActivationHandle = default;
        ResetPowerState();
        TurretNpcIds.Clear();
        _eventsRegistered = false;
    }

    /// <summary>
    /// 電源を落とし、生成済みのNPCプールを解放する。ラウンドリセット時に呼ばれる。
    /// </summary>
    public static void ResetPowerState()
    {
        IsPowerEnabled = false;

        foreach (HIDTurretObject turret in GetInstances())
            turret.EndOperation();
    }

    /// <summary>
    /// 自動起動条件を定期的に判定する。一度起動したらラウンドがリセットされるまで停止しない。
    /// </summary>
    private static IEnumerator<float> AutoActivationCoroutine()
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(AutoActivationCheckInterval);

            if (IsPowerEnabled || !Round.IsStarted || Round.IsEnded)
                continue;

            if (!ShouldAutoActivate())
                continue;

            EnablePowerPermanently();
        }
    }

    /// <summary>
    /// 自動起動条件。
    /// 「SCPチーム対その他が 7:3 以上」かつ「下層(軽度収容区画)が除染済み」、
    /// もしくは「Alpha Warhead 爆発済み」。
    /// </summary>
    private static bool ShouldAutoActivate()
    {
        if (Exiled.API.Features.Warhead.IsDetonated)
            return true;

        return Exiled.API.Features.Map.IsLczDecontaminated && IsScpTeamDominant();
    }

    private static bool IsScpTeamDominant()
    {
        int scpCount = 0;
        int totalCount = 0;

        foreach (Player player in Player.List)
        {
            if (player == null ||
                !player.IsAlive ||
                !player.IsSafePlayer() ||
                CRole.IsTeamNpc(player))
                continue;

            totalCount++;
            if (player.GetTeam() == CTeam.SCPs)
                scpCount++;
        }

        return totalCount > 0 && scpCount >= totalCount * ScpDominanceRatio;
    }

    private static void EnablePowerPermanently()
    {
        List<HIDTurretObject> turrets = GetInstances();

        IsPowerEnabled = true;
        Log.Debug($"[HIDTurretObject] Auto activation latched. detonated={Exiled.API.Features.Warhead.IsDetonated} " +
                  $"lczDecontaminated={Exiled.API.Features.Map.IsLczDecontaminated} turrets={turrets.Count}");

        // 電源投入のこのタイミングで初めてNPCプールを生成する。
        foreach (HIDTurretObject turret in turrets)
            turret.BeginOperation();

        // マップにタレットが存在しない構成では、無意味なアナウンスを流さない。
        if (turrets.Count <= 0)
            return;

        Exiled.API.Features.Cassie.MessageTranslated(
            "Danger . Facility Defense System Activated . H I D Turret System is now Online . . . . .",
            "警告。<split>施設防衛システムが作動しました。<split>H.I.Dタレットシステムがオンラインになりました。");
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

        // NPCプールは電源投入時にのみ生成する。
        // 起動条件を満たさないラウンドで8体のNPCを抱え続けないため。
        // 既に起動済みのラウンド中に生成された場合は、その場で稼働を開始する。
        if (IsPowerEnabled)
            BeginOperation();

        base.OnCreate();
    }

    protected override void OnDestroy()
    {
        EndOperation();
        _schematicObject = null;

        base.OnDestroy();
    }

    /// <summary>
    /// 電源投入時に呼ばれ、NPCプールを生成して追尾・射撃ループを開始する。
    /// </summary>
    private void BeginOperation()
    {
        if (_isOperating || _schematicObject == null)
            return;

        _isOperating = true;

        SpawnNpcPool();
        if (_npcs.Count == 0)
        {
            Log.Error("[HIDTurretObject] Failed to create the turret NPC pool.");
            EndOperation();
            return;
        }

        ScheduleDelayed(Npc.SpawnSetRoleDelay + 0.1f, StartUpdating);
    }

    /// <summary>
    /// 電源断・ラウンドリセット・破棄で呼ばれ、稼働を止めてNPCプールを解放する。
    /// スキマティック自体はマップの一部なので破棄しない。
    /// </summary>
    private void EndOperation()
    {
        if (_updateHandle.IsRunning)
            Timing.KillCoroutines(_updateHandle);

        _updateHandle = default;

        foreach (TurretNpcState state in _npcs)
        {
            SetNpcFiring(state, false);
            int npcId = state.Npc.Id;
            state.Npc.Destroy();
            Timing.CallDelayed(NpcEffectCleanupState.DestroyDelay + 0.1f, () =>
            {
                TurretNpcIds.Remove(npcId);
                InternalNpcRegistry.Unregister(npcId);
            });
        }

        _npcs.Clear();
        _currentTarget = null;
        _activeNpcCount = 1;
        _isOperating = false;
    }

    private void StartUpdating()
    {
        if (!_isOperating)
            return;

        bool anyInitialized = false;
        foreach (TurretNpcState state in _npcs)
        {
            if (state.IsInitialized || TryInitializeNpc(state))
                anyInitialized = true;
        }

        if (!anyInitialized)
        {
            Log.Error("[HIDTurretObject] Failed to initialize turret NPCs.");
            EndOperation();
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
        while (_isOperating && IsPowerEnabled && _schematicObject != null && _npcs.Count > 0)
        {
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
                EnterNpcStandby(_npcs[i]);
        }
        else if (newCount > _activeNpcCount)
        {
            for (int i = _activeNpcCount; i < newCount; i++)
                ActivateNpc(_npcs[i]);
        }

        _activeNpcCount = newCount;
    }

    /// <summary>
    /// NPC間の生成間隔（秒）。Npc.Spawn はフレームコストが高いため、
    /// 同一フレームに集中させず数フレームへ分散してストールを避ける。
    /// </summary>
    private const float NpcSpawnStaggerInterval = 0.02f;

    private void SpawnNpcPool()
    {
        int poolSize = Mathf.Max(1, NpcPoolSize);

        // 先頭NPCは同期生成し、スキマティック/NPC生成自体の致命的失敗を即座に検出する。
        SpawnSingleNpc(0);

        for (int index = 1; index < poolSize; index++)
        {
            int capturedIndex = index;
            ScheduleDelayed(capturedIndex * NpcSpawnStaggerInterval, () => SpawnSingleNpc(capturedIndex));
        }
    }

    private void SpawnSingleNpc(int index)
    {
        // 分散生成の途中で電源断・破棄された場合、解放済みのプールへ後から追加しない。
        if (!_isOperating)
            return;

        Npc? npc = Npc.Spawn("H.I.D Turret", RoleTypeId.Tutorial, true, GetCenterNpcPosition());
        if (npc == null)
        {
            Log.Error($"[HIDTurretObject] Failed to spawn turret NPC {index}.");
            return;
        }

        var state = new TurretNpcState(npc, index);
        _npcs.Add(state);
        TurretNpcIds.Add(npc.Id);
        InternalNpcRegistry.Register(npc, InternalNpcCategory.HidTurret);
        npc.HideNpcFromClientPlayerList($"HIDTurret:{index}:spawn");
        ScheduleDelayed(Npc.SpawnSetRoleDelay + 0.1f, () =>
        {
            if (!_npcs.Contains(state))
                return;

            if (!state.IsInitialized && !TryInitializeNpc(state))
            {
                Log.Error($"[HIDTurretObject] Failed to initialize turret NPC {index}.");
                return;
            }

            if (index > 0)
                EnterNpcStandby(state);
        });
    }

    private static void OnScp096AddingTarget(AddingTargetEventArgs ev)
    {
        if (ev?.Target == null)
            return;

        if (TurretNpcIds.Contains(ev.Target.Id))
            ev.IsAllowed = false;
    }

    /// <summary>
    /// 予備NPCを稼働状態へ戻す。
    /// 待機中も役職は Tutorial のまま維持しているため、役職変更は行わない。
    /// </summary>
    private void ActivateNpc(TurretNpcState state)
    {
        if (!state.IsStandby)
            return;

        state.IsStandby = false;

        // 初期化済みならインベントリも MicroHID もそのまま使えるため、即座に稼働できる。
        if (!state.IsInitialized && !TryInitializeNpc(state))
            Log.Error($"[HIDTurretObject] Failed to activate turret NPC {state.Index}.");
    }

    /// <summary>
    /// 予備NPCを待機状態にする。
    /// 待機は「射撃停止 + <see cref="ParkReserveNpcs"/> によるマップ外退避」だけで表現し、
    /// 役職変更は行わない。役職を差し替えると、遅延して走るロールのセットアップ処理
    /// （<see cref="Exiled.API.Extensions.PlayerExtensions.ChangeAppearance(Player, RoleTypeId, bool, byte)"/> 等）が
    /// 復帰後のNPCへ適用され、クライアント側の見た目が壊れたまま復旧しなくなる。
    /// </summary>
    private void EnterNpcStandby(TurretNpcState state)
    {
        if (state.IsStandby)
            return;

        SetNpcFiring(state, false);
        state.IsStandby = true;
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
        public bool IsStandby { get; set; }
    }
}
