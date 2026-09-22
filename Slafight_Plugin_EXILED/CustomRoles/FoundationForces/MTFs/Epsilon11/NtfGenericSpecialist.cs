using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfGenericSpecialist : CustomRole
{
    public override string Name => "Nine-tailed Fox Generic Specialist";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfSpecialist;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 2;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunE11SR,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(MCB3),
        typeof(MediHolder)
    ];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 40,
            [ItemType.Ammo556x45] = 120,
        };
    
    protected override void OnSpawned()
    {
        Player.GetCustomItems<MediHolder>().FirstOrDefault()?.HolderInventory = 
        [
            ItemType.Adrenaline,
            ItemType.Medkit
        ];
        base.OnSpawned();
    }
}
