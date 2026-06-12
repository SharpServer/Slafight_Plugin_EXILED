using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;
using CapybaraToy = Exiled.API.Features.Toys.Capybara;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class CapybaraRole : CRole
{
    protected override string RoleName { get; set; } = "Xx_CAPYBARA_xX";
    protected override string Description { get; set; } = "WTF";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Capybara;
    protected override string UniqueRoleKey { get; set; } = "CapybaraRole";
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override RoleSpawnFlags? SpawnBaseRoleFlags => RoleSpawnFlags.AssignInventory;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Fade)
    ];

    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(CapybaraMissile)
    ];

    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato9] = 35,
    };

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(RoleSpawnTimings.RoleStateReapply, () =>
        {
            if (!player.IsConnected || !Check(player))
                return;

            player.Scale = Vector3.one * 0.35f;

            var capybara = CapybaraToy.Create(player.Position, player.Rotation);
            capybara.Collidable = false;

            player.Wear(capybara);
        });
    }
}
