using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerRoles;
using PlayerRoles.Spectating;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using VoiceChat;
using VoiceChat.Networking;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using SpectatorRole = PlayerRoles.Spectating.SpectatorRole;

namespace Slafight_Plugin_EXILED.ProximityChat;

public static class Handler
{
    public static void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.VoiceChatting += OnPlayerUsingVoiceChat;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarted;
        
        Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
    }
    
    public static void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.VoiceChatting -= OnPlayerUsingVoiceChat;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarted;

        Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
    }
    public static readonly List<Player> ActivatedPlayers = [];
    public static readonly List<Player> CanUsePlayers = [];
    private static readonly HashSet<int> ForcedCanUsePlayers = [];

    public static readonly List<RoleTypeId> AllowedRoleTypes =
    [
        RoleTypeId.Scp049,
        RoleTypeId.Scp939,
        RoleTypeId.Scp3114
    ];

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        Timing.CallDelayed(1.1f, () =>
        {
            ActivatedPlayers.Remove(ev.Player);
            CanUsePlayers.Remove(ev.Player);
            if (CanPlayerUseProximityChat(ev.Player))
            {
                CanUsePlayers.Add(ev.Player);

                if (IsProximityChatForced(ev.Player) || ShouldEnableProximityChatByDefault(ev.Player))
                    ActivatedPlayers.Add(ev.Player);
            }

            if (CanUsePlayers.Contains(ev.Player))
            {
                var listText = string.Join(", ", CanUsePlayers.ConvertAll(p => $"{p.Nickname}({p.Id})"));
                Log.Debug($"CanUsePlayers Updated. List: {listText}");
                var hint = new Hint()
                {
                    Alignment = HintAlignment.Center, XCoordinate = 0, YCoordinate = 865,
                    Text = "<color=yellow><size=24>近接チャット機能が利用可能です！</size></color>", Id = "ProximityHint"
                };
                ev.Player?.AddHint(hint);
                Timing.CallDelayed(5f, () => ev.Player?.RemoveHint(hint));
            }
        });
    }
    
    private static void OnRoundRestarted()
    {
        ActivatedPlayers.Clear();
        CanUsePlayers.Clear();
        ForcedCanUsePlayers.Clear();
    }

    public static bool CanPlayerUseProximityChat(Player player)
    {
        if (player == null)
            return false;

        if (ForcedCanUsePlayers.Contains(player.Id))
            return true;

        if (!string.IsNullOrEmpty(player.UniqueRole))
            return CRole.TryGetByUniqueRole(player.UniqueRole, out var role) && role.CanUseProximityChat;

        return AllowedRoleTypes.Contains(player.Role);
    }

    public static bool ShouldEnableProximityChatByDefault(Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.UniqueRole))
            return false;

        return CRole.TryGetByUniqueRole(player.UniqueRole, out var role) && role.ProximityChatEnabledByDefault;
    }

    public static bool IsProximityChatForced(Player player)
        => player != null && ForcedCanUsePlayers.Contains(player.Id);

    public static void SetProximityChatForced(Player player, bool forced, bool activate = true)
    {
        if (player == null)
            return;

        if (forced)
        {
            ForcedCanUsePlayers.Add(player.Id);
            if (!CanUsePlayers.Contains(player))
                CanUsePlayers.Add(player);

            if (activate && !ActivatedPlayers.Contains(player))
                ActivatedPlayers.Add(player);

            return;
        }

        ForcedCanUsePlayers.Remove(player.Id);
        if (!CanPlayerUseProximityChat(player))
        {
            CanUsePlayers.Remove(player);
            ActivatedPlayers.Remove(player);
        }
    }

    public static void OnPlayerUsingVoiceChat(VoiceChattingEventArgs args)
    {
        if (args.VoiceMessage.Channel == VoiceChatChannel.Proximity)
            return;

        if (args.VoiceMessage.Channel != VoiceChatChannel.ScpChat)
            return;

        Log.Debug($"[ProximityChat] OnPlayerUsingVoiceChat: Captured ScpChat voice from {args.Player.Nickname}");

        if (!CanPlayerUseProximityChat(args.Player))
        {
            Log.Debug($"[ProximityChat] OnPlayerUsingVoiceChat: {args.Player.Nickname} cannot use proximity chat (Role: {args.Player.Role})");
            return;
        }

        if (!ActivatedPlayers.Contains(args.Player))
        {
            Log.Debug($"[ProximityChat] OnPlayerUsingVoiceChat: {args.Player.Nickname} has proximity chat toggled OFF");
            return;
        }

        Log.Debug($"[ProximityChat] OnPlayerUsingVoiceChat: Processing ProximityChat for {args.Player.Nickname} (maxRange: 20f)");
        SendProximityMessage(args.Player, args.VoiceMessage, 20f);
    }

    private static void SendProximityMessage(Player speakerPlayer, VoiceMessage msg, float maxRange = 20f)
    {
        var targets = new List<ReferenceHub>();
        var speakerPosition = speakerPlayer.Position;

        foreach (var referenceHub in ReferenceHub.AllHubs)
        {
            if (referenceHub == null || referenceHub.connectionToClient == null || referenceHub == speakerPlayer.ReferenceHub)
                continue;

            if (referenceHub.roleManager.CurrentRole is SpectatorRole spectator)
            {
                if (!speakerPlayer.ReferenceHub.IsSpectatedBy(referenceHub))
                    continue;
            }
            else
            {
                float dist = Vector3.Distance(speakerPosition, referenceHub.transform.position);
                if (dist >= maxRange)
                {
                    Log.Debug($"[ProximityChat] SendProximityMessage: Excluding {referenceHub.nicknameSync} (distance: {dist:F2}m >= {maxRange}m)");
                    continue;
                }
                Log.Debug($"[ProximityChat] SendProximityMessage: Including {referenceHub.nicknameSync} (distance: {dist:F2}m < {maxRange}m)");
            }

            targets.Add(referenceHub);
        }

        if (targets.Count == 0)
        {
            Log.Debug($"[ProximityChat] SendProximityMessage: No valid targets in range for {speakerPlayer.Nickname}");
            return;
        }

        if (PlayerSpeakerManager.TryGetSpeaker(speakerPlayer, out var liveSpeaker))
        {
            int sent = liveSpeaker.SendFrame(msg.Data, msg.DataLength, targets);
            Log.Debug($"[ProximityChat] SendProximityMessage: Sent audio frame of size {msg.DataLength} bytes from {speakerPlayer.Nickname} (ControllerId: {liveSpeaker.ControllerId}) to {sent}/{targets.Count} targets.");
        }
        else
        {
            Log.Warn($"[ProximityChat] SendProximityMessage: Failed to get/find speaker for {speakerPlayer.Nickname}!");
        }
    }
}
