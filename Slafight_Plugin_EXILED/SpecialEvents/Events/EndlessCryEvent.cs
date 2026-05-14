using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class EndlessCryEvent : SpecialEvent
{
    // ==== メタ情報 ====
    public override SpecialEventType EventType => SpecialEventType.EndlessCry;

    // 4人以上のプレイヤーで有効
    public override int MinPlayersRequired => 4;

    // ローカライズ情報（SEH から参照される）
    public override string LocalizedName => "ENDLESS CRY";
    public override string TriggerRequirement => "4人以上のプレイヤー";

    // ==== 内部状態 ====
    private int _eventPid;

    // ==== 実行エントリポイント ====
    protected override void OnExecute(int eventPID)
    {
        _eventPid = eventPID;
        Log.Debug($"Scp096CryFuckEvent.Execute called. PID: {_eventPid}");

        if (CancelIfOutdated())
            return;

        // 0.5 秒遅延後に実行（元コード踏襲）
        Timing.CallDelayed(0.5f, () =>
        {
            if (CancelIfOutdated())
                return;

            ForceOneScpTo096Anger();
        });
    }

    // このイベントは追加のサブスク不要なので RegisterEvents / UnregisterEvents は空
    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }

    // ==== 共通キャンセル判定 ====
    private new bool CancelIfOutdated()
    {
        if (_eventPid != SpecialEventsHandler.Instance.EventPID)
            return true;

        return false;
    }

    // ==== メインロジック ====
    private void ForceOneScpTo096Anger()
    {
        foreach (Player player in Player.List)
        {
            if (CancelIfOutdated())
                return;

            if (player.Role.Team != Team.SCPs)
                continue;

            // Cassie メッセージ（元の文字列そのまま）
            Exiled.API.Features.Cassie.MessageTranslated(
                "SCP 0 9 6 . SCP 0 9 6 . .g4 .g3 .g7 .g6 .g2 .g2 .g5",
                "<color=red>SCP-096！SCP-096！うわl...(ノイズ音)</color>",
                true);

            // 096 怒りロールに変化
            player.SetRole(CRoleTypeId.Scp096Anger);
            Log.Debug($"Scp096CryFuckEvent: Set {player.Nickname} to Scp096Anger");

            break;
        }
    }
}
