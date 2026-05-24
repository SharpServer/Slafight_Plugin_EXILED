using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class StatusCommand : ICommand
{
    public string Command => "status";
    public string[] Aliases { get; } = ["info", "state"];
    public string Description => "Show Slafight round, event, queue, player, and ability state.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        var seh = SpecialEventsHandler.Instance;
        var queue = seh?.EventQueue.Count > 0
            ? string.Join(" -> ", seh.EventQueue.Select((e, i) => $"#{i}:{e}"))
            : "empty";

        var current = seh?.NowEvent ?? SpecialEventType.None;
        var next = seh?.EventQueue.FirstOrDefault() ?? SpecialEventType.None;
        var players = Player.List.OrderBy(p => p.Id).ToArray();
        var abilityUsers = players.Count(p => AbilityManager.TryGetLoadout(p, out var loadout) &&
                                              loadout.Slots.Any(a => a != null));

        response =
            "Slafight Status\n" +
            $"  Round: Lobby={Round.IsLobby}, Started={Round.IsStarted}, Ended={Round.IsEnded}\n" +
            $"  Event: Current={current}, Display={seh?.LocalizedEventName ?? "none"}, Next={next}, PID={seh?.EventPID ?? 0}\n" +
            $"  Queue: {queue}\n" +
            $"  Players: {players.Length}, AbilityUsers={abilityUsers}\n" +
            "Use `sl list players` or `sl player info <id>` for player details.";
        return true;
    }
}
