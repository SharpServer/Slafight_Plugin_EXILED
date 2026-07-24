using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SNAV300 : CItem
{
    public override string DisplayName => "S-Nav 300";
    public override string Description => "近くのユニークな部屋について調べられる。\n投げて使用可能";

    protected override string UniqueKey => "SNAV300";
    protected override ItemType BaseItem => ItemType.Radio;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.green;

    private RadioRange _mode;

    private static readonly List<RoomType> Targets =
    [
        RoomType.Lcz914,
        RoomType.Hcz127,
        RoomType.HczCrossRoomWater,
        RoomType.HczHid,
        RoomType.HczNuke,
        RoomType.EzIntercom,
        RoomType.EzGateA,
        RoomType.EzGateB,
    ];

    public override void RegisterEvents()
    {
        Player.ChangingRadioPreset += OnChangingRadioPreset;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.ChangingRadioPreset -= OnChangingRadioPreset;
        base.UnregisterEvents();
    }

    private void OnChangingRadioPreset(ChangingRadioPresetEventArgs ev)
    {
        if (ev.Radio.BatteryLevel < 10) return;
        if (!Check(ev.Item)) return;
        _mode = ev.NewValue;
        ev.Player.ShowHint(SnavCommon.RangeHint(ev.NewValue));
    }

    /// <summary>投げる (Drop=Throw) 操作で使用 — バッテリー消費 + 部屋検知。</summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;
        if (Item.Get<Radio>(ev.Item.Base) is not { } radio) return;

        ev.IsAllowed = false;

        var consumption = SnavCommon.Consumption(_mode);
        if (radio.BatteryLevel < consumption)
        {
            ev.Player.ShowHint("バッテリー不足！");
            return;
        }

        radio.BatteryLevel -= (byte)consumption;

        var detected = SnavCommon.DetectRooms(ev.Player.Position, _mode, Targets);
        ev.Player.ShowHint(SnavCommon.RoomsHint(_mode, detected, ev.Player.Position), 10f);
    }
}
