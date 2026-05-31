using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class AbilityUniversal : ICommand
{
    public string Command => "giveability";
    public string[] Aliases { get; } = ["ga", "ability", "au"];
    public string Description => "Give an ability. Usage: sl giveability <ability> [target] [cooldown] [uses]";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        // パーミッションチェック
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"権限不足: slperm.{Command}";
            return false;
        }

        var executor = Player.Get(sender);
        if (executor == null)
        {
            response = "プレイヤー未検出";
            return false;
        }

        if (arguments.Count == 0)
        {
            response = $"Usage: sl {Command} <ability> [target] [cooldown] [uses]\n"
                     + $"Examples: sl {Command} sh | sl {Command} sh 5 | sl {Command} sh @me 5 3\n"
                     + $"Abilities: {string.Join(", ", AbilityParseHelper.GetAllAbilityNames())}";
            return false;
        }

        var abilityId = arguments.At(0);

        // --- ターゲット判定（@me / ID） ---
        Player target = executor;
        var optionIndex = 1;
        if (arguments.Count >= 2 && !CommandTools.TryParseFloat(arguments.At(1), out _))
        {
            if (!CommandTools.TryResolvePlayer(arguments.At(1), executor, out target, out response))
                return false;

            optionIndex = 2;
        }

        // --- オプション引数 ---
        float? cooldown = null;
        int? maxUses = null;

        if (arguments.Count > optionIndex && CommandTools.TryParseFloat(arguments.At(optionIndex), out var cd))
        {
            cooldown = Math.Max(0.1f, cd); // 最低0.1秒
        }

        if (arguments.Count > optionIndex + 1 && int.TryParse(arguments.At(optionIndex + 1), out var uses))
        {
            maxUses = uses < 0 ? -1 : uses; // -1=無制限
        }

        // --- Ability付与 ---
        bool success = AbilityParseHelper.TryGiveAbility(abilityId, target, cooldown, maxUses);
        
        if (!success)
        {
            response = $"不明なAbility: {abilityId}\n"
                     + $"利用可能: {string.Join(", ", AbilityParseHelper.GetAllAbilityNames())}";
            return false;
        }

        // --- 成功メッセージ ---
        var msg = $"[{target.Nickname}] {abilityId}";
        if (cooldown.HasValue) msg += $" CD={cooldown:F1}s";
        if (maxUses.HasValue) msg += $" 回数={maxUses}";
        
        response = msg;
MeowExtensions.ShowHint(        executor, $"<color=green>{msg}</color>", 3f);
        return true;
    }
}
