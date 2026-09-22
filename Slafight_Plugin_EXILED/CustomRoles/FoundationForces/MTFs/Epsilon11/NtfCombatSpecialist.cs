using System;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfCombatSpecialist : CustomRole
{
    public override string Name => "Nine-tailed Fox Combat Specialist";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfSpecialist;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 2;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunE11SR,
        ItemType.Adrenaline,
        ItemType.Adrenaline,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(MCB3),
        typeof(MCB3)
    ];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 40,
            [ItemType.Ammo556x45] = 120,
        };
}
