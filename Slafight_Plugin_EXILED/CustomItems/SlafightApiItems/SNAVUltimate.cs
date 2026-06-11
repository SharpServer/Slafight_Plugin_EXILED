using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SNAVUltimate : CItem
{
    public override string DisplayName => "S-Nav Ultimate";
    public override string Description =>
        "SCP-914によって改良されたS-Nav。\n電池不要かつマップが大幅に拡張されており、SCPの情報も得られる。\n" +
        "より多くの、近くのユニークな部屋について調べられる。\n投げて使用可能";

    protected override string UniqueKey => "SNAVUltimate";
    protected override ItemType BaseItem => ItemType.Radio;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.blue;

    private RadioRange _mode;

    private static readonly List<RoomType> Targets =
    [
        RoomType.Lcz914,
        RoomType.Lcz330,
        RoomType.LczGlassBox,
        RoomType.LczArmory,
        RoomType.LczCheckpointA,
        RoomType.LczCheckpointB,
        RoomType.Hcz127,
        RoomType.HczCrossRoomWater,
        RoomType.HczHid,
        RoomType.HczNuke,
        RoomType.HczTestRoom,
        RoomType.Hcz049,
        RoomType.Hcz106,
        RoomType.HczElevatorA,
        RoomType.HczElevatorB,
        RoomType.HczEzCheckpointA,
        RoomType.HczEzCheckpointB,
        RoomType.HczTesla,
        RoomType.EzIntercom,
        RoomType.EzDownstairsPcs,
        RoomType.EzPcs,
        RoomType.EzSmallrooms,
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
        ev.Player.ShowHint(SnavCommon.RangeHint(ev.NewValue));
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;
        if (Item.Get<Radio>(ev.Item.Base) is null) return;

        ev.IsAllowed = false;
        var pos = ev.Player.Position;
        var rooms = SnavCommon.DetectRooms(pos, _mode, Targets);
        var scps = DetectScps(pos, _mode);

        var roomsText = rooms.Count > 0
            ? string.Join("\n", rooms.Select(r => $"{r.Type.TranslateRoomName()}: {Vector3.Distance(pos, r.Position):F0}m"))
            : "なし";
        var scpsText = scps.Count > 0
            ? string.Join("\n", scps.Select(p => $"{p.Nickname} ({p.Role.Type}): {Vector3.Distance(pos, p.Position):F0}m"))
            : "なし";

        ev.Player.ShowHint($"[{_mode}]検知された部屋：\n{roomsText}\n\n検知されたSCP：\n{scpsText}",
            10f);
    }

    private static List<Player> DetectScps(Vector3 pos, RadioRange mode)
    {
        var range = mode switch
        {
            RadioRange.Short  => 30f,
            RadioRange.Medium => 60f,
            RadioRange.Long   => 80f,
            RadioRange.Ultra  => 100f,
            _ => 0f,
        };

        if (range <= 0f) return [];

        return Player.List
            .Where(p => p != null && p.IsAlive && p.GetTeam() == CTeam.SCPs
                        && Vector3.Distance(pos, p.Position) <= range)
            .OrderBy(p => Vector3.Distance(pos, p.Position))
            .ToList();
    }
}
