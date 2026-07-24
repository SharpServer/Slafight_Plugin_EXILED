using System;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

// Assuming this has PrefabHelper if custom, or use standard if not

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class SpawnBuiltInPrefab : ICommand
{
    public string Command => "prefab";
    public string[] Aliases { get; } = ["prefab"];
    public string Description => "Spawns a PrefabType at your location using PrefabHelper";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        // Permission check (matching the original style)
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        var executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Player not found.";
            return false;
        }

        // No arguments -> show usage and available prefabs
        if (arguments.Count == 0)
        {
            var names = Enum.GetNames(typeof(PrefabType));
            response = "Usage: .prefab <PrefabType>\nAvailable prefabs:\n" + string.Join(", ", names);
            return false;
        }

        var prefabName = arguments.At(0);

        // Parse PrefabType
        if (!Enum.TryParse<PrefabType>(prefabName, true, out var prefabType))
        {
            var names = Enum.GetNames(typeof(PrefabType));
            response = $"Unknown prefab: {prefabName}\nAvailable prefabs:\n" + string.Join(", ", names);
            return false;
        }

        // Spawn prefab at executor's position using PrefabHelper
        // Note: Replace with actual PrefabHelper usage if it's a custom extension in your plugin.
        // Assuming PrefabHelper.Spawn(executor.Position, prefabType, Quaternion.identity) or similar.
        // If PrefabHelper is not standard, ensure it's defined in Slafight_Plugin_EXILED.Extensions.
        // Placeholder: Use Map.Spawn or whatever the helper does.
        try
        {
            // Example spawn call - adjust based on your PrefabHelper signature
            // e.g., PrefabHelper.SpawnPrefab(prefabType, executor.Position, executor.Rotation);
            var spawned = PrefabHelper.Spawn(prefabType, executor.Position, executor.Rotation); // Adjust method name/signature as per your plugin

            response = $"Spawned {prefabType} prefab at {executor.Nickname}'s location.";
            return true;
        }
        catch (Exception ex)
        {
            response = $"Failed to spawn prefab {prefabType}: {ex.Message}";
            return false;
        }
    }
}