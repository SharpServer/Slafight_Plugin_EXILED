using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp173;
using Exiled.Events.Handlers;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp173Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-173";
    protected override string Description { get; set; } = "相手が瞬きしたときに超高速で移動し、首をへし折る。\nメインヴィランアビリティでランダムな場所にテレポートできる。\n汚物作戦アビリティで周囲5m以内に汚物を生成できる。";
    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp173;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp173";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp173;
    protected override float? SpawnMaxHealth => 4500f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-173";

    public override void RegisterEvents()
    {
        Scp173.Blinking += OnBlinking;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp173.Blinking -= OnBlinking;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.MaxHumeShield = 1500f;
        player.HumeShield = player.MaxHumeShield;

        player.EnableEffect(EffectType.Slowness, 95, 60f);

        player.AddAbility<TeleportRandomAbility>();
        player.AddAbility<PlaceTantrumAbility>();
        Timing.RunCoroutine(WaitAndTeleport(player));
    }

    private IEnumerator<float> WaitAndTeleport(Player player)
    {
        // スポーンポイントが初期化されるまで待機（最大10秒）
        float elapsed = 0f;
        while (MapFlags.Scp173SpawnPoint == Vector3.zero && elapsed < 10f)
        {
            yield return Timing.WaitForSeconds(RoleSpawnTimings.SpawnPointPollInterval);
            elapsed += RoleSpawnTimings.SpawnPointPollInterval;
            if (!Check(player)) yield break;
        }

        yield return Timing.WaitForSeconds(RoleSpawnTimings.AfterRoleSet);
        if (!Check(player))
            yield break;

        TrySetPosition(player, MapFlags.Scp173SpawnPoint, nameof(WaitAndTeleport));
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
            Log.Warn($"[Scp173Role] Skipped teleport during {context}: target is no longer valid.");
            return;
        }

        try
        {
            player.Position = position;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Scp173Role] Skipped teleport during {context}: {ex.Message}");
        }
    }

    private void OnBlinking(BlinkingEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.Targets.Count >= 3)
        {
            ev.Scp173.BlinkReady = false;
        }
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 1 7 3", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
