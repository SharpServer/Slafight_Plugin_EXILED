using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Scp1425 : CItem
{
    public override string DisplayName => "SCP-1425";
    public override string Description => "第五的な力を感じる・・・";
    protected override string UniqueKey => "Scp1425";
    protected override ItemType BaseItem => ItemType.Medkit;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.magenta;
    protected override string PickupSchematicName => "Scp1425Model";
    protected override bool UseSchematicPickup => true;

    private readonly Dictionary<int, byte> _readCount = [];

    protected override void OnWaitingForPlayers()
    {
        _readCount.Clear();
    }
    
    protected override void OnOwnerDying(DyingEventArgs ev)
    {
        if (ev.Player == null) return;
        _readCount[ev.Player.Id] = 0;
    }

    protected override void OnUsed(UsedItemEventArgs ev)
    {
        if (ev.Player == null) return;

        var count = _readCount.GetValueOrDefault(ev.Player.Id, (byte)0);

        switch (count)
        {
            case 0:
                ev.Player.ShowHint("<size=22>1ページ目\n壊れた星の五本の輻</size>", 5f);
                break;
            case 1:
                ev.Player.ShowHint("<size=22>2ページ目\n永遠に争う五つの元素</size>", 5f);
                break;
            case 2:
                ev.Player.ShowHint($"<size=22>3ページ目\n<color={CTeam.Fifthists.GetTeamColor()}>精神を呼び起こす五つの感覚</color></size>", 5f);
                ev.Player.EnableEffect<Flashed>(3f);
                Timing.CallDelayed(2.5f,
                    () => ev.Player?.SetRole(CRoleTypeId.FifthistConvert, RoleSpawnFlags.AssignInventory));
                break;
            case 3:
                ev.Player.ShowHint("<size=22>4ページ目\n五つの欠片が緩慢に解かれてゆく</size>", 5f);
                break;
            case 4:
                ev.Player.ShowHint($"<size=22>5ページ目\n<b><color={CTeam.Fifthists.GetTeamColor()}>咆哮する黒き月は、見るに値しない幻である</color></b></size>", 5f);
                ev.Player.EnableEffect<Flashed>(3f);
                Timing.CallDelayed(2.5f,
                    () => ev.Player?.SetRole(CRoleTypeId.FifthistMarionette, RoleSpawnFlags.AssignInventory));
                break;
            default:
                _readCount[ev.Player.Id] = 0;
                return;
        }

        if (count >= 4)
            _readCount[ev.Player.Id] = 0;
        else
            _readCount[ev.Player.Id] = (byte)(count + 1);

        var player = ev.Player;
        Timing.CallDelayed(0.1f, () => Give(player));
    }
}
