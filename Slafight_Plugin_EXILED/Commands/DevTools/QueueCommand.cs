using System;
using System.Linq;
using CommandSystem;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class QueueCommand : ICommand
{
    public string Command => "queue";
    public string[] Aliases { get; } = ["eventqueue", "eq"];
    public string Description => "Manage special-event queue: list/set/add/insert/remove/clear/skip/random/run.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission("slperm.queue"))
        {
            response = "Permission denied. Required: slperm.queue";
            return false;
        }

        if (arguments.Count == 0 || arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            response =
                "Usage: sl queue <list|set|add|insert|remove|clear|skip|random|run> [...]\n" +
                "  list\n" +
                "  set <SpecialEventType>        Replace queue[0]\n" +
                "  add <SpecialEventType>        Append event\n" +
                "  insert <index> <SpecialEventType>\n" +
                "  remove <index>\n" +
                "  clear\n" +
                "  skip [index]\n" +
                "  random [set|add|run]\n" +
                "  run <SpecialEventType>\n" +
                "Events: " + string.Join(", ", Enum.GetNames(typeof(SpecialEventType)));
            return false;
        }

        var seh = SpecialEventsHandler.Instance;
        if (seh == null)
        {
            response = "SpecialEventsHandler is not initialized.";
            return false;
        }

        var action = arguments.At(0).ToLowerInvariant();
        switch (action)
        {
            case "list":
            case "show":
            case "get":
                response = FormatQueue(seh);
                return true;

            case "set":
                if (!TryReadEvent(arguments, 1, out var setEvent, out response))
                    return false;

                seh.SetQueueEvent(setEvent);
                response = $"Queue[0] set to {setEvent}.\n{FormatQueue(seh)}";
                return true;

            case "add":
                if (!TryReadEvent(arguments, 1, out var addEvent, out response))
                    return false;

                seh.AddEvent(addEvent);
                response = $"Added {addEvent}.\n{FormatQueue(seh)}";
                return true;

            case "insert":
                if (arguments.Count < 3 || !int.TryParse(arguments.At(1), out var insertIndex))
                {
                    response = "Usage: sl queue insert <index> <SpecialEventType>";
                    return false;
                }

                if (!TryReadEvent(arguments, 2, out var insertEvent, out response))
                    return false;

                insertIndex = Math.Max(0, Math.Min(insertIndex, seh.EventQueue.Count));
                seh.EventQueue.Insert(insertIndex, insertEvent);
                seh.EventLocSet();
                response = $"Inserted {insertEvent} at #{insertIndex}.\n{FormatQueue(seh)}";
                return true;

            case "remove":
            case "delete":
            case "del":
                if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out var removeIndex))
                {
                    response = "Usage: sl queue remove <index>";
                    return false;
                }

                if (removeIndex < 0 || removeIndex >= seh.EventQueue.Count)
                {
                    response = $"Invalid queue index: {removeIndex}";
                    return false;
                }

                var removed = seh.EventQueue[removeIndex];
                seh.EventQueue.RemoveAt(removeIndex);
                seh.EventLocSet();
                response = $"Removed #{removeIndex}: {removed}.\n{FormatQueue(seh)}";
                return true;

            case "clear":
                seh.EventQueue.Clear();
                seh.EventLocSet();
                response = "Event queue cleared.";
                return true;

            case "skip":
                var skipIndex = 0;
                if (arguments.Count >= 2 && !int.TryParse(arguments.At(1), out skipIndex))
                {
                    response = "Usage: sl queue skip [index]";
                    return false;
                }

                if (skipIndex < 0 || skipIndex >= seh.EventQueue.Count)
                {
                    response = $"Invalid queue index: {skipIndex}";
                    return false;
                }

                var skipped = seh.EventQueue[skipIndex];
                seh.SkipEvent(skipIndex);
                response = $"Skipped #{skipIndex}: {skipped}.\n{FormatQueue(seh)}";
                return true;

            case "random":
            case "reroll":
                var mode = arguments.Count >= 2 ? arguments.At(1).ToLowerInvariant() : "set";
                if (mode is "run" or "start")
                {
                    seh.RunRandomEvent();
                    response = $"Random event run requested. Current={seh.NowEvent}, Display={seh.LocalizedEventName}";
                    return true;
                }

                if (mode is "add" or "append")
                {
                    seh.InsertQueueRandomEventAfterFirst();
                    response = $"Random event inserted after queue[0].\n{FormatQueue(seh)}";
                    return true;
                }

                seh.SetQueueRandomEvent();
                response = $"Queue[0] randomized.\n{FormatQueue(seh)}";
                return true;

            case "run":
            case "start":
                if (!TryReadEvent(arguments, 1, out var runEvent, out response))
                    return false;

                seh.RunEvent(runEvent);
                response = $"Forced event started: {seh.LocalizedEventName} ({runEvent})";
                return true;

            default:
                response = $"Unknown queue action: {action}. Use `sl queue help`.";
                return false;
        }
    }

    private static bool TryReadEvent(ArraySegment<string> arguments, int index, out SpecialEventType eventType, out string response)
    {
        eventType = SpecialEventType.None;

        if (arguments.Count <= index)
        {
            response = "Missing SpecialEventType.\nEvents: " + string.Join(", ", Enum.GetNames(typeof(SpecialEventType)));
            return false;
        }

        var arg = arguments.At(index);
        if (Enum.TryParse(arg, true, out eventType) && Enum.IsDefined(typeof(SpecialEventType), eventType))
        {
            response = string.Empty;
            return true;
        }

        response = $"Unknown SpecialEventType: {arg}\nEvents: " + string.Join(", ", Enum.GetNames(typeof(SpecialEventType)));
        return false;
    }

    private static string FormatQueue(SpecialEventsHandler seh)
    {
        if (seh.EventQueue.Count == 0)
            return "Queue: empty";

        return "Queue:\n" + string.Join("\n", seh.EventQueue.Select((e, i) => $"  #{i}: {e}"));
    }
}
