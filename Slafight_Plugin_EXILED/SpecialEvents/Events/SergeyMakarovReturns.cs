using System.Linq;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;
// SpecialEvent 基底クラス
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class SergeyMakarovReturns : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.SergeyMakarovReturns;
    public override int MinPlayersRequired => 8;
    public override string LocalizedName => "-=[管理官の帰還]=-";
    public override string TriggerRequirement => "April Fools";

    // ===== ショートカット =====
    // ===== 実行本体 =====
    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() == SeasonTypeId.FifthFestival;
    }

    protected override void OnExecute(int eventPid)
    {
        Round.IsLocked = true;
        CoroutineHandle handle = Timing.CallDelayed(0.5f, () => {
            // FacilityManagerを探す（安全ループ）
            Player targetPlayer = null;
            foreach (var player in Player.List)
            {
                if (player?.Role == null) continue;  // Role nullチェック追加
                if (player.GetCustomRole() == CRoleTypeId.FacilityManager)
                {
                    targetPlayer = player;
                    break;
                }
            }

            // SergeyMarkov設定（安全に）
            if (targetPlayer != null)
            {
                targetPlayer.SetRole(CRoleTypeId.SergeyMakarov);
            }
            else
            {
                var alivePlayer = Player.List.FirstOrDefault(p => p?.Role != null && p.IsNotHost() && p.Role != RoleTypeId.Spectator);
                alivePlayer?.SetRole(CRoleTypeId.SergeyMakarov);
            }

            // Spectator変更（安全ループ + 遅延）
            Timing.CallDelayed(1f, () => {
                Round.IsLocked = true;
                foreach (var player in Player.List.ToList())  // ToList()でスナップショット
                {
                    if (player?.Role == null || player.IsSergeyMarkov()) continue;
                    player.SetRole(RoleTypeId.Spectator);
                }
                Timing.CallDelayed(0.5f, () =>
                {
                    SpawnSystem.ForceSpawnNow(SpawnTypeId.GoiChaosNormal);
                    Round.IsLocked = false;
                });
            });
        });
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}