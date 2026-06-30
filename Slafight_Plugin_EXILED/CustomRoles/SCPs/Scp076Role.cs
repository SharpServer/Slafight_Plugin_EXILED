using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.CustomStats;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API;
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
    private const byte BaseMovementIntensity = 25;
    private const byte BoostedMovementIntensity = 40;

    protected override string RoleName { get; set; } = "SCP-076";
    protected override string Description { get; set; } = "W.I.P";
    protected override float DescriptionDuration { get; set; } = 15f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp076;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp076";
    protected override RoleTypeId? TeamNpcRoleTypeId { get; set; } = RoleTypeId.Scp0492;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 1500f;
    protected override bool SpawnClearsInventory => true;

    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(Spear)
    ];

    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Scp1853),
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.PickingDisabled,
        SpecificFlagType.DroppingDisabled
    ];

    // playerId -> 適用中の MovementBoost 強度
    private static readonly Dictionary<int, byte> MovementIntensities = [];
    // playerId -> このロールセッション中のキル数
    private static readonly Dictionary<int, int> KillCounts = [];
    // 反逆状態に入っている playerId
    private static readonly HashSet<int> ResistancePlayerIds = [];

    public static bool IsResistanceState(Player? player)
        => player?.ReferenceHub != null && ResistancePlayerIds.Contains(player.Id);

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
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Died += OnAnyPlayerDied;
        RoundVictoryEvents.RoundEnded += OnRoundEnded;
        Exiled.Events.Handlers.Server.WaitingForPlayers += ResetRoundState;
        Exiled.Events.Handlers.Server.RestartingRound += ResetRoundState;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Died -= OnAnyPlayerDied;
        RoundVictoryEvents.RoundEnded -= OnRoundEnded;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetRoundState;
        Exiled.Events.Handlers.Server.RestartingRound -= ResetRoundState;
        base.UnregisterEvents();
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 0 7 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        CleanupPlayerState(ev.Player);
        base.OnRoleDying(ev);
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

        if (!Check(attacker)) return;
        if (attacker.Id == target.Id) return;

        var kills = KillCounts.GetValueOrDefault(attacker.Id) + 1;
        KillCounts[attacker.Id] = kills;

        Log.Debug($"[SCP-076] kill recorded: attacker={attacker.Nickname}({attacker.Id}) kills={kills}");

        ScheduleKillReward(attacker);

        if (kills >= ResistanceKillThreshold)
            EnterResistanceState(attacker);
    }

    private static bool IsCountable(Player? player)
        => player is not null && player.IsNotHost();

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

    private void EnterResistanceState(Player player)
    {
        if (!Check(player) || !player.IsAlive) return;
        if (!ResistancePlayerIds.Add(player.Id)) return;

        player.SetCustomInfo($"<color={ServerColors.Red}>SCP-076</color>");
        ShowResistanceWarning(player);
        Timing.RunCoroutine(ResistanceCountdownLoop(player));

        Log.Debug($"[SCP-076] Resistance state started for {player.Nickname}({player.Id}).");
    }

    private void OnRoundEnded(RoundVictoryEndedEventArgs ev)
    {
        foreach (var player in ev.AlivePlayers)
        {
            if (!Check(player)) continue;

            player.Explode();
            if (player.IsAlive)
                player.Kill("抑制装置により爆発された");
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
                player.Explode(ProjectileType.FragGrenade);
                if (player.IsAlive)
                    player.Kill("抑制装置により爆発された");

                CleanupPlayerState(player);
                yield break;
            }

            elapsed++;
            yield return Timing.WaitForSeconds(1f);
        }
    }

    // ===== HUD / 警告 =====

    private static void ShowResistanceWarning(Player player)
    {
        const string warning =
            "<size=26><color=red><b>※あなたは財団に反逆した！ロール名が赤くなりました。\n10分後に抑制装置が起爆し爆死します！</b></color></size>";

        player.ShowHint(warning, 8f);
        EffectedInfoTextProvider.Set(
            player,
            "<color=#ff3333><b>SCP-076 反逆状態</b></color>\n<size=21>10分後に抑制装置が起爆します。</size>",
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
    }
}
