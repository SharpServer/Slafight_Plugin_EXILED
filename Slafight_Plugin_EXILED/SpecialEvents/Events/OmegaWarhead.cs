using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features; // SpecialEvent 基底クラス

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class OmegaWarheadEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.OmegaWarhead;
    public override int MinPlayersRequired => 6;
    public override string LocalizedName => "OMEGA WARHEAD";
    public override string TriggerRequirement => "無し";

    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        // ここに来た時点で CurrentEventPid == eventPid がセット済み
        if (CancelIfOutdated())
            return;

        // Warhead 関連フラグ
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);
        RoundHazardController.SetDeadmanSwitchBlocked(true);

        if (CancelIfOutdated())
            return;

        // 30 秒後に ALPHA WARHEAD 通知
        Timing.CallDelayed(60f, () =>
        {
            if (CancelIfOutdated()) return;

            foreach (Player player in Player.List)
            {
                player.MessageTranslated("O5 Command has decided to halt containment breaches using alpha warhead . Continue evacuation .",
                    "",
                    "O5評議会が<color=red>ALPHA WARHEAD</color>を用いた収容違反の一時解決を決定しました。起動までに引き続き非難をしてください。",
                    true,
                    false);
            }

            // さらに 35 秒後、OMEGA WARHEAD ステータス変更＋プロトコル開始
            Timing.CallDelayed(60f, () =>
            {
                if (CancelIfOutdated()) return;

                Exiled.API.Features.Cassie.MessageTranslated(
                    "New Status for Containment Breach by O5 Command : Using OMEGA WARHEAD",
                    "O5による収容違反対応ステータス更新：<color=blue>OMEGA WARHEAD</color>を用いた対応",
                    true,
                    true);

                Exiled.API.Features.Cassie.MessageTranslated(
                    "New Status Accepted .",
                    "新ステータス：承認",
                    false,
                    false);

                // OMEGA プロトコル開始（CurrentEventPid をそのまま渡す）
                OmegaWarhead.StartProtocol(825f);
            });
        });
    }

    // このイベントは追加サブスク不要なので空で OK
    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}
