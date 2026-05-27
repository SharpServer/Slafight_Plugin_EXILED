using System;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features; // SpecialEvent 基底
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class FifthistsRaidEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.FifthistsRaid;
    public override int MinPlayersRequired => 4;
    public override string LocalizedName => "Fifthists Raid";
    public override string TriggerRequirement => "4人以上のプレイヤー";

    // ===== ショートカット =====
    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        // PID セット済みなので CancelIfOutdated() だけ見ればよい
        if (CancelIfOutdated())
            return;

        SpecialEventsHandler.Instance.IsFifthistsRaidActive = true;

        // まず非 SCP から 1/4 を FifthistRescure に
        var convertedCount = 0;
        var targetCount = (int)Math.Truncate(Player.List.Count / 4f);

        Timing.CallDelayed(0.8f, () =>
        {
            foreach (var player in Player.List)
            {
                if (CancelIfOutdated())
                    return;

                if (player.GetTeam() == CTeam.SCPs)
                    continue;

                player.SetRole(CRoleTypeId.FifthistRescure);
                convertedCount++;

                if (convertedCount >= targetCount)
                    break;
            }

            // SCP チームから SCP-3005 を 1 人だけ選ぶ（既に 3005 が居たらそれを優先）
            var existing3005 = Player.List.FirstOrDefault(player => player.GetCustomRole() == CRoleTypeId.Scp3005);

            if (existing3005 != null) return;
            {
                foreach (var player in Player.List)
                {
                    if (CancelIfOutdated())
                        return;

                    if (player.GetTeam() != CTeam.SCPs) continue;
                    player.SetRole(CRoleTypeId.Scp3005);
                    break;
                }
            }
        });

        // 8 秒後に Cassie + BGM
        Timing.CallDelayed(8f, () =>
        {
            if (CancelIfOutdated())
                return;

            // BGM 再生
            SpeakerApi.Play("_w_fifthists.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999f, 0f);

            // Cassie アナウンス（元コードのヘルパー）
            CassieHelper.AnnounceFifthist(convertedCount);
        });
    }

    // 追加サブスク不要
    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}