using Exiled.API.Features.Items;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using MapHandlers = Exiled.Events.Handlers.Map;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class FakeGrenade : CItem
{
    public override string DisplayName => "多目的グレネード";
    public override string Description =>
        "様々な清掃やドア破壊等、多種多様な用途に使う特殊グレネード。\n人体等に被害はないらしい";

    protected override string UniqueKey => "FakeGrenade";
    protected override ItemType BaseItem => ItemType.GrenadeHE;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.green;

    public override void RegisterEvents()
    {
        MapHandlers.ExplodingGrenade += OnExploding;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        MapHandlers.ExplodingGrenade -= OnExploding;
        base.UnregisterEvents();
    }

    protected override void OnThrowingRequest(ThrowingRequestEventArgs ev)
    {
        if (ev.Item is ExplosiveGrenade eg)
            eg.FuseTime = 0.5f;
    }

    protected override void OnPickingUp(PickingUpItemEventArgs ev)
    {
        ev.Player.SetCategoryLimit(ItemCategory.Grenade,
            (sbyte)(ev.Player.GetCategoryLimit(ItemCategory.Grenade) + 1));
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.Player.SetCategoryLimit(ItemCategory.Grenade,
            (sbyte)(ev.Player.GetCategoryLimit(ItemCategory.Grenade) - 1));
    }

    private void OnExploding(ExplodingGrenadeEventArgs ev)
    {
        if (!Check(ev.Projectile)) return;
        if (UnityEngine.Random.Range(0, 11) != 0)
        {
            ev.TargetsToAffect.Clear();
        }
        else
        {
            ev.Player?.ShowRueiPlus("<color=red><size=32>不良品だった！！！</size></color>");
        }
    }
}
