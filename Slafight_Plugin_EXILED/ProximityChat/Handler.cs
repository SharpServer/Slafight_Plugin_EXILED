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
using VoiceChat.Networking;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using SpectatorRole = PlayerRoles.Spectating.SpectatorRole;

namespace Slafight_Plugin_EXILED.ProximityChat;

public static class Handler
{
    // ====== 設定・状態 ======

    public static readonly List<Player> ActivatedPlayers = new();
    public static readonly List<Player> CanUsePlayers = new();
    private static readonly HashSet<int> ForcedCanUsePlayers = new();

    public static float AudioMaxDistance = 20f;
    public static float AudioMinDistance = 7.5f;
    public static bool AudioIsSpatial = true;

    public static readonly List<RoleTypeId> AllowedRoleTypes =
    [
        RoleTypeId.Scp049,
        RoleTypeId.Scp939,
        RoleTypeId.Scp3114
    ];

    // ====== イベント登録 ======

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

    // ====== ロール・ラウンド変化 ======

    private static void OnRoundRestarted()
    {
        ActivatedPlayers.Clear();
        CanUsePlayers.Clear();
        ForcedCanUsePlayers.Clear();
    }

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        var player = ev.Player;
        if (player is null)
            return;

        // 役職が確定してから判定したいので少しディレイ
        Timing.CallDelayed(1.1f, () =>
        {
            ActivatedPlayers.Remove(player);
            CanUsePlayers.Remove(player);

            if (CanPlayerUseProximityChat(player))
            {
                if (!CanUsePlayers.Contains(player))
                    CanUsePlayers.Add(player);

                if (IsProximityChatForced(player) || ShouldEnableProximityChatByDefault(player))
                    if (!ActivatedPlayers.Contains(player))
                        ActivatedPlayers.Add(player);
            }

            if (!CanUsePlayers.Contains(player))
                return;

            var listText = string.Join(", ", CanUsePlayers.ConvertAll(p => $"{p.Nickname}({p.Id})"));
            Log.Debug($"[ProximityChat] CanUsePlayers Updated. List: {listText}");

            var hint = new Hint
            {
                Alignment = HintAlignment.Center,
                XCoordinate = 0,
                YCoordinate = 865,
                Text = "<color=yellow><size=24>近接チャット機能が利用可能です！</size></color>",
                Id = HudConstId.ProximityChat
            };

            player.AddHint(hint);
            Timing.CallDelayed(5f, () => player?.RemoveHint(hint));
        });
    }

    // ====== 使用可否・強制フラグ ======

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

    // ====== 音声イベント ======

    public static void OnPlayerUsingVoiceChat(VoiceChattingEventArgs args)
    {
        if (args.Player is null)
            return;

        // Proximity チャンネルで送っている時はスルー
        if (args.VoiceMessage.Channel == VoiceChatChannel.Proximity)
            return;

        // SCP チャットだけを Proximity にミラー
        if (args.VoiceMessage.Channel != VoiceChatChannel.ScpChat)
            return;

        if (!CanPlayerUseProximityChat(args.Player))
            return;

        if (!ActivatedPlayers.Contains(args.Player))
            return;

        SendProximityMessage(args.Player, args.VoiceMessage, AudioMaxDistance);
    }

    // ====== 近接送信処理 ======

    private static void SendProximityMessage(Player speakerPlayer, VoiceMessage msg, float maxRange)
    {
        if (speakerPlayer == null)
            return;

        var targets = new List<ReferenceHub>();
        var speakerPosition = speakerPlayer.Position;

        foreach (var hub in ReferenceHub.AllHubs)
        {
            if (hub == null || hub.connectionToClient == null)
                continue;

            // 観戦者は「見ている対象」だけ聞ける
            if (hub.roleManager.CurrentRole is SpectatorRole)
            {
                if (!speakerPlayer.ReferenceHub.IsSpectatedBy(hub))
                    continue;
            }
            else
            {
                var dist = Vector3.Distance(speakerPosition, hub.transform.position);
                if (dist >= maxRange)
                    continue;
            }

            targets.Add(hub);
        }

        if (targets.Count == 0)
            return;

        // 用途キー "proximity" でスピーカーを取得
        if (!PlayerSpeakerManager.TryGetSpeaker(
                speakerPlayer,
                PlayerSpeakerManager.PurposeProximity,
                out var liveSpeaker))
        {
            Log.Warn($"[ProximityChat] SendProximityMessage: No proximity speaker for {speakerPlayer.Nickname}");
            return;
        }

        // 生の Opus フレームをそのまま投げる
        liveSpeaker.SendFrame(msg.Data, msg.DataLength, targets);
    }
}