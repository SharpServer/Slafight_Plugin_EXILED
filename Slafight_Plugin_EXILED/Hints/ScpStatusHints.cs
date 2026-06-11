using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using AbstractHint = HintServiceMeow.Core.Models.Hints.AbstractHint;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints;

public class ScpStatusHints : IBootstrapHandler
{
    private const string HintId = "ScpStatusHints_Status";
    private const float UpdateInterval = 0.5f;
    private const float GeneratorStartupBlinkSeconds = 3f;
    private const float GeneratorStartupBlinkInterval = 0.8f;

    private static readonly Dictionary<int, AbstractHint> TrackingHints = [];
    private static readonly Vector2 BasePosition = new(0, 150);

    private const float OtherAITypeOffsetX = 370f;
    private const int FontSize = 24;

    private static CoroutineHandle _coroutineHandle;
    private static int _updateVersion;
    private static bool _registered;

    public static void Register()
    {
        Unregister();

        _registered = true;
        _updateVersion++;

        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.Left += OnLeft;
        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;

        if (!Round.IsLobby)
            StartUpdateCoroutine();
    }

    public static void Unregister()
    {
        _registered = false;
        _updateVersion++;

        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.Left -= OnLeft;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;

        Timing.KillCoroutines(_coroutineHandle);
        ClearAll();
    }

    private static void OnRoundStarted()
    {
        if (!_registered)
            return;

        StartUpdateCoroutine();
    }

    private static void StartUpdateCoroutine()
    {
        Timing.KillCoroutines(_coroutineHandle);
        _coroutineHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private static void OnRestartingRound()
    {
        Timing.KillCoroutines(_coroutineHandle);
        ClearAll();
    }

    private static void OnVerified(VerifiedEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        var version = _updateVersion;

        Timing.CallDelayed(0.75f, () =>
        {
            if (IsCurrent(version))
                RefreshAll();
        });
    }

    private static void OnLeft(LeftEventArgs? ev)
    {
        if (ev?.Player == null)
            return;

        RemoveHint(ev.Player);
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player is null)
            return;

        var version = _updateVersion;

        Timing.CallDelayed(0.25f, () =>
        {
            if (IsCurrent(version))
                RefreshAll();
        });

        Timing.CallDelayed(0.75f, () =>
        {
            if (IsCurrent(version))
                RefreshAll();
        });

        Timing.CallDelayed(1.5f, () =>
        {
            if (IsCurrent(version))
                RefreshAll();
        });
    }

    private static IEnumerator<float> UpdateCoroutine()
    {
        while (true)
        {
            if (!_registered)
                yield break;

            if (Round.IsLobby)
            {
                ClearAll();
                yield break;
            }

            RefreshAll();

            yield return Timing.WaitForSeconds(UpdateInterval);
        }
    }

    private static void RefreshAll()
    {
        if (!_registered)
            return;

        var players = Player.List.ToList();
        var scpPlayers = GetScpPlayers(players);
        var hintRecipients = GetScpHintRecipients(scpPlayers);
        var generatorText = BuildGeneratorText();

        foreach (var player in hintRecipients)
            UpdateHint(player, scpPlayers, generatorText);

        var recipientIds = new HashSet<int>(hintRecipients.Select(player => player.Id));

        foreach (var trackedId in TrackingHints.Keys.ToList())
        {
            if (recipientIds.Contains(trackedId))
                continue;

            var player = players.FirstOrDefault(p => p.Id == trackedId);

            if (player != null)
                RemoveHint(player);
            else
            {
                TrackingHints.Remove(trackedId);
            }
        }
    }

    private static void UpdateHint(Player? player)
    {
        UpdateHint(player, GetScpPlayers(), BuildGeneratorText());
    }

