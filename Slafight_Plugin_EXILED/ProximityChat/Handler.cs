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
        DestroyAllProximitySpeakers();
    }
    public static readonly List<Player> ActivatedPlayers = [];
    public static readonly List<Player> CanUsePlayers = [];
    private static readonly HashSet<int> ForcedCanUsePlayers = [];

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        DestroyProximitySpeaker(ev.Player);
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
        DestroyAllProximitySpeakers();
    }

    public static bool CanPlayerUseProximityChat(Player player)
    {
        if (player == null)
            return false;

        if (ForcedCanUsePlayers.Contains(player.Id))
            return true;

        return !string.IsNullOrEmpty(player.UniqueRole)
               && CRole.TryGetByUniqueRole(player.UniqueRole, out var role)
               && role.CanUseProximityChat;
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
            DestroyProximitySpeaker(player);
        }
    }

    public static void OnPlayerUsingVoiceChat(VoiceChattingEventArgs args)
    {
        // 自分が再送した Proximity は一切触らない
        if (args.VoiceMessage.Channel == VoiceChatChannel.Proximity)
            return;

        // SCPチャット以外は触らない
        if (args.VoiceMessage.Channel != VoiceChatChannel.ScpChat)
            return;

        // 対象ロールか？
        if (!CanPlayerUseProximityChat(args.Player))
            return;

        // トグルONにしているか？
        if (!ActivatedPlayers.Contains(args.Player))
            return;

        // ここまで来た人だけ近接に変換
        SendProximityMessage(args.Player, args.VoiceMessage, 5f);
    }

    private static readonly Dictionary<int, SpeakerApi.LivePlayback> ProximitySpeakers = [];

    private static void SendProximityMessage(Player speakerPlayer, VoiceMessage msg, float maxRange = 5f)
    {
        var targets = new List<ReferenceHub>();
        foreach (var referenceHub in ReferenceHub.AllHubs)
        {
            if (referenceHub.roleManager.CurrentRole is SpectatorRole spectator
                && !msg.Speaker.IsSpectatedBy(referenceHub))
                continue;

            if (referenceHub.roleManager.CurrentRole is not PlayerRoles.Voice.IVoiceRole voiceRole2)
                continue;

            if (Vector3.Distance(msg.Speaker.transform.position, referenceHub.transform.position) >= maxRange)
                continue;

            if (voiceRole2.VoiceModule.ValidateReceive(msg.Speaker, VoiceChatChannel.Proximity)
                is VoiceChatChannel.None)
                continue;

            targets.Add(referenceHub);
        }

        if (targets.Count == 0)
            return;

        var liveSpeaker = GetOrCreateProximitySpeaker(speakerPlayer, maxRange);
        liveSpeaker.SetTransform(speakerPlayer.Position);
        liveSpeaker.SendFrame(msg.Data, msg.DataLength, targets);
    }

    private static SpeakerApi.LivePlayback GetOrCreateProximitySpeaker(Player player, float maxRange)
    {
        if (ProximitySpeakers.TryGetValue(player.Id, out var playback) && playback.IsValid)
            return playback;

        playback = SpeakerApi.CreateLiveSpeaker(
            GetAudioPlayerName(player),
            player.Position,
            parent: null,
            speakerName: "Voice",
            isSpatial: true,
            maxDistance: maxRange,
            minDistance: 0f);

        ProximitySpeakers[player.Id] = playback;
        return playback;
    }

    public static void DestroyProximitySpeaker(Player player)
    {
        if (player == null)
            return;

        if (!ProximitySpeakers.TryGetValue(player.Id, out var playback))
            return;

        playback.DestroyAudioPlayer();
        ProximitySpeakers.Remove(player.Id);
    }

    private static void DestroyAllProximitySpeakers()
    {
        foreach (var playback in ProximitySpeakers.Values)
            playback.DestroyAudioPlayer();

        ProximitySpeakers.Clear();
    }

    private static string GetAudioPlayerName(Player player)
        => $"ProximityChat_{player.Id}";

}
