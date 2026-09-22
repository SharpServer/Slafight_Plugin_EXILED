using System;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfContainmentSpecialist : CustomRole
{
    public override string Name => "Nine-tailed Fox Containment Specialist";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfSpecialist;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 2;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.Medkit,
        ItemType.Painkillers,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(GunAnomalyDetainer),
        typeof(GunM82)
    ];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 40,
            [ItemType.Ammo556x45] = 120,
        };
}
