using System;
using System.Collections.Generic;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomItems.Weapons;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

public sealed class FifthistGuidance : FifthistRole
{
    public override string Name => "第五教会 案内人";
    public override string Description => "第五主義を広め、人々を第五世界へと誘う案内人。\n杖を使って相手を第五すると第五主義者に出来る。";
    protected override string InfoName => "Fifthist Guidance";
    public override string HudLabel => $"<color={ServerColors.Magenta}>Fifthist: Guidance</color>";
    public override string Objective => "杖を用い、第五主義を施設に広めなさい。あなたの導きは教会にとって重要です！";
    public override float? MaxHealth => 125f;
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.ArmorLight,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.SCP500,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(CaneOfTheStars), typeof(FifthistKeycard)];
}
