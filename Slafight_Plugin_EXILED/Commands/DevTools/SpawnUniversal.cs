using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class SpawnUniversal : ICommand
{
    public string Command     => "spawn";
    public string[] Aliases   => ["us"];   // "spawn"自体はCommandに使うのでAliasesから除去
    public string Description => "Set a vanilla/custom role. Usage: sl spawn <roleId> [target]";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"Permission denied. Required: slperm.{Command}";
            return false;
        }

        var executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Player not found.";
            return false;
        }

        if (arguments.Count == 0)
        {
            response = "Usage: sl spawn <roleId> [target]\n" +
                     "Target can be omitted, @me, player id, nickname, or UserId.\nAvailable roles:\n"
                     + string.Join(", ", RoleParseHelper.GetAllRoleNames());
            return false;
        }

        var roleId = arguments.At(0);

        // ── 特殊ロール ────────────────────────────────────────────
        if (TryHandleSpecialRole(roleId, executor, out response))
            return true;

        // ── 汎用ロールパース ──────────────────────────────────────
        if (!RoleParseHelper.TryParseRole(roleId, out var vanilla, out var custom))
        {
            response = $"Unknown role: {roleId}\nAvailable roles:\n"
                     + string.Join(", ", RoleParseHelper.GetAllRoleNames());
            return false;
        }

        // ── ターゲット解決 ────────────────────────────────────────
        if (!TryResolveTarget(arguments, executor, out var target, out response))
            return false;

        // ── ロール付与 ────────────────────────────────────────────
        if (vanilla.HasValue)
        {
            target.SetRole(vanilla.Value);
            response = $"{target.Nickname} → {vanilla.Value}";
            return true;
        }

        if (custom.HasValue)
        {
            target.SetRole(custom.Value);
            response = $"{target.Nickname} → {target.UniqueRole}";
            return true;
        }

        response = "Failed to assign role.";
        return false;
    }

    // ─────────────────────────────────────────────────────────────

    private static bool TryHandleSpecialRole(string roleId, Player executor, out string response)
    {
        if (roleId.Equals("mp", StringComparison.OrdinalIgnoreCase))
        {
            executor.UniqueRole = "MapEditor";
            // PlayerHUD.Instance.ResetHudForPlayer(executor);
            response = $"{executor.Nickname} → MapEditor";
            return true;
        }

        if (roleId.Equals("debug", StringComparison.OrdinalIgnoreCase))
        {
            executor.UniqueRole = "Debug";
            response = $"{executor.Nickname} → Debug";
            return true;
        }

        response = string.Empty;
        return false;
    }

    private static bool TryResolveTarget(
        ArraySegment<string> arguments,
        Player executor,
        out Player target,
        out string response)
    {
        target   = executor;
        response = string.Empty;

        if (arguments.Count < 2)
            return true;

        return CommandTools.TryResolvePlayer(arguments.At(1), executor, out target, out response);
    }
}
