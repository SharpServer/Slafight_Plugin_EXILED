using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfGeneral : CustomRole
{
    public override string Name => "Nine-tailed Fox General";

    public override string CustomInfo => Name;
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfCaptain;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 6;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.GrenadeHE,
        ItemType.Radio,
        ItemType.ArmorHeavy
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(GunFRMGX),
        typeof(MediHolder)
    ];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 80,
            [ItemType.Ammo556x45] = 160,
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
