using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Warhead;
using JetBrains.Annotations;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp096Anger : CRole
{
    private const float TryNotToCryDuration = 35f;
    private const float TargetRetentionInputDuration = TryNotToCryDuration + 5f;
    private const float RageRefreshDuration = 35f;

    private const float TargetRestoreDelay = 0.05f;
    private const float RageMaintenanceInterval = 0.2f;

    private const string ChamberGuardName =
        "SCP-096 Chamber Facility Guard";

    protected override string RoleName { get; set; } = "SCP-096";

    protected override string Description { get; set; } =
        "<size=24><color=red>SCP-096: Anger</color>\n" +
        "SCP-096の怒りと悲しみが再び不安定化し、本来の力が戻ってきた！\n" +
        "<color=red>自分を見てきた相手を地の底まで追いかけろ！！！</color>";

    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override bool DescriptionShowRoleName { get; set; } = false;

    protected override CRoleTypeId CRoleTypeId { get; set; } =
        CRoleTypeId.Scp096Anger;

    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp096_Anger";

    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp096;
    protected override string SpawnCustomInfo => "SCP-096: ANGER";

    /*
     * SCP-096がスポーンした位置。
     */
    private readonly Dictionary<Player, Vector3> _shyGuyPositions = [];

    /*
     * 泣くアニメーション中かどうか。
     */
    private readonly Dictionary<Player, bool> _inTryNotToCryAnim = [];

    /*
     * ゲーム本体側のTargetsから削除されても維持する対象。
     *
     * RemovingTargetイベントのキャンセルが反映されない場合でも、
     * このリストを基準に再追加する。
     */
    private readonly Dictionary<Player, HashSet<Player>> _persistentTargets = [];

    /*
     * Playerごとの怒り維持コルーチン。
     */
    private readonly Dictionary<Player, CoroutineHandle> _maintenanceCoroutines = [];

    /*
     * 古いCallDelayedが、再スポーン後の新しい状態へ干渉するのを防ぐ。
     */
    private readonly Dictionary<Player, uint> _sessions = [];

    /*
     * AddTargetによる復元時にAddingTargetが再度発火しても、
     * 泣く演出を再開始しないためのガード。
     */
    private readonly HashSet<(int ScpId, int TargetId)> _restoringTargets = [];

    private uint _nextSessionId;

    public override void RegisterEvents()
    {
        Log.Info("[Scp096Anger] RegisterEvents");

        Exiled.Events.Handlers.Scp096.Enraging += OnEnraging;
        Exiled.Events.Handlers.Scp096.AddingTarget += OnTargetAdded;
        Exiled.Events.Handlers.Scp096.RemovingTarget += OnRemovingTarget;
        Exiled.Events.Handlers.Scp096.CalmingDown += OnCalming;
        Exiled.Events.Handlers.Warhead.Detonating += OnVanillaWarheadDetonating;
        OmegaWarhead.OmegaWarheadDetonating += OnMassCasualtyImminent;

        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Log.Info("[Scp096Anger] UnregisterEvents");

        Exiled.Events.Handlers.Scp096.Enraging -= OnEnraging;
        Exiled.Events.Handlers.Scp096.AddingTarget -= OnTargetAdded;
        Exiled.Events.Handlers.Scp096.RemovingTarget -= OnRemovingTarget;
        Exiled.Events.Handlers.Scp096.CalmingDown -= OnCalming;
        Exiled.Events.Handlers.Warhead.Detonating -= OnVanillaWarheadDetonating;
        OmegaWarhead.OmegaWarheadDetonating -= OnMassCasualtyImminent;

        foreach (Player player in _sessions.Keys.ToArray())
            CleanupPlayer(player);

        DestroyChamberGuards();

        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(
        Player player,
        RoleSpawnFlags roleSpawnFlags)
    {
        CleanupPlayer(player);

        uint session = CreateSession(player);

        _inTryNotToCryAnim[player] = false;
        _persistentTargets[player] = [];

        player.MaxArtificialHealth = 1000f;
        player.MaxHealth = 8000f;
        player.Health = 8000f;

        player.MaxHumeShield = 150f;
        player.HumeShieldRegenerationMultiplier = 0.35f;

        ChangeSpeedState(player, false);

        player.Transform.eulerAngles = new Vector3(0f, -90f, 0f);
        _shyGuyPositions[player] = player.Position;

        _maintenanceCoroutines[player] =
            Timing.RunCoroutine(MaintainAnger(player, session));

        Log.Debug("[Scp096Anger] SCP-096: Anger was spawned.");

        Timing.CallDelayed(
            RoleSpawnTimings.FastSpawnFinalize,
            () =>
            {
                if (!IsCurrentSession(player, session))
                    return;

                StartAnger(player);
            });
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        DestroyChamberGuards();

        CassieHelper.AnnounceTermination(
            ev,
            "SCP 0 9 6",
            $"<color={Team.GetTeamColor()}>{RoleName}</color>",
            true);

        base.OnRoleDying(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        DestroyChamberGuards();

        base.OnRoleChanging(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        DestroyChamberGuards();

        base.OnRoleLeft(ev);
    }

    private uint CreateSession(Player player)
    {
        uint session = ++_nextSessionId;

        if (session == 0)
            session = ++_nextSessionId;

        _sessions[player] = session;
        return session;
    }

    private bool IsCurrentSession(Player player, uint session)
    {
        return player is not null &&
               player.IsConnected &&
               session != 0 &&
               _sessions.TryGetValue(player, out uint currentSession) &&
               currentSession == session &&
               Check(player);
    }

    private void StartAnger(Player player)
    {
        if (!Check(player) ||
            !_shyGuyPositions.TryGetValue(player, out Vector3 shyGuyPosition))
        {
            return;
        }

        foreach (Door door in Door.List)
        {
            if (door.Type != DoorType.HeavyContainmentDoor)
                continue;

            if (door.Room is null || door.Room.Type != RoomType.Hcz096)
                continue;

            door.Lock(DoorLockType.AdminCommand);
        }

        Vector3 spawnPoint = new(
            shyGuyPosition.x + 1f,
            shyGuyPosition.y,
            shyGuyPosition.z);

        Npc guard = Npc.Spawn(
            ChamberGuardName,
            RoleTypeId.FacilityGuard,
            false,
            position: spawnPoint);

        if (guard is null)
        {
            Log.Warn("[Scp096Anger] Failed to spawn chamber guard.");
            return;
        }

        guard.Transform.localEulerAngles = new Vector3(0f, -90f, 0f);
    }

    private void OnEnraging(EnragingEventArgs ev)
    {
        if (!Check(ev.Player))
            return;

        if (IsInTryNotToCryAnimation(ev.Player))
        {
            ev.IsAllowed = false;
            return;
        }

        DestroyChamberGuards();
    }

    private void OnTargetAdded(AddingTargetEventArgs ev)
    {
        if (!ev.IsAllowed ||
            !Check(ev.Player) ||
            !ShouldPersistTarget(ev.Target))
        {
            return;
        }

        Player player = ev.Player;
        Player target = ev.Target;

        AddPersistentTarget(player, target);

        /*
         * 復元目的で呼ばれたAddTargetなので、
         * 泣く演出をもう一度開始しない。
         */
        if (_restoringTargets.Contains((player.Id, target.Id)))
            return;

        /*
         * 既に怒っている場合は、新しいターゲットを記録するだけ。
         */
        if (ev.Scp096.RageManager.IsEnraged)
        {
            RefreshRageTimer(ev.Scp096);
            return;
        }

        /*
         * 最初の対象に対する泣く演出が既に進行中。
         * 対象だけ追加し、演出は重複させない。
         */
        if (IsInTryNotToCryAnimation(player))
            return;

        if (!_sessions.TryGetValue(player, out uint session))
            return;

        Log.Debug(
            $"[Scp096Anger] Initial target added: " +
            $"{target.Nickname} ({target.Id})");

        _inTryNotToCryAnim[player] = true;

        player.EnableEffect(EffectType.Slowness, 95);
        player.EnableEffect(EffectType.DamageReduction, 90);

        SpeakerApi.Play(
            "096Angered.ogg",
            "Scp096",
            player.Position,
            true,
            player.Transform,
            false,
            80f,
            0f);

        Timing.CallDelayed(
            0.1f,
            () =>
            {
                if (!IsCurrentSession(player, session) ||
                    !IsInTryNotToCryAnimation(player))
                {
                    return;
                }

                if (player.Role is not Scp096Role role)
                    return;

                role.ShowRageInput(TargetRetentionInputDuration);
            });

        Timing.CallDelayed(
            TryNotToCryDuration,
            () =>
            {
                if (!IsCurrentSession(player, session))
                    return;

                _inTryNotToCryAnim[player] = false;

                player.DisableEffect(EffectType.DamageReduction);

                PrunePersistentTargets(player);

                if (!HasPersistentTarget(player))
                {
                    ChangeSpeedState(player, false);

                    if (player.Role is Scp096Role emptyRole &&
                        emptyRole.RageManager.IsEnraged)
                    {
                        emptyRole.Calm();
                    }

                    return;
                }

                if (player.Role is not Scp096Role role)
                    return;

                RestoreMissingTargets(player, role);
                ChangeSpeedState(player, true);
                RefreshRageTimer(role);
            });
    }

    private void OnRemovingTarget(RemovingTargetEventArgs ev)
    {
        if (!Check(ev.Player) || ev.Target is null)
            return;

        Player player = ev.Player;
        Player target = ev.Target;

        Log.Debug(
            $"[Scp096Anger] RemovingTarget triggered: " +
            $"SCP={player.Nickname} ({player.Id}), " +
            $"Target={target.Nickname} ({target.Id}), " +
            $"AllowedBefore={ev.IsAllowed}, " +
            $"Connected={target.IsConnected}, " +
            $"Alive={target.IsAlive}, " +
            $"Team={target.GetTeam()}");

        /*
         * 死亡、切断、SCP化などの場合は通常どおり削除させる。
         */
        if (!ShouldPersistTarget(target))
        {
            RemovePersistentTarget(player, target);
            return;
        }

        /*
         * 本体側の削除処理を一応キャンセルする。
         *
         * バージョンや削除経路によってキャンセルが無視されても、
         * CallDelayedと維持コルーチンで再追加される。
         */
        ev.IsAllowed = false;

        AddPersistentTarget(player, target);

        if (!_sessions.TryGetValue(player, out uint session))
            return;

        Timing.CallDelayed(
            TargetRestoreDelay,
            () =>
            {
                if (!IsCurrentSession(player, session) ||
                    !ShouldPersistTarget(target))
                {
                    return;
                }

                if (player.Role is not Scp096Role role)
                    return;

                RestoreTarget(player, role, target);

                if (!IsInTryNotToCryAnimation(player))
                {
                    ChangeSpeedState(player, true);
                    RefreshRageTimer(role);
                }
            });
    }

    private void OnCalming(CalmingDownEventArgs ev)
    {
        if (!Check(ev.Player))
            return;

        Player player = ev.Player;

        PrunePersistentTargets(player);

        if (!HasPersistentTarget(player))
        {
            ChangeSpeedState(player, false);
            return;
        }

        /*
         * 生存中の敵ターゲットが残っている限り鎮静を拒否する。
         */
        ev.IsAllowed = false;
        ev.ShouldClearEnragedTimeLeft = false;

        if (!_sessions.TryGetValue(player, out uint session))
            return;

        Timing.CallDelayed(
            TargetRestoreDelay,
            () =>
            {
                if (!IsCurrentSession(player, session))
                    return;

                PrunePersistentTargets(player);

                if (!HasPersistentTarget(player))
                {
                    ChangeSpeedState(player, false);
                    return;
                }

                if (player.Role is not Scp096Role role)
                    return;

                RestoreMissingTargets(player, role);

                if (!IsInTryNotToCryAnimation(player))
                {
                    ChangeSpeedState(player, true);
                    RefreshRageTimer(role);
                }
            });
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (!Check(ev.Attacker))
            return;

        Player attacker = ev.Attacker;

        ev.Amount = 999999f;
        attacker.ArtificialHealth += 25f;

        if (!_sessions.TryGetValue(attacker, out uint session))
            return;

        Timing.CallDelayed(
            1f,
            () =>
            {
                if (!IsCurrentSession(attacker, session))
                    return;

                PrunePersistentTargets(attacker);

                if (HasPersistentTarget(attacker))
                    return;

                ChangeSpeedState(attacker, false);

                if (attacker.Role is Scp096Role role &&
                    role.RageManager.IsEnraged)
                {
                    role.Calm();
                }
            });
    }

    private IEnumerator<float> MaintainAnger(Player player, uint session)
    {
        while (IsCurrentSession(player, session))
        {
            if (player.Role is not Scp096Role role)
                yield break;

            PrunePersistentTargets(player);

            bool hasTarget = HasPersistentTarget(player);

            if (hasTarget)
            {
                /*
                 * ターゲット削除のキャンセルが無視された場合でも、
                 * 本体側のTargetsへ再追加する。
                 */
                RestoreMissingTargets(player, role);

                /*
                 * 泣く演出中にEnrageすると演出を飛ばしてしまうため、
                 * ターゲット復元だけ行い、怒り状態の更新はしない。
                 */
                if (!IsInTryNotToCryAnimation(player))
                {
                    ChangeSpeedState(player, true);
                    RefreshRageTimer(role);
                }
            }
            else if (!IsInTryNotToCryAnimation(player))
            {
                ChangeSpeedState(player, false);

                if (role.RageManager.IsEnraged)
                    role.Calm();
            }

            yield return Timing.WaitForSeconds(RageMaintenanceInterval);
        }
    }

    private void RestoreMissingTargets(Player player, Scp096Role role)
    {
        if (!_persistentTargets.TryGetValue(
                player,
                out HashSet<Player> targets))
        {
            return;
        }

        foreach (Player target in targets.ToArray())
        {
            if (!ShouldPersistTarget(target))
            {
                targets.Remove(target);
                continue;
            }

            RestoreTarget(player, role, target);
        }
    }

    private void RestoreTarget(
        Player player,
        Scp096Role role,
        Player target)
    {
        if (!ShouldPersistTarget(target))
            return;

        if (role.Targets.Contains(target))
            return;

        var key = (player.Id, target.Id);

        /*
         * AddTargetによってAddingTargetイベントが発火するので、
         * 復元中であることを記録する。
         */
        if (!_restoringTargets.Add(key))
            return;

        try
        {
            role.AddTarget(target);

            Log.Debug(
                $"[Scp096Anger] Restored target: " +
                $"{target.Nickname} ({target.Id})");
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[Scp096Anger] Failed to restore target " +
                $"{target.Nickname} ({target.Id}):\n{exception}");
        }
        finally
        {
            _restoringTargets.Remove(key);
        }
    }

    private void AddPersistentTarget(Player player, Player target)
    {
        if (player is null || target is null)
            return;

        if (!_persistentTargets.TryGetValue(
                player,
                out HashSet<Player> targets))
        {
            targets = [];
            _persistentTargets[player] = targets;
        }

        targets.Add(target);
    }

    private void RemovePersistentTarget(Player player, Player target)
    {
        if (player is null || target is null)
            return;

        if (_persistentTargets.TryGetValue(
                player,
                out HashSet<Player> targets))
        {
            targets.Remove(target);
        }

        _restoringTargets.Remove((player.Id, target.Id));
    }

    private void PrunePersistentTargets(Player player)
    {
        if (player is null ||
            !_persistentTargets.TryGetValue(
                player,
                out HashSet<Player> targets))
        {
            return;
        }

        targets.RemoveWhere(target => !ShouldPersistTarget(target));
    }

    private bool HasPersistentTarget(Player player)
    {
        if (player is null ||
            !_persistentTargets.TryGetValue(
                player,
                out HashSet<Player> targets))
        {
            return false;
        }

        foreach (Player target in targets)
        {
            if (ShouldPersistTarget(target))
                return true;
        }

        return false;
    }

    private bool IsInTryNotToCryAnimation(Player player)
    {
        return player is not null &&
               _inTryNotToCryAnim.TryGetValue(player, out bool value) &&
               value;
    }

    private static bool ShouldPersistTarget(Player target)
    {
        return target is
               {
                   IsConnected: true,
                   IsAlive: true
               } &&
               target.GetTeam() is not CTeam.SCPs;
    }

    private static void RefreshRageTimer(Scp096Role role)
    {
        if (role is null)
            return;

        /*
         * RemovingTargetまたはCalmingDownの段階で既にIsEnragedが
         * falseになっている場合もあるため、早期returnしない。
         */
        if (!role.RageManager.IsEnraged)
            role.Enrage(RageRefreshDuration);

        /*
         * Enrageの処理後に明示的に再設定し、
         * 本体側で減算・クリアされた値を上書きする。
         */
        role.TotalEnrageTime = RageRefreshDuration;
        role.EnragedTimeLeft = RageRefreshDuration;
    }

    private void ChangeSpeedState([CanBeNull] Player player, bool isFast)
    {
        if (!Check(player))
            return;

        if (isFast)
        {
            player!.EnableEffect(EffectType.MovementBoost, 50);
            player.DisableEffect(EffectType.Slowness);
            player.EnableEffect(EffectType.Invigorated, 20);
        }
        else
        {
            player!.EnableEffect(EffectType.Slowness, 40);
            player.DisableEffect(EffectType.MovementBoost);
            player.DisableEffect(EffectType.Invigorated);
        }
    }

    /*
     * OMEGA WARHEAD / 通常のALPHA WARHEADは爆発時に生存者を同一フレームで
     * 一斉Killする。その瞬間にChamber Guard(Npc.Spawnで生成したFacilityGuardRole)が
     * 破棄処理中だと、観戦者側のSpectatableListManagerが所有者なしのロールを
     * 参照して例外を吐き続ける(FacilityGuardRole(Clone) does not have an owner)。
     * 実際の一斉死亡より先にNPCを破棄し、破棄が完了する猶予を確保する。
     */
    private static void OnVanillaWarheadDetonating(DetonatingEventArgs ev)
    {
        if (!ev.IsAllowed)
            return;

        DestroyChamberGuards();
    }

    private static void OnMassCasualtyImminent() => DestroyChamberGuards();

    private static void DestroyChamberGuards()
    {
        foreach (Npc npc in Npc.List.ToArray())
        {
            if (npc.CustomName == ChamberGuardName)
                npc.Destroy();
        }
        foreach (Door door in Door.List)
        {
            if (door.Type != DoorType.HeavyContainmentDoor)
                continue;

            if (door.Room is null || door.Room.Type != RoomType.Hcz096)
                continue;

            if (door is BreakableDoor breakableDoor)
            {
                if (!breakableDoor.CanBreak()) continue;
                breakableDoor.Break();
            }
        }
    }

    private void CleanupPlayer(Player player)
    {
        if (player is null)
            return;

        /*
         * 最初にセッションを消し、古いCallDelayedを無効化する。
         */
        _sessions.Remove(player);

        if (_maintenanceCoroutines.Remove(
                player,
                out CoroutineHandle coroutine))
        {
            Timing.KillCoroutines(coroutine);
        }

        _shyGuyPositions.Remove(player);
        _inTryNotToCryAnim.Remove(player);
        _persistentTargets.Remove(player);

        _restoringTargets.RemoveWhere(
            key => key.ScpId == player.Id || key.TargetId == player.Id);

        if (!player.IsConnected)
            return;

        player.DisableEffect(EffectType.DamageReduction);
        player.DisableEffect(EffectType.MovementBoost);
        player.DisableEffect(EffectType.Invigorated);
        player.DisableEffect(EffectType.Slowness);
    }
}