    private static void UpdateHint(Player? player, IReadOnlyList<Player> scpPlayers, string generatorText)
    {
        if (!_registered)
            return;

        if (!IsPlayerValid(player))
            return;

        // NPCにはHintを送信しない。
        if (player!.IsNPC)
            return;

        if (player.GetTeam() is not CTeam.SCPs || CRole.IsTeamNpc(player))
        {
            RemoveHint(player);
            return;
        }

        var display = TryGetDisplay(player);
        if (display == null)
            return;

        var hint = EnsureHint(player, display);
        var text = BuildStatusText(player, scpPlayers, generatorText);

        if (hint.Text != text)
            hint.Text = text;
    }

    private static AbstractHint EnsureHint(Player player, PlayerDisplay display)
    {
        var resultX = GetHintX(player);

        // PlayerHUD と同じ管理方式:
        // 既存Hintでも毎回レイアウト情報を再適用する。
        // 座標差分のためにRemove/Addしない。
        if (display.GetHint(HintId) is not Hint hint)
        {
            hint = new Hint()
            {
                Id = HintId,
                Text = string.Empty,
            };

            display.AddHint(hint);
        }

        hint.Alignment = HintAlignment.Right;
        hint.ResolutionBasedAlign = true;
        hint.SyncSpeed = HintSyncSpeed.Fastest;
        hint.XCoordinate = resultX;
        hint.YCoordinate = BasePosition.y;
        hint.FontSize = FontSize;

        TrackingHints[player.Id] = hint;
        return hint;
    }

    private static float GetHintX(Player player)
    {
        var resultX = BasePosition.x;

        // カスタムかどうかではなく、ベースRole.Typeのみで判定する。
        if (player.Role.Type is not RoleTypeId.Scp079)
            resultX += OtherAITypeOffsetX;

        return resultX;
    }

    private static void RemoveHint(Player? player)
    {
        if (player == null)
            return;

        try
        {
            if (TrackingHints.Remove(player.Id, out var trackedHint))
                player.RemoveHint(trackedHint);

            var display = TryGetDisplay(player);
            var displayHint = display?.GetHint(HintId);

            if (displayHint != null)
                player.RemoveHint(displayHint);
        }
        catch (Exception e)
        {
            Log.Debug($"[ScpStatusHints] Failed to remove hint for {player.Nickname}: {e.Message}");
        }
    }

    private static void ClearAll()
    {
        foreach (var player in Player.List.ToList())
            RemoveHint(player);

        TrackingHints.Clear();
    }

    private static bool IsCurrent(int version)
    {
        return _registered && _updateVersion == version;
    }

    private static bool IsPlayerValid(Player? player)
    {
        try
        {
            return player != null && player.IsConnected && player.ReferenceHub != null;
        }
        catch
        {
            return false;
        }
    }

    private static PlayerDisplay? TryGetDisplay(Player player)
    {
        try
        {
            return PlayerDisplay.Get(player.ReferenceHub);
        }
        catch
        {
            return null;
        }
    }

    private static List<Player> GetScpPlayers()
    {
        return GetScpPlayers(Player.List);
    }

    private static List<Player> GetScpPlayers(IEnumerable<Player> players)
    {
        return players
            .Where(player => IsPlayerValid(player) && player.GetTeam() is CTeam.SCPs && !CRole.IsTeamNpc(player))
            .OrderBy(player => player.Id)
            .ToList();
    }

    private static List<Player> GetScpHintRecipients(IEnumerable<Player> scpPlayers)
    {
        return scpPlayers
            .Where(player => !player.IsNPC)
            .ToList();
    }

