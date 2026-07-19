using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp076Role : CRole
{
    private const int ResistanceKillThreshold = 3;
    private const float ResistanceCountdownSeconds = 600f;
    private const float OmegaSevenLossCheckDelaySeconds = RoleSpawnTimings.CustomRoleRemovalCleanup;
    private const byte BaseMovementIntensity = 25;
    private const byte BoostedMovementIntensity = 40;

    protected override string RoleName { get; set; } = "SCP-076";
    protected override string Description { get; set; } =
        "機動部隊Omega-7 \"Pandra's Box\" に運用される、財団制御下の異常戦闘員。\n" +
        "槍とアビリティを使い、財団の敵対勢力を殲滅せよ。\n" +
        "未反逆の間は財団側の勝利に貢献する。\n" +
        "<color=#ff3333>財団職員を3人殺害、またはOmega-7が全滅すると反逆状態となり、10分後に抑制装置が起爆する。</color>";
    protected override float DescriptionDuration { get; set; } = 15f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp076;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp076";
    protected override RoleTypeId? TeamNpcRoleTypeId { get; set; } = RoleTypeId.NtfPrivate;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 1500f;
    protected override bool SpawnClearsInventory => true;

    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(Spear)
    ];

    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new CRoleEffect<NaturalHeal>(),
        new(EffectType.Scp1853),
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.PickingDisabled,
        SpecificFlagType.DroppingDisabled
    ];

    protected override RoleTypeId? GetTeamNpcRoleTypeId(Player player)
        => IsResistanceState(player) ? RoleTypeId.Scp0492 : TeamNpcRoleTypeId;

    // playerId -> 適用中の MovementBoost 強度
    private static readonly Dictionary<int, byte> MovementIntensities = [];
    // playerId -> このロールセッション中のキル数
    private static readonly Dictionary<int, int> KillCounts = [];
    // 反逆状態に入っている playerId
    private static readonly HashSet<int> ResistancePlayerIds = [];
    // 抑制装置の起爆処理中の playerId（死因放送を専用のものへ切り替える判定に使用）
    private static readonly HashSet<int> SuppressionDetonatingIds = [];
    private static int _omegaSevenLossCheckGeneration;

    private const string SuppressionDeviceKillReason = "抑制装置により爆発された";

    // 抑制装置の起爆による終了専用の死因放送。
    private static readonly TerminationCause SuppressionDeviceCause = TerminationCause.Custom(
        "successfully terminated by detonation of the restraint device .",
        "は、<color=#ff3333>抑制装置</color>の起爆により終了されました。");

    public static bool IsResistanceState(Player? player)
        => player?.ReferenceHub != null && ResistancePlayerIds.Contains(player.Id);

    public static bool IsActiveScp076(Player? player)
        => player?.ReferenceHub != null &&
           (player.IsConnected || player.IsNPC) &&
           player.IsAlive &&
           player.GetCustomRole() == CRoleTypeId.Scp076;

    public static bool TryDetonateSuppressionDevice(Player? player, ProjectileType? projectile = null)
    {
        if (!IsActiveScp076(player))
            return false;

        DetonateSuppressionDevice(player, projectile);
        return true;
    }

    public static bool IsFoundationAlignedForVictory(Player? player)
    {
        if (player?.ReferenceHub == null)
            return false;

        return player.GetCustomRole() == CRoleTypeId.Scp076 &&
               !IsResistanceState(player);
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        TrySetPlayerPosition(player, PositionProvider.GetNtfSpawnPosition(), nameof(Scp076Role));

        player.SetCustomInfo($"<color={ServerColors.DeepPink}>SCP-076</color>");
        
        player.CustomHumeShieldStat.MaxValue = 500f;
        player.CustomHumeShieldStat.CurValue = 500f;
        player.CustomHumeShieldStat.ShieldRegenerationMultiplier = 2.5f;

        player.AddAbility<AbsolutePowerAbility>();
        player.AddAbility<GenerateWeaponAbility>();

        CleanupPlayerState(player);
        MovementIntensities[player.Id] = BaseMovementIntensity;
        Timing.RunCoroutine(MovementBoostLoop(player));
        ScheduleOmegaSevenLossCheck("SCP-076 spawned");
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Died += OnAnyPlayerDied;
        Exiled.Events.Handlers.Player.ChangingRole += OnAnyPlayerChangingRole;
        Exiled.Events.Handlers.Player.Left += OnAnyPlayerLeft;
        RoundVictoryEvents.RoundEnded += OnRoundEnded;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ResetRoundState;
        Exiled.Events.Handlers.Server.RestartingRound += ResetRoundState;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Died -= OnAnyPlayerDied;
        Exiled.Events.Handlers.Player.ChangingRole -= OnAnyPlayerChangingRole;
        Exiled.Events.Handlers.Player.Left -= OnAnyPlayerLeft;
        RoundVictoryEvents.RoundEnded -= OnRoundEnded;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetRoundState;
        Exiled.Events.Handlers.Server.RestartingRound -= ResetRoundState;
        base.UnregisterEvents();
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        var target = $"<color={Team.GetTeamColor()}>{RoleName}</color>";
        if (ev.Player != null && SuppressionDetonatingIds.Contains(ev.Player.Id))
            CassieHelper.AnnounceTermination(ev, "SCP 0 7 6", target, SuppressionDeviceCause, true);
        else
            CassieHelper.AnnounceTermination(ev, "SCP 0 7 6", target, true);
        CleanupPlayerState(ev.Player);
        base.OnRoleDying(ev);
    }

    /// <summary>
    /// 抑制装置の起爆としてアベルを爆死させる。
    /// 起爆中フラグを立ててから爆破するため、<see cref="OnRoleDying"/> 側で専用の死因放送に切り替えられる。
    /// </summary>
    /// <param name="projectile">爆破に使うプロジェクタイル。null の場合は <see cref="Player.Explode()"/>（自己爆発）を使う。</param>
    private static void DetonateSuppressionDevice(Player player, ProjectileType? projectile = null)
    {
        SuppressionDetonatingIds.Add(player.Id);
        if (projectile.HasValue)
            player.Explode(projectile.Value);
        else
            player.Explode();
        if (player.IsAlive)
            player.Kill(SuppressionDeviceKillReason);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupPlayerState(ev.Player);
        base.OnRoleChanging(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupPlayerState(ev.Player);
        base.OnRoleLeft(ev);
    }

    // ===== キル計上（Died 一本に統一）=====

    private void OnAnyPlayerDied(DiedEventArgs ev)
    {
        var attacker = ev?.Attacker;
        var target = ev?.Player;

        if (IsOmegaSevenController(target))
            ScheduleOmegaSevenLossCheck("Omega-7 died");

        if (!Check(attacker)) return;
        if (target == null) return;
        if (attacker.Id == target.Id) return;
        if (!IsFoundationPersonnel(target)) return;

        var kills = KillCounts.GetValueOrDefault(attacker.Id) + 1;
        KillCounts[attacker.Id] = kills;

        Log.Debug($"[SCP-076] kill recorded: attacker={attacker.Nickname}({attacker.Id}) kills={kills}");

        ScheduleKillReward(attacker);

        if (kills >= ResistanceKillThreshold)
            EnterResistanceState(attacker);
    }

    private void OnAnyPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null || !ev.IsAllowed)
            return;

        if (IsOmegaSevenController(ev.Player))
            ScheduleOmegaSevenLossCheck("Omega-7 changed role");
    }

    private void OnAnyPlayerLeft(LeftEventArgs ev)
    {
        if (IsOmegaSevenController(ev?.Player))
            ScheduleOmegaSevenLossCheck("Omega-7 left");
    }

    private static bool IsCountable(Player? player)
        => player is not null && player.IsNotHost();

    private static bool IsFoundationPersonnel(Player? player)
    {
        if (player?.ReferenceHub == null)
            return false;

        return player.GetTeam() is CTeam.FoundationForces or CTeam.Scientists or CTeam.Guards or CTeam.O5;
    }

    // ===== キル報酬：60秒後に火力と移動速度を一時強化 =====

    private void ScheduleKillReward(Player attacker)
    {
        var attackerId = attacker.Id;

        Timing.CallDelayed(60f, () =>
        {
            var current = Player.Get(attackerId);
            if (current == null || !Check(current) || !current.IsAlive) return;

            current.EnableEffect<DamageBoost>(20, 30f);

            if (!MovementIntensities.ContainsKey(current.Id)) return;
            MovementIntensities[current.Id] = BoostedMovementIntensity;
            Timing.RunCoroutine(ResetMovementAfter(current.Id, 30f));
        });
    }

    // ===== 反逆状態 =====

    private void ScheduleOmegaSevenLossCheck(string reason)
    {
        int generation = ++_omegaSevenLossCheckGeneration;

        Timing.CallDelayed(OmegaSevenLossCheckDelaySeconds, () =>
        {
            if (generation != _omegaSevenLossCheckGeneration)
                return;

            EnterResistanceIfOmegaSevenLost(reason);
        });
    }

    private void EnterResistanceIfOmegaSevenLost(string reason)
    {
        if (Round.IsLobby || Round.IsEnded)
            return;

        if (HasAliveOmegaSevenControllers())
            return;

        var triggered = 0;
        foreach (var player in Player.List)
        {
            if (!Check(player) || !player.IsAlive || IsResistanceState(player))
                continue;

            EnterResistanceState(
                player,
                "Omega-7が不在となったため、あなたは財団に反逆した！");
            triggered++;
        }

        if (triggered > 0)
            Log.Debug($"[SCP-076] Resistance state started because Omega-7 was lost. Reason={reason}, Count={triggered}");
    }

    private static bool HasAliveOmegaSevenControllers()
    {
        foreach (var player in Player.List)
        {
            if (player == null || !player.IsAlive)
                continue;

            if (IsOmegaSevenController(player))
                return true;
        }

        return false;
    }

    private static bool IsOmegaSevenController(Player? player)
    {
        if (player?.ReferenceHub == null)
            return false;

        return player.GetCustomRole() is CRoleTypeId.PdxWarden or CRoleTypeId.PdxWatcher;
    }

    private void EnterResistanceState(Player player, string? triggerMessage = null)
    {
        if (!Check(player) || !player.IsAlive) return;
        if (!ResistancePlayerIds.Add(player.Id)) return;

        player.SetCustomInfo($"<color={ServerColors.Red}>SCP-076</color>");
        RefreshTeamNpc(player);
        ShowResistanceWarning(player, triggerMessage);
        Timing.RunCoroutine(ResistanceCountdownLoop(player));

        Log.Debug($"[SCP-076] Resistance state started for {player.Nickname}({player.Id}).");
    }

    private void OnRoundEnded(RoundVictoryEndedEventArgs ev)
    {
        foreach (var player in ev.AlivePlayers)
        {
            if (!Check(player)) continue;

            DetonateSuppressionDevice(player);
        }
    }

    // ===== コルーチン =====

    private IEnumerator<float> MovementBoostLoop(Player player)
    {
        while (true)
        {
            if (Round.IsEnded || !Check(player))
            {
                MovementIntensities.Remove(player.Id);
                yield break;
            }

            if (MovementIntensities.TryGetValue(player.Id, out var intensity))
            {
                if (player.TryGetEffect(out MovementBoost mb))
                    mb.Intensity = intensity;
                else
                    player.EnableEffect<MovementBoost>(intensity: intensity, duration: 5f);
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static IEnumerator<float> ResetMovementAfter(int playerId, float seconds)
    {
        yield return Timing.WaitForSeconds(seconds);

        if (MovementIntensities.ContainsKey(playerId))
            MovementIntensities[playerId] = BaseMovementIntensity;
    }

    private IEnumerator<float> ResistanceCountdownLoop(Player player)
    {
        var elapsed = 0f;

        while (true)
        {
            if (Round.IsLobby || !Check(player))
            {
                CleanupPlayerState(player);
                yield break;
            }

            UpdateResistanceHud(player, Mathf.CeilToInt(ResistanceCountdownSeconds - elapsed));

            if (elapsed >= ResistanceCountdownSeconds)
            {
                DetonateSuppressionDevice(player, ProjectileType.FragGrenade);

                CleanupPlayerState(player);
                yield break;
            }

            elapsed++;
            yield return Timing.WaitForSeconds(1f);
        }
    }

    // ===== HUD / 警告 =====

    private static void ShowResistanceWarning(Player player, string? triggerMessage = null)
    {
        triggerMessage ??= "あなたは財団に反逆した！";
        string warning =
            $"<size=26><color=red><b>※{triggerMessage}ロール名が赤くなりました。\n10分後に抑制装置が起爆し爆死します！</b></color></size>";

        player.ShowHint(warning, 8f);
        EffectedInfoTextProvider.Set(
            player,
            $"<color=#ff3333><b>SCP-076 反逆状態</b></color>\n<size=21>{triggerMessage}</size>\n<size=21>10分後に抑制装置が起爆します。</size>",
            8f);
        UpdateResistanceHud(player, (int)ResistanceCountdownSeconds);
    }

    private static void UpdateResistanceHud(Player player, int remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0, remainingSeconds);
        RoleSpecificTextProvider.Set(
            player,
            $"<color=#ff3333><b>[反逆状態]</b></color>\n抑制装置: 起動済み\n起爆まで: {remainingSeconds / 60:00}:{remainingSeconds % 60:00}");
    }

    // ===== 状態クリーンアップ =====

    private static void CleanupPlayerState(Player? player)
    {
        if (player?.ReferenceHub == null) return;

        MovementIntensities.Remove(player.Id);
        KillCounts.Remove(player.Id);
        ResistancePlayerIds.Remove(player.Id);
        SuppressionDetonatingIds.Remove(player.Id);
        RoleSpecificTextProvider.Clear(player);
        EffectedInfoTextProvider.Clear(player);
    }

    private static void ResetRoundState()
    {
        foreach (var player in Player.List)
        {
            if (player == null) continue;
            if (MovementIntensities.ContainsKey(player.Id) ||
                KillCounts.ContainsKey(player.Id) ||
                ResistancePlayerIds.Contains(player.Id))
            {
                RoleSpecificTextProvider.Clear(player);
                EffectedInfoTextProvider.Clear(player);
            }
        }

        MovementIntensities.Clear();
        KillCounts.Clear();
        ResistancePlayerIds.Clear();
        SuppressionDetonatingIds.Clear();
        _omegaSevenLossCheckGeneration++;
    }
}
