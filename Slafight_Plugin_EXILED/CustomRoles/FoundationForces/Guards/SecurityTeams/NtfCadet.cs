using System;
using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces.Guards.SecurityTeams;

public class NtfCadet : CustomRole
{
    public override string Name => "Nine-tailed Fox Cadet";
    public override string Description => "MTFの士官養成課程を受講し訓練に励んでいる士官候補生。\n" +
                                          "本来まだ訓練中だがサイト-02の死守の為に保安部隊の隊長として緊急動員された。\n" +
                                          "その為装備が一世代前の型落ち品となっている。";
    public override string CustomInfo => Name;
    public override CustomTeam Team => CustomTeam.Get<GuardTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.NtfPrivate;
    public override float? MaxHealth => 100f;
    public override int ForceRolePower => 2;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.KeycardMTFPrivate,
        ItemType.GunCrossvec,
        ItemType.Medkit,
        ItemType.Radio,
        ItemType.ArmorCombat
    ];

    public override IReadOnlyList<Type> CustomItems { get; } =
    [
        typeof(EmergencyForceKeycard)
    ];

    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort>
        {
            [ItemType.Ammo9x19] = 160,
            [ItemType.Ammo556x45] = 40,
        };
}
