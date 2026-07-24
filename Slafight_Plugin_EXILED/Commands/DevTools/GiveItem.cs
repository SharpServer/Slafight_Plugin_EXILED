using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class GiveItem : ICommand
{
    public string Command => "giveitem";
    public string[] Aliases { get; } = ["gi", "item", "gitem"];
    public string Description => "Give a CItem by key. Usage: sl giveitem <key> [target]";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        // パーミッションチェック（例: slperm.giveitem）
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        Player executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Player not found.";
            return false;
        }

        // ヘルプ表示（引数なし or 不十分）
        if (arguments.Count == 0 || arguments.Count == 1 && arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            var keys = CItem.GetAllInstances().Select(ci => ci.UniqueKeyName).ToArray();
            response = "Usage: sl giveitem <uniqueKey> [target]\n" +
                       "Target can be omitted, @me, player id, nickname, or UserId.\n" +
                       $"Available CItem keys:\n{string.Join(", ", keys)}";
            return false;
        }

        string key = arguments.At(0);

        // CItem を UniqueKey から取得
        if (!CItem.TryGetByKey(key, out CItem? cItem))
        {
            var keys = CItem.GetAllInstances().Select(ci => ci.UniqueKeyName).ToArray();
            response = $"Unknown CItem key: {key}\n" +
                       $"Available keys:\n{string.Join(", ", keys)}";
            return false;
        }

        // ターゲット（省略時は実行者）
        Player target = executor;
        if (arguments.Count >= 2 &&
            !CommandTools.TryResolvePlayer(arguments.At(1), executor, out target, out response))
            return false;

        // CItem を付与して、返り値があれば成功
        var item = cItem!.Give(target, displayMessage: true);
        if (item != null)
        {
            response = $"Gave {cItem.DisplayName} ({cItem.UniqueKeyName}) to {target.Nickname}.";
            return true;
        }

        response = $"Failed to give {cItem.DisplayName} to {target.Nickname}.";
        return false;
    }
}