    private static string BuildStatusText(Player targetPlayer, IReadOnlyList<Player> scpPlayers, string generatorText)
    {
        var sb = new StringBuilder();

        foreach (var player in scpPlayers)
        {
            var customRole = player.GetCustomRole();
            CRole? cRole = null;
            var hasCustomRole = customRole is not CRoleTypeId.None && CRole.TryGet(customRole, out cRole);

            // カスタムかどうかではなく、ベースRole.Typeのみで079扱いする。
            var isScp079 = player.Role.Type is RoleTypeId.Scp079;
            var scp079Role = player.Role as Scp079Role;

            if (hasCustomRole && cRole != null)
            {
                sb.Append($"<color={CTeam.SCPs.GetTeamColor()}>")
                    .Append(cRole.RoleDisplayName.RemoveUnityRichTextTag())
                    .Append("</color> ");
            }
            else
            {
                sb.Append($"<color={CTeam.SCPs.GetTeamColor()}>")
                    .Append(player.Role.Name.RemoveUnityRichTextTag())
                    .Append("</color> ");
            }

            if (isScp079 && scp079Role != null)
            {
                var playerEnergyPercentage = scp079Role.MaxEnergy > 0f ? scp079Role.Energy / scp079Role.MaxEnergy : 0f;
                var energyColor = StaticUtils.ToGradientColor(playerEnergyPercentage).ToHex();

                sb.Append($"[ENERGY: <color={energyColor}>{scp079Role.Energy:F0}</color>/{scp079Role.MaxEnergy:F0}] (LEVEL: {scp079Role.Level})");
            }
            else
            {
                sb.Append("[");

                var playerHealthPercentage = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f;
                var healthColor = StaticUtils.ToGradientColor(playerHealthPercentage).ToHex();

                sb.Append($"<color={healthColor}>")
                    .Append(player.Health.ToString("F0"))
                    .Append("</color>/")
                    .Append(player.MaxHealth.ToString("F0"))
                    .Append(" HP")
                    .Append("] ");

                if (player.MaxHumeShield > 0f)
                {
                    sb.Append("(");

                    var playerHsPercentage = player.HumeShield / player.MaxHumeShield;
                    var hsColor = StaticUtils.ToGradientColor(playerHsPercentage).ToHex();

                    sb.Append($"<color={hsColor}>")
                        .Append(player.HumeShield.ToString("F0"))
                        .Append("</color>/")
                        .Append(player.MaxHumeShield.ToString("F0"))
                        .Append(" HS")
                        .Append(") ");
                }
            }

            if (player != targetPlayer)
            {
                sb.Append("距離: ");

                int distance;

                if (isScp079 && scp079Role != null)
                    distance = (int)Vector3.Distance(targetPlayer.Position, scp079Role.CameraPosition);
                else
                    distance = (int)Vector3.Distance(targetPlayer.Position, player.Position);

                sb.Append($"{distance}m");
            }

            sb.AppendLine();
        }

        sb.Append(generatorText);
        return sb.ToString();
    }

    private static string BuildGeneratorText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("発電機の状態：");

        foreach (var generator in Generator.List)
        {
            if (generator is null)
                continue;

            float progress = generator.ActivationTime > 0f
                ? 1f - generator.CurrentTime / generator.ActivationTime
                : 0f;

            progress = Mathf.Clamp01(progress);

            string color;
            string statusText;
            var startupElapsed = Mathf.Max(0f, generator.ActivationTime - generator.CurrentTime);

            if (generator.IsEngaged || progress >= 1f)
            {
                color = "red";
                statusText = "起動済み";
            }
            else if (generator.IsActivating && startupElapsed <= GeneratorStartupBlinkSeconds)
            {
                color = GetGeneratorStartupBlinkColor(startupElapsed);
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else if (progress == 0f)
            {
                color = "white";
                statusText = "未起動";
            }
            else if (progress < 0.5f)
            {
                color = "yellow";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else if (progress < 0.8f)
            {
                color = "orange";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }
            else
            {
                color = "red";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
            }

            sb.Append("<color=")
                .Append(color)
                .Append("><b>")
                .Append(generator.Room.Type.TranslateRoomName())
                .Append(": </b>")
                .Append(statusText)
                .Append("</color>")
                .AppendLine();
        }

        return sb.ToString();
    }

    private static string GetGeneratorStartupBlinkColor(float startupElapsed)
    {
        var blinkIndex = Mathf.FloorToInt(startupElapsed / GeneratorStartupBlinkInterval);
        return blinkIndex % 2 == 0 ? "red" : "yellow";
    }
}
