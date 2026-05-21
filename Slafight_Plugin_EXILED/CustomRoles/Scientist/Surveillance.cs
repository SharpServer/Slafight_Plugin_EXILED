using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using VoiceChat;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class Surveillance : CRole
{
    protected override string RoleName { get; set; } = "Surveillance";
    protected override string Description { get; set; } = "W.I.P";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Surveillance;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "Surveillance";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp079;
    protected override float? SpawnMaxHealth => 100f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "Surveillance";

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
