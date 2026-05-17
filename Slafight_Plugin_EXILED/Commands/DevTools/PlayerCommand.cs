using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class PlayerCommand : ICommand
{
    public string Command => "player";
    public string[] Aliases { get; } = ["p", "pl"];
    public string Description => "Inspect/control players: info/role/item/ability/clearabilities/heal/god/bring/tp.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (!CommandTools.TryGetExecutor(sender, out var executor, out response))
            return false;

        if (arguments.Count == 0 || arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            response =
                "Usage: sl player <action> [...]\n" +
                "  list\n" +
                "  info [target]\n" +
                "  role <roleId> [target]\n" +
                "  item <citemKey> [target]\n" +
                "  ability <ability> [target] [cooldown] [uses]\n" +
                "  clearabilities [target]\n" +
                "  heal [target] [amount|full]\n" +
                "  god [target] [on|off|toggle]\n" +
                "  bring <target>\n" +
                "  tp <target> [destination]\n" +
                "Target can be @me, player id, exact nickname, partial nickname, or UserId.";
            return false;
        }

        switch (arguments.At(0).ToLowerInvariant())
        {
            case "list":
                response = string.Join("\n", Player.List.OrderBy(p => p.Id).Select(FormatPlayerLine));
                return true;

            case "info":
                if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var infoTarget, out response))
                    return false;

                response = FormatPlayerInfo(infoTarget);
                return true;

            case "role":
            case "setrole":
                return SetRole(arguments, executor, out response);

            case "item":
            case "give":
            case "giveitem":
                return GiveItem(arguments, executor, out response);

            case "ability":
            case "giveability":
                return GiveAbility(arguments, executor, out response);

            case "clearabilities":
            case "clearability":
            case "clearab":
                if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var clearTarget, out response))
                    return false;

                clearTarget.ClearAbilities();
                response = $"Cleared abilities from {clearTarget.Nickname}.";
                return true;

            case "heal":
                return Heal(arguments, executor, out response);

            case "god":
            case "godmode":
                return SetGod(arguments, executor, out response);

            case "bring":
                if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var bringTarget, out response))
                    return false;

                bringTarget.Position = executor.Position;
                response = $"Brought {bringTarget.Nickname} to {executor.Nickname}.";
                return true;

            case "tp":
            case "teleport":
                return Teleport(arguments, executor, out response);

            default:
                response = $"Unknown player action: {arguments.At(0)}. Use `sl player help`.";
                return false;
        }
    }

    private static bool SetRole(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl player role <roleId> [target]\nRoles: " +
                       CommandTools.JoinNames(RoleParseHelper.GetAllRoleNames());
            return false;
        }

        if (!RoleParseHelper.TryParseRole(arguments.At(1), out var vanilla, out var custom))
        {
            response = $"Unknown role: {arguments.At(1)}\nRoles: " +
                       CommandTools.JoinNames(RoleParseHelper.GetAllRoleNames(), arguments.At(1));
            return false;
        }

        if (!CommandTools.TryResolveOptionalPlayer(arguments, 2, executor, out var target, out response))
            return false;

        if (vanilla.HasValue)
        {
            target.SetRole(vanilla.Value, RoleSpawnFlags.All);
            response = $"{target.Nickname} -> {vanilla.Value}";
            return true;
        }

        if (custom.HasValue)
        {
            target.SetRole(custom.Value, RoleSpawnFlags.All);
            response = $"{target.Nickname} -> {custom.Value}";
            return true;
        }

        response = "Failed to assign role.";
        return false;
    }

    private static bool GiveItem(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl player item <citemKey> [target]\nItems: " +
                       CommandTools.JoinNames(CItem.GetAllInstances().Select(i => i.UniqueKeyName));
            return false;
        }

        if (!CItem.TryGetByKey(arguments.At(1), out var cItem) || cItem == null)
        {
            response = $"Unknown CItem key: {arguments.At(1)}\nItems: " +
                       CommandTools.JoinNames(CItem.GetAllInstances().Select(i => i.UniqueKeyName), arguments.At(1));
            return false;
        }

        if (!CommandTools.TryResolveOptionalPlayer(arguments, 2, executor, out var target, out response))
            return false;

        var item = cItem.Give(target, true);
        response = item != null
            ? $"Gave {cItem.DisplayName} ({cItem.UniqueKeyName}) to {target.Nickname}."
            : $"Failed to give {cItem.DisplayName} to {target.Nickname}.";
        return item != null;
    }

    private static bool GiveAbility(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl player ability <ability> [target] [cooldown] [uses]\nAbilities: " +
                       CommandTools.JoinNames(AbilityParseHelper.GetAllAbilityNames());
            return false;
        }

        var target = executor;
        var optionIndex = 2;
        if (arguments.Count >= 3 && !CommandTools.TryParseFloat(arguments.At(2), out _))
        {
            if (!CommandTools.TryResolvePlayer(arguments.At(2), executor, out target, out response))
                return false;

            optionIndex = 3;
        }

        float? cooldown = null;
        int? uses = null;
        if (arguments.Count > optionIndex && CommandTools.TryParseFloat(arguments.At(optionIndex), out var cd))
            cooldown = Math.Max(0.1f, cd);

        if (arguments.Count > optionIndex + 1 && int.TryParse(arguments.At(optionIndex + 1), out var parsedUses))
            uses = parsedUses < 0 ? -1 : parsedUses;

        if (!AbilityParseHelper.TryGiveAbility(arguments.At(1), target, cooldown, uses))
        {
            response = $"Unknown or failed ability: {arguments.At(1)}\nAbilities: " +
                       CommandTools.JoinNames(AbilityParseHelper.GetAllAbilityNames(), arguments.At(1));
            return false;
        }

        response = $"Gave ability {arguments.At(1)} to {target.Nickname}" +
                   (cooldown.HasValue ? $" CD={cooldown.Value:0.0}s" : string.Empty) +
                   (uses.HasValue ? $" Uses={uses.Value}" : string.Empty);
        return true;
    }

    private static bool Heal(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var target, out response))
            return false;

        if (arguments.Count < 3 || arguments.At(2).Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            target.Health = target.MaxHealth;
            response = $"{target.Nickname} healed to full ({target.Health:0}/{target.MaxHealth:0}).";
            return true;
        }

        if (!CommandTools.TryParseFloat(arguments.At(2), out var amount))
        {
            response = "Usage: sl player heal [target] [amount|full]";
            return false;
        }

        target.Health = Math.Min(target.MaxHealth, target.Health + Math.Max(0f, amount));
        response = $"{target.Nickname} healed by {amount:0}. Health={target.Health:0}/{target.MaxHealth:0}.";
        return true;
    }

    private static bool SetGod(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (!CommandTools.TryResolveOptionalPlayer(arguments, 1, executor, out var target, out response))
            return false;

        var mode = arguments.Count >= 3 ? arguments.At(2).ToLowerInvariant() : "toggle";
        target.IsGodModeEnabled = mode switch
        {
            "on" or "true" or "1" => true,
            "off" or "false" or "0" => false,
            _ => !target.IsGodModeEnabled,
        };

        response = $"{target.Nickname} GodMode={target.IsGodModeEnabled}.";
        return true;
    }

    private static bool Teleport(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl player tp <target> [destination]";
            return false;
        }

        if (!CommandTools.TryResolvePlayer(arguments.At(1), executor, out var target, out response))
            return false;

        var destination = executor;
        if (arguments.Count >= 3 &&
            !CommandTools.TryResolvePlayer(arguments.At(2), executor, out destination, out response))
        {
            return false;
        }

        target.Position = destination.Position;
        response = $"Teleported {target.Nickname} to {destination.Nickname}.";
        return true;
    }

    private static string FormatPlayerLine(Player player)
        => $"{player.Id}: {player.Nickname} | Role={player.Role.Type} | UniqueRole={player.UniqueRole} | HP={player.Health:0}/{player.MaxHealth:0}";

    private static string FormatPlayerInfo(Player player)
    {
        var abilities = AbilityManager.TryGetLoadout(player, out var loadout)
            ? string.Join(", ", loadout.Slots.Select((a, i) => a == null ? $"Slot{i}: empty" : $"Slot{i}: {a.GetType().Name}"))
            : "No loadout";

        return
            $"{FormatPlayerLine(player)}\n" +
            $"  UserId={player.UserId}\n" +
            $"  Team={player.Role.Team}, GodMode={player.IsGodModeEnabled}\n" +
            $"  Position={player.Position.x:0.00}, {player.Position.y:0.00}, {player.Position.z:0.00}\n" +
            $"  Abilities={abilities}";
    }
}
