#nullable enable
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Scp079;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class Scp079PingHints : EventHandlerBase
{
    private const float DisplaySeconds = 5f;

    private readonly Dictionary<int, int> _versions = new();

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp079.Pinging += OnPinging;
        Exiled.Events.Handlers.Server.RestartingRound += ClearAll;

        base.RegisterEvents();
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp079.Pinging -= OnPinging;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearAll;
        ClearAll();

        base.UnregisterEvents();
    }

    private void OnPinging(PingingEventArgs? ev)
    {
        if (ev?.Room == null)
            return;

        string message = BuildMessage(ev);
        foreach (var recipient in Player.List.Where(IsScpTeam))
            ShowTransient(recipient, message);
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
        string zone = ev.Room.Zone.TranslateZoneName();
        string room = ev.Room.Type.TranslateRoomName();
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
        return player.Role.Side == Side.Scp;
    }

    private static bool IsHuman(Player player)
    {
        return player.IsHuman;
    }
}
