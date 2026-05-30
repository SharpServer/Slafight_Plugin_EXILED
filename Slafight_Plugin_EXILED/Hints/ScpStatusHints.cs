#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class ScpStatusHints : IBootstrapHandler, IDisposable
{
    private const string HintId = "ScpStatusHint";
    private static ScpStatusHints? _instance;

    private readonly int _hintY = HintCoordinateConverter.FromRueiY(900);
    private CoroutineHandle _loop;
    private bool _disposed;

    public static void Register()
    {
        Unregister();
        _instance = new ScpStatusHints();
    }

    public static void Unregister()
    {
        _instance?.Dispose();
        _instance = null;
    }

    private ScpStatusHints()
    {
        Exiled.Events.Handlers.Player.Verified += OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
        Exiled.Events.Handlers.Server.RestartingRound += ClearAll;
        _loop = Timing.RunCoroutine(UpdateLoop());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Player.Verified -= OnVerified;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearAll;

        if (_loop.IsRunning)
            Timing.KillCoroutines(_loop);

        ClearAll();
        GC.SuppressFinalize(this);
    }

    private void OnVerified(Exiled.Events.EventArgs.Player.VerifiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.5f, () => EnsureFor(ev.Player));
    }

    private void OnChangingRole(Exiled.Events.EventArgs.Player.ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.5f, () => EnsureFor(ev.Player));
    }

    private IEnumerator<float> UpdateLoop()
    {
        yield return Timing.WaitForSeconds(0.5f);
        for (;;)
        {
            foreach (var player in Player.List.ToList())
                EnsureFor(player);

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private void EnsureFor(Player player)
    {
        if (!IsPlayerValid(player))
            return;

        var display = TryGetDisplay(player);
        if (display == null)
            return;

        if (!ShouldShow(player))
        {
            SetText(display, string.Empty);
            return;
        }

        EnsureHint(display);
        SetText(display, BuildContent(player));
    }

    private void ClearAll()
    {
        foreach (var player in Player.List.ToList())
        {
            if (!IsPlayerValid(player))
                continue;

            var display = TryGetDisplay(player);
            if (display != null)
                SetText(display, string.Empty);
        }
    }

    private void EnsureHint(PlayerDisplay display)
    {
        if (display.GetHint(HintId) != null)
            return;

        display.AddHint(new Hint
        {
            Id = HintId,
            Text = string.Empty,
            Alignment = HintAlignment.Right,
            SyncSpeed = HintSyncSpeed.Fast,
            FontSize = 24,
            XCoordinate = 350,
            YCoordinate = _hintY,
        });
    }

    private static void SetText(PlayerDisplay display, string text)
    {
        var hint = display.GetHint(HintId);
        if (hint != null)
            hint.Text = text;
    }

    private static string BuildContent(Player screenPlayer)
    {
        var sb = new StringBuilder();
        sb.Append("<align=right><size=30>");

        var scpPlayers = Player.List
            .Where(player => IsScpTeam(player) && player.IsAlive)
            .ToList();

        foreach (var player in scpPlayers)
        {
            if (player.Role.Type == RoleTypeId.Scp0492)
                continue;

            AppendScpLine(sb, screenPlayer, player);
        }

        int zombies = scpPlayers.Count(player => player.Role.Type == RoleTypeId.Scp0492);
        if (zombies > 0)
            sb.Append("<b><color=#ff3232>SCP049-2</color></b>の数: ").Append(zombies).AppendLine();

        sb.AppendLine("発電機の状態");
        foreach (var generator in Generator.List)
            AppendGeneratorLine(sb, generator);

        sb.Append("</size></align>");
        return sb.ToString();
    }

    private static void AppendScpLine(StringBuilder sb, Player screenPlayer, Player player)
    {
        if (player.Role.Type == RoleTypeId.Scp079)
        {
            var scp079Role = player.Role as Exiled.API.Features.Roles.Scp079Role;
            if (scp079Role == null)
                return;

            sb.Append("<b><color=#ff3232>SCP-079 : </color></b>")
                .Append("レベル: ")
                .Append(scp079Role.Level)
                .Append("(電力: ")
                .Append((int)scp079Role.Energy)
                .Append('/')
                .Append((int)scp079Role.MaxEnergy)
                .Append(')');

            if (screenPlayer.Role.Type != RoleTypeId.Scp079 && scp079Role.Camera != null)
            {
                float distance = Vector3.Distance(screenPlayer.Position, scp079Role.Camera.Position);
                sb.Append("[距離: ").Append((int)distance).Append("m]");
            }

            sb.AppendLine();
            return;
        }

        string scpName = GetScpName(player);
        int health = Mathf.CeilToInt(player.Health);
        int maxHealth = Mathf.Max(1, Mathf.CeilToInt(player.MaxHealth));
        int humeShield = Mathf.CeilToInt(player.HumeShield);
        int maxHumeShield = Mathf.Max(0, Mathf.CeilToInt(player.CustomHumeShieldStat.MaxValue));

        string healthColor = ToGradientHex((float)health / maxHealth, redToGreen: true);
        string humeColor = ToGradientHex(maxHumeShield <= 0 ? 0f : (float)humeShield / maxHumeShield, redToGreen: false);

        sb.Append("<b><color=#ff3232>")
            .Append(scpName)
            .Append(" : </color></b><color=")
            .Append(healthColor)
            .Append('>')
            .Append(health)
            .Append('/')
            .Append(maxHealth)
            .Append(" HP</color>");

        if (maxHumeShield > 0)
        {
            sb.Append(" (<color=")
                .Append(humeColor)
                .Append('>')
                .Append(humeShield)
                .Append('/')
                .Append(maxHumeShield)
                .Append(" HS</color>)");
        }

        AppendDistance(sb, screenPlayer, player);
        sb.AppendLine();
    }

    private static void AppendGeneratorLine(StringBuilder sb, Generator generator)
    {
        if (generator.Room == null || generator.ActivationTime <= 0f)
            return;

        float progress = 1f - generator.CurrentTime / generator.ActivationTime;
        progress = Mathf.Clamp01(progress);

        string color;
        string statusText;

        if (progress == 0f)
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
        else if (progress >= 1f)
        {
            color = "red";
            statusText = "起動済み";
        }
        else
        {
            color = "red";
            statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:F0}秒)";
        }

        sb.Append("<color=")
            .Append(color)
            .Append("><b>")
            .Append(RoomNameTranslator.TranslateRoomName(generator.Room.Type))
            .Append(": </b>")
            .Append(statusText)
            .Append("</color>")
            .AppendLine();
    }

    private static void AppendDistance(StringBuilder sb, Player screenPlayer, Player target)
    {
        if (screenPlayer == target)
            return;

        if (screenPlayer.Role.Type == RoleTypeId.Scp079)
        {
            var scp079Role = screenPlayer.Role as Exiled.API.Features.Roles.Scp079Role;
            if (scp079Role?.Camera == null)
                return;

            float cameraDistance = Vector3.Distance(target.Position, scp079Role.Camera.Position);
            sb.Append("[距離:").Append((int)cameraDistance).Append("m]");
            return;
        }

        float distance = Vector3.Distance(target.Position, screenPlayer.Position);
        sb.Append("[距離:").Append((int)distance).Append("m]");
    }

    private static string GetScpName(Player player)
    {
        if (!string.IsNullOrWhiteSpace(player.CustomInfo))
            return player.CustomInfo;

        return player.Role.Type switch
        {
            RoleTypeId.Scp173 => "SCP-173",
            RoleTypeId.Scp106 => "SCP-106",
            RoleTypeId.Scp096 => "SCP-096",
            RoleTypeId.Scp049 => "SCP-049",
            RoleTypeId.Scp939 => "SCP-939",
            RoleTypeId.Scp3114 => "SCP-3114",
            _ => player.Role.Name ?? "???",
        };
    }

    private static string ToGradientHex(float value, bool redToGreen)
    {
        value = Mathf.Clamp01(value);
        int red = redToGreen ? Mathf.RoundToInt(255 * (1f - value)) : 0;
        int green = redToGreen ? Mathf.RoundToInt(255 * value) : Mathf.RoundToInt(255 * value);
        int blue = redToGreen ? 0 : Mathf.RoundToInt(255 * (1f - value));
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static bool ShouldShow(Player player)
    {
        return IsScpTeam(player);
    }

    private static bool IsScpTeam(Player player)
    {
        return player.GetTeam() == CTeam.SCPs || player.Role.Team == Team.SCPs;
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
}
