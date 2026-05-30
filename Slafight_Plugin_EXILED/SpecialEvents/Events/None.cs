using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features; // SpecialEvent 基底クラス

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class NoneEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.None;
    public override int MinPlayersRequired => 0; // 誰もいなくても可なら 0
    public override string LocalizedName => "無し";
    public override string TriggerRequirement => "無し";

    // ===== ショートカット =====
    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        return; // None lol.
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}