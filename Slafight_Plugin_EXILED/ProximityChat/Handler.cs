using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerRoles;
using PlayerRoles.Spectating;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using VoiceChat;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using SpectatorRole = PlayerRoles.Spectating.SpectatorRole;

namespace Slafight_Plugin_EXILED.ProximityChat;

public static class Handler
{
    // ====== 設定・状態 ======

    public static readonly List<Player> ActivatedPlayers = [];
    public static readonly List<Player> CanUsePlayers = [];
    private static readonly HashSet<int> ForcedCanUsePlayers = [];

    public static float AudioMaxDistance = 20f;
    public static float AudioMinDistance = 7.5f;
    public static float AudioVolume => Mathf.Max(0f, Plugin.Singleton?.Config?.ProximityChatVolume ?? 1f);
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
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarted;
        Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        VoiceRoutingApi.Register(new VoiceRouteRule(
            "proximity.scp-chat-mirror",
            EvaluateProximityRoute,
            priority: 100));
    }

    public static void UnregisterEvents()
    {
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarted;
        Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        VoiceRoutingApi.Unregister("proximity.scp-chat-mirror");
        ClearState();
    }

    // ====== ロール・ラウンド変化 ======

    private static void OnRoundRestarted()
        => ClearState();

    private static void ClearState()
    {
        ActivatedPlayers.Clear();
        CanUsePlayers.Clear();
        ForcedCanUsePlayers.Clear();
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
        => RemovePlayer(ev.Player);

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        var player = ev.Player;
        if (player is null)
            return;
        if (!ev.IsAllowed)
            return;

        var playerId = player.Id;
        // 役職が確定してから判定したいので少しディレイ
        Timing.CallDelayed(1.1f, () =>
        {
            player = GetUsablePlayer(playerId);
            if (player == null)
                return;

            RemovePlayer(player, removeForced: false);

            if (CanPlayerUseProximityChat(player))
            {
                AddPlayer(CanUsePlayers, player);

                if (IsProximityChatForced(player) || ShouldEnableProximityChatByDefault(player))
                    AddPlayer(ActivatedPlayers, player);
            }

            if (!ContainsPlayer(CanUsePlayers, player))
                return;

            var listText = string.Join(", ", CanUsePlayers.Where(IsValid).Select(p => $"{p.Nickname}({p.Id})"));
            Log.Debug($"[ProximityChat] CanUsePlayers Updated. List: {listText}");

            if (!CanReceiveHint(player))
                return;

            var keybindHint = BuildAvailableHintContent(player);
            var hint = new Hint
            {
                Alignment = HintAlignment.Center,
                XCoordinate = 0,
                YCoordinate = 865,
                Text = keybindHint.Text,
                Parameters = keybindHint.Parameters,
                Id = HudConstId.ProximityChat
            };

            player.AddHint(hint);
            Timing.CallDelayed(5f, () => GetUsablePlayer(playerId)?.RemoveHint(hint));
        });
    }

    // ====== 使用可否・強制フラグ ======

    public static bool CanPlayerUseProximityChat(Player player)
    {
        if (!IsValid(player))
            return false;

        if (ForcedCanUsePlayers.Contains(player.Id))
            return true;

        if (!string.IsNullOrEmpty(player.UniqueRole))
            return CRole.TryGetByUniqueRole(player.UniqueRole, out var role) &&
                   role.Voice.Proximity.IsAvailable;

        return AllowedRoleTypes.Contains(player.Role);
    }

    public static bool ShouldEnableProximityChatByDefault(Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.UniqueRole))
            return false;

        return CRole.TryGetByUniqueRole(player.UniqueRole, out var role) &&
               role.Voice.Proximity.EnabledByDefault;
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

            AddPlayer(CanUsePlayers, player);

            if (activate)
                AddPlayer(ActivatedPlayers, player);

            return;
        }

        ForcedCanUsePlayers.Remove(player.Id);

        if (!CanPlayerUseProximityChat(player))
        {
            RemovePlayer(player, removeForced: false);
        }
    }

    // ====== 音声イベント ======

    private static VoiceRouteDecision? EvaluateProximityRoute(VoiceRouteContext context)
    {
        if (!IsValid(context.Sender) || !IsValid(context.Receiver))
            return null;

        PrunePlayerLists();

        // SCP チャットだけを Proximity にミラー
        if (context.SourceChannel != VoiceChatChannel.ScpChat)
            return null;

        if (!CanPlayerUseProximityChat(context.Sender))
            return null;

        if (!ContainsPlayer(ActivatedPlayers, context.Sender))
            return null;

        if (!CanReceiveProximity(context.Sender, context.Receiver, AudioMaxDistance))
            return null;

        return VoiceRouteDecision.Spatial(
            PlayerSpeakerManager.PurposeProximity,
            AudioMaxDistance,
            AudioMinDistance,
            AudioVolume,
            suppressNative: false);
    }

    // ====== 近接送信処理 ======

    private static bool CanReceiveProximity(Player speakerPlayer, Player receiver, float maxRange)
    {
        var hub = receiver.ReferenceHub;
        if (hub.roleManager.CurrentRole is SpectatorRole)
        {
            // 観戦者は「見ている対象」だけ聞ける
            return speakerPlayer.ReferenceHub.IsSpectatedBy(hub);
        }

        return Vector3.Distance(speakerPlayer.Position, hub.transform.position) < maxRange;
    }

    private static void AddPlayer(List<Player> players, Player player)
    {
        if (!IsValid(player))
            return;

        players.RemoveAll(p => !IsValid(p) || p.Id == player.Id);
        players.Add(player);
    }

    private static bool ContainsPlayer(List<Player> players, Player player)
        => IsValid(player) && players.Any(p => IsValid(p) && p.Id == player.Id);

    private static void RemovePlayer(Player player, bool removeForced = true)
    {
        if (player == null)
            return;

        ActivatedPlayers.RemoveAll(p => !IsValid(p) || p.Id == player.Id);
        CanUsePlayers.RemoveAll(p => !IsValid(p) || p.Id == player.Id);
        if (removeForced)
            ForcedCanUsePlayers.Remove(player.Id);
    }

    private static void PrunePlayerLists()
    {
        ActivatedPlayers.RemoveAll(p => !IsValid(p));
        CanUsePlayers.RemoveAll(p => !IsValid(p));
    }

    private static Player GetUsablePlayer(int playerId)
    {
        var player = Player.List.FirstOrDefault(p => p != null && p.Id == playerId);
        return IsValid(player) ? player : null;
    }

    private static bool IsValid(Player player)
    {
        try
        {
            return player != null
                   && player.ReferenceHub != null
                   && player.IsNotHost();
        }
        catch
        {
            return false;
        }
    }

    private static bool CanReceiveHint(Player player)
        => player != null && player.IsConnected && !player.IsNPC;

    private static ServerSpecificUserSettings.KeybindHintContent BuildAvailableHintContent(Player player)
    {
        var keybindHint = ServerSpecificUserSettings.BuildKeybindUsageHint(
            player,
            ServerSpecifics.ProximityChatKeybindSettingId,
            "近接チャットをON/OFFできます");

        return new ServerSpecificUserSettings.KeybindHintContent(
            "<color=yellow><size=24>近接チャット機能が利用可能です！</size></color>\n" + keybindHint.Text,
            keybindHint.Parameters);
    }
}
