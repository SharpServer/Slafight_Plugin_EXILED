using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfSergeant : CustomRole
{
    public override string Name => "Nine-tailed Fox Sergeant";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfSergeant;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 4;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunE11SR,
        ItemType.GrenadeHE,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(MediHolder)
    ];
    
    protected override void OnSpawned()
    {
        Player.GetCustomItems<MediHolder>().FirstOrDefault()?.HolderInventory = 
        [
            ItemType.Medkit
        ];
        base.OnSpawned();
    }
}
