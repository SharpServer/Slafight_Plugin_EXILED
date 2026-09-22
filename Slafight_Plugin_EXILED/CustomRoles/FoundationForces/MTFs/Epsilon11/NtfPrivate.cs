using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

public class NtfPrivate : CustomRole
{
    public override string Name => "Nine-tailed Fox Private";
    public override CustomTeam Team => CustomTeam.Get<FoundationTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfPrivate;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 1;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFOperative,
        ItemType.GunCrossvec,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];

    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 160,
            [ItemType.Ammo556x45] = 40,
        };
}
