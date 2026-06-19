using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Medicine : CItemUsable
{
    public override string DisplayName => "注射器型メディキット";
    public override string Description =>
        "セラムをベースに、医務室のニーズに合わせて開発された即時医療薬。\n" +
        "激務の財団職員をだいたい一発で治せる優れモノ。";

    protected override string UniqueKey => "Medicine";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override string? PickupSchematicName => "Alienisolation_medkit";
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.yellow;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player.Heal(75f);
        ev.Player.AddAhp(60f);
    }
}
