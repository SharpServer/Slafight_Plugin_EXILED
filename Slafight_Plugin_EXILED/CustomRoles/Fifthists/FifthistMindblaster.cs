using System;
using System.Collections.Generic;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

public sealed class FifthistMindblaster : FifthistRole
{
    public override string Name => "第五教会 思壊師";
    public override string Description => $"<color={ServerColors.Magenta}>第五の力</color>を用いて敵の思念を破壊する";
    protected override string InfoName => "Fifthist Mindblaster";
    public override string HudLabel => $"<color={ServerColors.Magenta}>Fifthist: Mindblaster</color>";
    public override string Objective => "第五の力を用いて、全ての思考を第五で破壊しろ！";
    public override float? MaxHealth => 155f;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.ArmorLight,
        ItemType.Medkit,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistKeycard), typeof(Mindblaster)];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo => new Dictionary<ItemType, ushort>
    {
        [ItemType.Ammo762x39] = 80,
    };
}
