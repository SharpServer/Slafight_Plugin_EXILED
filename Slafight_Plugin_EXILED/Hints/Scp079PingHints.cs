#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Scp079;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class Scp079PingHints : IBootstrapHandler, IDisposable
{
    private const string HintId = "Scp079PingHint";
    private const float DisplaySeconds = 5f;
    private static Scp079PingHints? _instance;

    private readonly Dictionary<int, int> _versions = new();
    private readonly int _hintY = HintCoordinateConverter.FromRueiY(200);
    private bool _disposed;

    public static void Register()
    {
        Unregister();
        _instance = new Scp079PingHints();
    }

    public static void Unregister()
    {
        _instance?.Dispose();
        _instance = null;
    }

    private Scp079PingHints()
    {
        Exiled.Events.Handlers.Scp079.Pinging += OnPinging;
        Exiled.Events.Handlers.Server.RestartingRound += ClearAll;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Exiled.Events.Handlers.Scp079.Pinging -= OnPinging;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearAll;
        ClearAll();
        GC.SuppressFinalize(this);
    }

    private void OnPinging(PingingEventArgs ev)
    {
        if (ev?.Room == null)
            return;

        string message = BuildMessage(ev);
        foreach (var scp in Player.List.Where(IsScpTeam))
            ShowTransient(scp, message);
    }

    private void ShowTransient(Player player, string text)
    {
        if (!IsPlayerValid(player))
            return;

        var display = TryGetDisplay(player);
        if (display == null)
            return;

        EnsureHint(display);
        SetText(display, text);

        int version = _versions.TryGetValue(player.Id, out int current) ? current + 1 : 1;
        _versions[player.Id] = version;

        Timing.CallDelayed(DisplaySeconds, () =>
        {
            if (_versions.TryGetValue(player.Id, out int latest) && latest == version)
            {
                var delayedDisplay = TryGetDisplay(player);
                if (delayedDisplay != null)
                    SetText(delayedDisplay, string.Empty);
            }
        });
    }

    private void ClearAll()
    {
        _versions.Clear();
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
            Alignment = HintAlignment.Center,
            SyncSpeed = HintSyncSpeed.Fastest,
            FontSize = 24,
            XCoordinate = 0,
            YCoordinate = _hintY,
        });
    }

    private static void SetText(PlayerDisplay display, string text)
    {
        var hint = display.GetHint(HintId);
        if (hint != null)
            hint.Text = text;
    }

    private static string BuildMessage(PingingEventArgs ev)
    {
        string zone = RoomNameTranslator.TranslateZoneName(ev.Room.Zone);
        string room = RoomNameTranslator.TranslateRoomName(ev.Room.Type);
        string target = ev.Type switch
        {
            PingType.Generator => "発電機",
            PingType.Projectile => "爆発物",
            PingType.MicroHid => "マイクロHIDを持った人間",
            PingType.Human => "人間",
            PingType.Elevator => "エレベーター",
            PingType.Door => "ドア",
            _ => string.Empty,
        };

        string color = ev.Type is PingType.Generator or PingType.Projectile or PingType.MicroHid
            ? "red"
            : ev.Type == PingType.Human
                ? "yellow"
                : "white";

        string targetText = string.IsNullOrEmpty(target) ? string.Empty : $"{target}に";
        return $"<color={color}><size=80%>SCP079が{targetText}ピンを差した。場所：{zone}の{room}</size></color>";
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

    private static bool IsScpTeam(Player player)
    {
        return player.GetTeam() == CTeam.SCPs || player.Role.Team == Team.SCPs;
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
