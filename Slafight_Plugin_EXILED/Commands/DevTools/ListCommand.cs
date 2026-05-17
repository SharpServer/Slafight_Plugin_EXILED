using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class ListCommand : ICommand
{
    public string Command => "list";
    public string[] Aliases { get; } = ["ls", "search"];
    public string Description => "List/search roles, items, abilities, events, waves, prefabs, or players.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission("slperm.list") && !sender.CheckPermission("slperm.help"))
        {
            response = "Permission denied. Required: slperm.list";
            return false;
        }

        if (arguments.Count == 0)
        {
            response =
                "Usage: sl list <roles|items|abilities|events|waves|prefabs|players> [filter]\n" +
                "Examples:\n" +
                "  sl list roles scp\n" +
                "  sl list items keycard\n" +
                "  sl list players";
            return false;
        }

        var category = arguments.At(0).ToLowerInvariant();
        var filter = arguments.Count >= 2 ? string.Join(" ", arguments.Skip(1)) : null;

        switch (category)
        {
            case "role":
            case "roles":
                response = "Roles:\n" + CommandTools.JoinNames(RoleParseHelper.GetAllRoleNames(), filter);
                return true;

            case "item":
            case "items":
            case "citem":
            case "citems":
                response = "CItems:\n" + CommandTools.JoinNames(
                    CItem.GetAllInstances().Select(i => $"{i.UniqueKeyName} ({i.DisplayName})"), filter);
                return true;

            case "ability":
            case "abilities":
                response = "Abilities:\n" + CommandTools.JoinNames(AbilityParseHelper.GetAllAbilityNames(), filter);
                return true;

            case "event":
            case "events":
                response = "Special events:\n" + CommandTools.JoinNames(Enum.GetNames(typeof(SpecialEventType)), filter);
                return true;

            case "wave":
            case "waves":
            case "spawn":
            case "spawns":
                response = "Spawn waves:\n" + CommandTools.JoinNames(Enum.GetNames(typeof(SpawnTypeId)), filter);
                return true;

            case "prefab":
            case "prefabs":
                response = "Prefabs:\n" + CommandTools.JoinNames(Enum.GetNames(typeof(PrefabType)), filter);
                return true;

            case "player":
            case "players":
                response = "Players:\n" + string.Join("\n", Player.List
                    .OrderBy(p => p.Id)
                    .Select(p => $"{p.Id}: {p.Nickname} | {p.Role.Type} | UniqueRole={p.UniqueRole}"));
                return true;

            default:
                response = $"Unknown list category: {category}\nUse: roles, items, abilities, events, waves, prefabs, players";
                return false;
        }
    }
}
