using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class RevolverBattles : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.RevolverBattles;
    public override int MinPlayersRequired => 0;
    public override string LocalizedName => "Revolver Battles";
    public override string TriggerRequirement => "無し";
    // ===== 実行エントリポイント =====
    public override bool IsReadyToExecute()
    {
        return false; // TEMPORARY TEST
    }

    protected override void OnExecute(int eventPID)
    {
        if (CancelIfOutdated())
            return;

        foreach (var player in Player.List)
        {
            if (player == null) continue;
            if (player.HasItem(ItemType.GunRevolver)) continue;
            player.SetAmmoLimit(AmmoType.Ammo44Cal, 80);
            player.GiveOrDrop(ItemType.GunRevolver);
            player.SetAmmo(AmmoType.Ammo44Cal, 80);
        }
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}