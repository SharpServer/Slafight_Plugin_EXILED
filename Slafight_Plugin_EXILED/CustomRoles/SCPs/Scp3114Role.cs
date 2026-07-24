using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp3114;
using Exiled.Events.Handlers;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp3114Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-3114";
    protected override string Description { get; set; } = "人の皮をかぶって擬態し、周りに溶け込む事が出来る骨。\n" +
                                                          "混沌とした施設に絶大な恐怖の一撃を与えよ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp3114;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp3114";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp3114;
    protected override float? SpawnMaxHealth => 3114f;
    protected override bool SpawnClearsInventory => true;
    protected override CRoleVoiceSettings VoiceSettings => CRoleVoiceSettings.WithProximity();

    public override void RegisterEvents()
    {
        Scp3114.Disguised += ExtendTime;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp3114.Disguised -= ExtendTime;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Room SpawnRoom = Room.Get(RoomType.Hcz127);
        Log.Debug(SpawnRoom.Position);
        Vector3 offset = new Vector3(0f,13f,0f);
        if (TrySetPlayerPosition(player, SpawnRoom.Position + SpawnRoom.Rotation * offset, nameof(Scp3114Role)))
            player.Rotation = SpawnRoom.Rotation;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            Ragdoll classd = Ragdoll.CreateAndSpawn(RoleTypeId.ClassD, "D-9341","For You",SpawnRoom.Position + SpawnRoom.Rotation * offset,SpawnRoom.Rotation);
            Ragdoll scientist = Ragdoll.CreateAndSpawn(RoleTypeId.Scientist, "Dr. Maynard","For You",SpawnRoom.Position + SpawnRoom.Rotation * offset,SpawnRoom.Rotation);
        });
    }

    private void ExtendTime(DisguisedEventArgs ev)
    {
        ev.Scp3114.DisguiseDuration = 300f;
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 3 1 1 4", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
