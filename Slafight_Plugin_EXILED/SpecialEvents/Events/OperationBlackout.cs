using System;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class OperationBlackout : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.OperationBlackout;
    public override int MinPlayersRequired => 0;
    public override string LocalizedName => "Operation Blackout";
    public override string TriggerRequirement => "無し";
    // ===== 実行エントリポイント & Register & Unregister =====
    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
    public override bool IsReadyToExecute()
    {
        return false;
    }

    protected override void OnExecute(int eventPID)
    {
        if (CancelIfOutdated())
            return;

        
    }
}