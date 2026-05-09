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

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.ReceivingVoiceMessage += TrashMessages;
        Exiled.Events.Handlers.Player.VoiceChatting += DenySpeakInScpChat;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.ReceivingVoiceMessage -= TrashMessages;
        Exiled.Events.Handlers.Player.VoiceChatting -= DenySpeakInScpChat;
        base.UnregisterEvents();
    }

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scp079);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
            
        player.SetCustomInfo("Surveillance");
    }

    private void TrashMessages(ReceivingVoiceMessageEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.VoiceMessage.Channel == VoiceChatChannel.ScpChat)
        {
            ev.IsAllowed = false;
        }
    }

    private void DenySpeakInScpChat(VoiceChattingEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.VoiceMessage.Channel == VoiceChatChannel.ScpChat)
        {
            ev.IsAllowed = false;
        }
    }
}