using System;
using CommandSystem;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public sealed class WaypointStreamCommand : ICommand
{
    public string Command => "waypointstream";
    public string[] Aliases { get; } = ["wpstream", "wps"];
    public string Description => "Stream WaypointToy chunks globally around active players.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (arguments.Count == 0 || arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            response =
                "Usage:\n" +
                $"  sl waypointstream start [chunkSize={WaypointChunkStreamer.DefaultChunkSize:0}] " +
                $"[preload={WaypointChunkStreamer.DefaultPreloadDistance:0}] " +
                $"[gcSeconds={WaypointChunkStreamer.DefaultGcDelay:0}]\n" +
                "  sl waypointstream ensure <x> <y> <z>\n" +
                "  sl waypointstream stop\n" +
                "  sl waypointstream status";
            return false;
        }

        switch (arguments.At(0).ToLowerInvariant())
        {
            case "start":
                return Start(arguments, out response);

            case "ensure":
                return Ensure(arguments, out response);

            case "stop":
                WaypointChunkStreamer.Stop();
                response = "Waypoint streaming stopped. Unreferenced chunks will be garbage-collected after the configured delay.";
                return true;

            case "status":
                response = WaypointChunkStreamer.GetStatus();
                return true;

            default:
                response = $"Unknown waypointstream action: {arguments.At(0)}. Use `sl waypointstream help`.";
                return false;
        }
    }

    private static bool Start(ArraySegment<string> arguments, out string response)
    {
        if (arguments.Count > 4)
        {
            response = "Usage: sl waypointstream start [chunkSize] [preload] [gcSeconds]";
            return false;
        }

        if (!TryReadOptional(arguments, 1, WaypointChunkStreamer.DefaultChunkSize, out float chunkSize) ||
            !TryReadOptional(arguments, 2, WaypointChunkStreamer.DefaultPreloadDistance, out float preload) ||
            !TryReadOptional(arguments, 3, WaypointChunkStreamer.DefaultGcDelay, out float gcDelay))
        {
            response = "chunkSize, preload, and gcSeconds must be valid invariant-culture numbers.";
            return false;
        }

        if (!WaypointChunkStreamer.TryConfigure(chunkSize, preload, gcDelay, out response))
            return false;

        response =
            "Global waypoint streaming started around all FPC players.\n" +
            $"Chunk={chunkSize:0.##}m, preload={preload:0.##}m, GC={gcDelay:0.##}s.";
        return true;
    }

    private static bool Ensure(ArraySegment<string> arguments, out string response)
    {
        if (!TryReadVector(arguments, 1, out Vector3 position))
        {
            response = "Usage: sl waypointstream ensure <x> <y> <z>";
            return false;
        }

        if (!WaypointChunkStreamer.IsConfigured)
        {
            response = "Waypoint streaming is not configured.";
            return false;
        }

        int created = WaypointChunkStreamer.EnsureCoverage(position);
        response = $"Ensured waypoint coverage at {Format(position)}. Created chunks: {created}.";
        return true;
    }

    private static bool TryReadVector(ArraySegment<string> arguments, int index, out Vector3 vector)
    {
        vector = default;
        if (arguments.Count < index + 3 ||
            !CommandTools.TryParseFloat(arguments.At(index), out float x) ||
            !CommandTools.TryParseFloat(arguments.At(index + 1), out float y) ||
            !CommandTools.TryParseFloat(arguments.At(index + 2), out float z))
        {
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private static bool TryReadOptional(
        ArraySegment<string> arguments,
        int index,
        float defaultValue,
        out float value)
    {
        if (arguments.Count <= index)
        {
            value = defaultValue;
            return true;
        }

        return CommandTools.TryParseFloat(arguments.At(index), out value);
    }

    private static string Format(Vector3 value)
        => $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
}
