using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp049Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-049";

    protected override string Description { get; set; } = "悪疫を根絶する使命を抱いたペスト医師の見た目のSCP。\n" +
                                                          "名医の感で患者を救い出せ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp049;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp049";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp049;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-049";
    public override bool CanUseProximityChat => true;

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.MaxHealth = 2200f;
        player.Health = player.MaxHealth;
        player.MaxHumeShield = 1200f;
        player.HumeShield = player.MaxHumeShield;
        player.AddAbility<SenseOfGreatDoctor>();
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 0 4 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
