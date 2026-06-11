#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Scp079;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class Scp079PingHints : IBootstrapHandler, IDisposable
{
    private const float DisplaySeconds = 5f;
    private static Scp079PingHints? _instance;

    private readonly Dictionary<int, int> _versions = new();
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

    private void OnPinging(PingingEventArgs? ev)
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

        int version = _versions.TryGetValue(player.Id, out int current) ? current + 1 : 1;
        _versions[player.Id] = version;

        player.ShowHint(text, DisplaySeconds);

        Timing.CallDelayed(DisplaySeconds, () =>
        {
            if (_versions.TryGetValue(player.Id, out int latest) && latest == version)
            {
                _versions.Remove(player.Id);
            }
        });
    }

    private void ClearAll()
    {
        _versions.Clear();
    }

    private static string BuildMessage(PingingEventArgs ev)
    {
        string zone = RoomNameTranslator.TranslateZoneName(ev.Room.Zone);
        string room = RoomNameTranslator.TranslateRoomName(ev.Room.Type);
        string target = ev.Type switch
        {
            PingType.Generator => "発電機",
            PingType.Projectile => "爆発物",
            PingType.MicroHid => "マイクロ HID を持った人間",
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
        return $"<color={color}><size=80%>SCP079 が{targetText}ピンを差した。場所：{zone}の{room}</size></color>";
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
        return player.GetTeam() == CTeam.SCPs;
    }
}
