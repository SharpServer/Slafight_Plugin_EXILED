using System;
using System.Collections.Generic;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

public sealed class FifthistRescuer : FifthistRole
{
    public override string Name => "第五教会 救出師";
    public override string Description => $"非常に<color={ServerColors.Magenta}>第五的</color>な存在を脱出させなければいけない";
    protected override string InfoName => "Fifthist Rescure";
    public override string HudLabel => $"<color={ServerColors.Magenta}>Fifthist: Rescue</color>";
    public override string Objective => "第五を探し出し、救出し、従い、施設を占領せよ。";
    public override float? MaxHealth => 135f;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.ArmorCombat,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.SCP500,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistKeycard)];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo => new Dictionary<ItemType, ushort>
    {
        [ItemType.Ammo762x39] = 180,
    };
}
