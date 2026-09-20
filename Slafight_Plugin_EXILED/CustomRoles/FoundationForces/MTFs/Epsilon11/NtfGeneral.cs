using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomItems.Weapons;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfGeneral : CustomRole
{
    public override string Name => "Nine-tailed Fox General";
    public override RoleTypeId BaseRole => RoleTypeId.NtfCaptain;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 4;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFCaptain,
        ItemType.Adrenaline,
        ItemType.Medkit,
        ItemType.GrenadeHE,
        ItemType.Radio,
        ItemType.ArmorHeavy
    ];
    public override IReadOnlyList<Type> CustomItems =>
    [
        typeof(GunFRMGX)
    ];
}