using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using VoiceChat;

namespace Slafight_Plugin_EXILED.CustomRoles.FoundationForces;

public class AraOrun : CRole
{
    protected override string RoleName { get; set; } = "アラ・オルン";

    protected override string Description { get; set; } = "貴方はミームで構成された機動部隊、アラ・オルンだ。\n" +
                                                          "下層を目指す反ミーム部門職員を<color=cyan>サポート</color>し\n" +
                                                          "SCP-3125とその傀儡を<color=red>食い止めよ</color>！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.AraOrun;
    protected override CTeam Team { get; set; } = CTeam.FoundationForces;
    protected override string UniqueRoleKey { get; set; } = "AraOrun";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp079;
    protected override float? SpawnMaxHealth => 100f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "Ara Orun";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(RoleSpawnTimings.Scp079Setup, () =>
        {
            if (player.Role is Scp079Role scp079Role)
            {
                scp079Role.Level = 5;
                scp079Role.MaxEnergy = 200f;
                scp079Role.Energy = 200f;
            }
        });
    }

    protected override void OnRoleReceivingVoiceMessage(ReceivingVoiceMessageEventArgs ev)
    {
        if (ev.VoiceMessage.Channel == VoiceChatChannel.ScpChat)
        {
            ev.IsAllowed = false;
        }
    }

    protected override void OnRoleVoiceChatting(VoiceChattingEventArgs ev)
    {
        if (ev.VoiceMessage.Channel == VoiceChatChannel.ScpChat)
        {
            ev.IsAllowed = false;
        }
    }
}
