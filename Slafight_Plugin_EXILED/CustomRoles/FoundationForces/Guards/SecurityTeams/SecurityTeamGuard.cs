using System;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.CustomTeams;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.Guards.SecurityTeams;

public class SecurityTeamGuard : CustomRole
{
    public override string Name => "Security Team Guard";
    public override string Description => "緊急時に派遣されてくる保安部隊の一般隊員。\n" +
                                          "上位の警備員カードを所持しており、強力な武器での制圧を目指す。";
    public override string CustomInfo => Name;
    public override CustomTeam Team => CustomTeam.Get<GuardTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.FacilityGuard;
    public override Vector3? SpawnPosition => PositionProvider.GetNtfSpawnPosition();
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 1;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunFSP9,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];

    public override IReadOnlyList<Type> CustomItems { get; } =
    [
        typeof(SecurityTeamKeycard)
    ];

    public override IReadOnlyDictionary<ItemType, ushort> Ammo => new Dictionary<ItemType, ushort>
    {
        [ItemType.Ammo9x19] = 160, 
        [ItemType.Ammo556x45] = 40,
    };
}
