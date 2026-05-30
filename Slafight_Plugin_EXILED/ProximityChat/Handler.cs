using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerRoles;
using PlayerRoles.Spectating;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using VoiceChat;
using VoiceChat.Codec;
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

    // Audio Settings
    public static float AudioMaxDistance = 20f;
    public static float AudioMinDistance = 7.5f;
    public static bool AudioIsSpatial = true;

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
                    Text = "<color=yellow><size=24>近接チャット機能が利用可能です！</size></color>", Id = HudConstId.ProximityChat
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

        if (!CanPlayerUseProximityChat(args.Player))
            return;

        if (!ActivatedPlayers.Contains(args.Player))
            return;

        SendProximityMessage(args.Player, args.VoiceMessage, AudioMaxDistance);
    }

    private static void SendProximityMessage(Player speakerPlayer, VoiceMessage msg, float maxRange)
    {
        var targets = new List<ReferenceHub>();
        var speakerPosition = speakerPlayer.Position;

        foreach (var referenceHub in ReferenceHub.AllHubs)
        {
            if (referenceHub == null || referenceHub.connectionToClient == null)
                continue;

            if (referenceHub.roleManager.CurrentRole is SpectatorRole)
            {
                if (!speakerPlayer.ReferenceHub.IsSpectatedBy(referenceHub))
                    continue;
            }
            else
            {
                float dist = Vector3.Distance(speakerPosition, referenceHub.transform.position);
                if (dist >= maxRange)
                    continue;
            }

            targets.Add(referenceHub);
        }

        if (targets.Count == 0)
            return;

        if (!PlayerSpeakerManager.TryGetSpeaker(speakerPlayer, out var liveSpeaker))
        {
            Log.Warn($"[ProximityChat] SendProximityMessage: No speaker for {speakerPlayer.Nickname}");
            return;
        }

        // Stream raw Opus frames directly to clients to prevent CPU load and choppiness
        liveSpeaker.SendFrame(msg.Data, msg.DataLength, targets);
    }
}
