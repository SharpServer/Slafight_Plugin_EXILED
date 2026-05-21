using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp079Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-079";
    protected override string Description { get; set; } = "施設制御システムを操り、施設に混沌を引き起こす。\n" +
                                                          "巧みに立ち回り自身の目的を達成せよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp079;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp079";

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Map.GeneratorActivating += OnGenerated;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Map.GeneratorActivating -= OnGenerated;
        base.UnregisterEvents();
    }
    
    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scp079);
        player.UniqueRole = UniqueRoleKey;
        //player.MaxHealth = 100f;
        //player.Health = player.MaxHealth;
        player.ClearInventory();
        player.SetCustomInfo("SCP-079");
        MapFlags.IsOverrideActivated = false;

        if (player.Role is Exiled.API.Features.Roles.Scp079Role role)
        {
            role.Level = 5;
            role.MaxEnergy = 1000f;
            role.Scp2176LostTime = 5f;
        }
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        if (MapFlags.IsOverrideActivated && ev.DamageHandler.Type is DamageType.Warhead)
        {
            ev.IsAllowed = false;
            return;
        }
        if (ev.Attacker is not null)
            CassieHelper.AnnounceTermination(ev, "SCP 0 7 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    private static void OnGenerated(GeneratorActivatingEventArgs ev)
    {
        Timing.CallDelayed(1f, () =>
        {
            if (Generator.List.Where(g => !g.IsEngaged).ToList().Count is 0)
            {
                var list = Player.List.Where(p => p.GetCustomRole() is CRoleTypeId.Scp079).ToList();
                foreach (var player in list)
                {
                    player.ShowHint("<size=23><color=red>!!!!!発電機が全て起動されました!!!!!\n最終手段を確立しています・・・</color></size>");
                }

                Timing.CallDelayed(60f, () =>
                {
                    foreach (var player in list)
                    {
                        if (player is null || Round.IsLobby || player.GetCustomRole() is not CRoleTypeId.Scp079 ||
                            Generator.List.Where(g => !g.IsEngaged).ToList().Count is not 0) return;
                        player.AddAbility<AlphaWarheadOverride>();
                        player.ShowHint(
                            "<color=red><b>ALPHA WARHEAD OVERRIDEが使用可能になりました！</b></color>\nアビリティ使用キーを押して施設を破壊しましょう！");
                    }
                });
            }
        });
    }

}
