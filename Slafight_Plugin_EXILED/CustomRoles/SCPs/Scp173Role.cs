using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp173;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

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
        Exiled.Events.Handlers.Scp173.Blinking += OnBlinking;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp173.Blinking -= OnBlinking;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.MaxHumeShield = 1500f;
        player.HumeShield = player.MaxHumeShield;

        player.EnableEffect(EffectType.Slowness, 95, 60f);

        player.AddAbility(new TeleportRandomAbility(player));
        player.AddAbility(new PlaceTantrumAbility(player));
        Timing.RunCoroutine(WaitAndTeleport(player));
    }

    private IEnumerator<float> WaitAndTeleport(Player player)
    {
        // スポーンポイントが初期化されるまで待機（最大10秒）
        float elapsed = 0f;
        while (MapFlags.Scp173SpawnPoint == Vector3.zero && elapsed < 10f)
        {
            yield return Timing.WaitForSeconds(0.25f);
            elapsed += 0.25f;
            if (!Check(player)) yield break;
        }

        yield return Timing.WaitForSeconds(0.05f);
        player.Position = MapFlags.Scp173SpawnPoint;
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
