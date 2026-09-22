using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfMedicalSpecialist : CustomRole
{
    public override string Name => "Nine-tailed Fox Medical Specialist";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfSpecialist;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 2;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunCrossvec,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(MediHolder)
    ];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 160,
            [ItemType.Ammo556x45] = 40,
        };
    
    protected override void OnSpawned()
    {
        Player.GetCustomItems<MediHolder>().FirstOrDefault()?.HolderInventory = 
        [
            ItemType.Medkit,
            ItemType.Medkit,
            ItemType.Painkillers,
            ItemType.Painkillers,
            ItemType.Painkillers,
        ];
        base.OnSpawned();
    }
}
