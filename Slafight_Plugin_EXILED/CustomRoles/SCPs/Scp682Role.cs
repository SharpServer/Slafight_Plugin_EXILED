using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp939;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp682Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-682";

    protected override string Description { get; set; } = "不死身の爬虫類とまで恐れられた最強クラスのSCP。\n" +
                                                          "その危険性から長い間眠らされていたが、大規模な収容違反の影響により\n" +
                                                          "遂に目覚めることができた。今まで抑え込まれていた物を全て解き放ち、\n" +
                                                          "<color=red>忌まわしき財団を破壊せよ！</color>";

    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp682;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp682";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp939;
    protected override float? SpawnMaxHealth => 999f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-682";
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.FocusedVision, 255),
        new(EffectType.NightVision, 255)
    ];

    public override bool CanUseProximityChat => true;

    private static readonly Dictionary<Player, float> SpeedLevels = new();
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        CleanupPlayer(player);
        player.MaxHumeShield = 1200;
        player.HumeShieldRegenerationMultiplier = 13.5f;

        SpeedLevels[player] = 1f;
        player.SetScale(new Vector3(0.7f, 0.75f, 1.2f));
        
        Timing.RunCoroutine(WaitAndTeleport(player));
        Timing.RunCoroutine(Coroutine(player));
    }

    private IEnumerator<float> WaitAndTeleport(Player player)
    {
        // スポーンポイントが初期化されるまで待機（最大10秒）
        float elapsed = 0f;
        while (MapFlags.Scp682SpawnPoint == Vector3.zero && elapsed < 10f)
        {
            yield return Timing.WaitForSeconds(RoleSpawnTimings.SpawnPointPollInterval);
            elapsed += RoleSpawnTimings.SpawnPointPollInterval;
            if (!Check(player)) yield break;
        }

        yield return Timing.WaitForSeconds(RoleSpawnTimings.AfterRoleSet);
        if (!Check(player))
            yield break;

        TrySetPosition(player, MapFlags.Scp682SpawnPoint, nameof(WaitAndTeleport));
    }

    private static bool IsSafePlayerTarget(Player player)
    {
        try
        {
            return player?.ReferenceHub != null &&
                   (player.IsNPC || player.IsConnected) &&
                   player.Role.Type != RoleTypeId.Destroyed;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetPosition(Player player, Vector3 position, string context)
    {
        if (!IsSafePlayerTarget(player))
        {
            Log.Warn($"[Scp682Role] Skipped teleport during {context}: target is no longer valid.");
            return;
        }

        try
        {
            player.Position = position;
        }
        catch (System.Exception ex)
        {
            Log.Warn($"[Scp682Role] Skipped teleport during {context}: {ex.Message}");
        }
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        for (;;)
        {
            if (player?.ReferenceHub == null || player.GetCustomRole() != CRoleTypeId.Scp682)
            {
                CleanupPlayer(player);
                yield break;
            }

            if (!SpeedLevels.TryGetValue(player, out float speedLevel))
                speedLevel = SpeedLevels[player] = 1f;
            
            SpeedLevels[player] *= 1.0005f;
            
            // ★ 修正: RoleSpecificTextProvider を使用
            RoleSpecificTextProvider.Set(
                player,
                "Awaken Status: " + SpeedLevels[player].ToString("F2")
            );
            
            yield return Timing.WaitForSeconds(1f);
        }
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Attacker != null && SpeedLevels.TryGetValue(ev.Attacker, out float level))
        {
            ev.Amount *= level;
            SpeedLevels[ev.Attacker] = level + ev.Amount / 10000;
        }
    }
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 6 8 2", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        base.OnRoleChanging(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        base.OnRoleLeft(ev);
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp939.PlacingAmnesticCloud += OnAmnesia;
        Exiled.Events.Handlers.Scp939.PlacingMimicPoint += OnMimic;
        Exiled.Events.Handlers.Scp939.PlayingFootstep += OnPF;
        Exiled.Events.Handlers.Scp939.PlayingSound += OnPS;
        Exiled.Events.Handlers.Scp939.PlayingVoice += OnPV;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp939.PlacingAmnesticCloud -= OnAmnesia;
        Exiled.Events.Handlers.Scp939.PlacingMimicPoint -= OnMimic;
        Exiled.Events.Handlers.Scp939.PlayingFootstep -= OnPF;
        Exiled.Events.Handlers.Scp939.PlayingSound -= OnPS;
        Exiled.Events.Handlers.Scp939.PlayingVoice -= OnPV;
        base.UnregisterEvents();
    }

    private void OnAmnesia(PlacingAmnesticCloudEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
    }

    private void OnMimic(PlacingMimicPointEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
    }

    private void OnPF(PlayingFootstepEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
    }
    
    private void OnPS(PlayingSoundEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
    }

    private void OnPV(PlayingVoiceEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
    }

    private static void CleanupPlayer(Player player)
    {
        if (player == null)
            return;

        SpeedLevels.Remove(player);
        RoleSpecificTextProvider.Clear(player);
    }
}
