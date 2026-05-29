using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Mirror;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp106Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-106";

    protected override string Description { get; set; } = MapFlags.GetSeason() == SeasonTypeId.April
        ? "若者の叫び声大好き爺。いっぱいPDに送り込もう！\nアビリティで糞まみれの爺街道を創り出せるぞ！\n施設中に糞を垂れ流す奴二号機になりましょう！"
        : "若者の叫び声大好き爺。いっぱいPDに送り込もう！\nアビリティで陥没穴を創り出せるぞ！陥没穴は中に\n人を引き込めるから沢山作れ！";
    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp106;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp106";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp106;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-106";

    public override void RegisterEvents()
    {
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        if (MapFlags.GetSeason() == SeasonTypeId.April)
        {
            player.AddAbility(new DropBiggerShitAbility(player));
        }
        else
        {
            player.AddAbility(new CreateSinkholeAbility(player));
        }
    }

    private void CreateSmallSinkhole(Player player)
    {
        try
        {
            var position = player.Position + new Vector3(0f,-0.5f,0f);
        
            // SinkholeのPrefabIdを指定（実際のIDは要確認）
            var sinkholePrefabId = PrefabType.Sinkhole;
        
            // PrefabHelperでスポーン
            var sinkhole = PrefabHelper.Spawn(sinkholePrefabId, position, Quaternion.identity);
            
            // 必要に応じてNetwork同期
            NetworkServer.Spawn(sinkhole);
        
            // 10秒後に削除
            Timing.CallDelayed(5f, () => UnityEngine.Object.Destroy(sinkhole));
        }
        catch (System.Exception ex)
        {
            Log.Error($"Sinkhole Prefabスポーン失敗: {ex.Message}");
        }
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 1 0 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
