using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SNAV310 : CItem
{
    public override string DisplayName => "S-Nav 310 Navigator";
    public override string Description =>
        "S-Nav 300が改良され、電池不要かつマップが拡張されている。\n様々な近くのユニークな部屋について調べられる。\n投げて使用可能";

    protected override string UniqueKey => "SNAV310";
    protected override ItemType BaseItem => ItemType.Radio;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.cyan;

    private RadioRange _mode;

    private static readonly List<RoomType> Targets =
    [
        RoomType.Lcz914,
        RoomType.Lcz330,
        RoomType.LczGlassBox,
        RoomType.Hcz127,
        RoomType.HczCrossRoomWater,
        RoomType.HczHid,
        RoomType.HczNuke,
        RoomType.HczTestRoom,
        RoomType.EzIntercom,
        RoomType.EzGateA,
        RoomType.EzGateB,
    ];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.ChangingRadioPreset += OnChangingRadioPreset;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.ChangingRadioPreset -= OnChangingRadioPreset;
        base.UnregisterEvents();
    }

    private void OnChangingRadioPreset(ChangingRadioPresetEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        _mode = ev.NewValue;
        ev.Player.ShowRueiPlus(SnavCommon.RangeHint(ev.NewValue));
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;
        if (Item.Get<Radio>(ev.Item.Base) is null) return;

        ev.IsAllowed = false;
        var detected = SnavCommon.DetectRooms(ev.Player.Position, _mode, Targets);
        ev.Player.ShowRueiPlus(SnavCommon.RoomsHint(_mode, detected, ev.Player.Position), 10f);
    }
}
