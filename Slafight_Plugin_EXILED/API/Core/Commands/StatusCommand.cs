using System;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.ProximityChat;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// 自動登録されたハンドラや有効な役職・アイテムなど、土台の状態を表示します。
/// </summary>
public sealed class StatusCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "status";

    public override string[] Aliases { get; } = ["info"];

    public override string Description => "登録状況と実行中の状態を表示します。";

    protected override bool OnExecute(out string response)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("<b>Slafight Core</b>");
        builder.AppendLine($"  自動登録ハンドラ : {EventHandlerRegistry.AutoRegistered.Count}");
        builder.AppendLine($"  有効なハンドラ   : {EventHandlerBase.Active.Count}");
        builder.AppendLine($"  宣言済みチーム   : {CustomTeam.All.Count}");
        builder.AppendLine($"  現在の役職       : {CustomRole.Active.Count}");
        builder.AppendLine($"  追跡中アイテム   : {CustomItem.Tracked.Count}");
        builder.AppendLine($"  ゲームモード     : {GameMode.Current?.Name ?? "なし"}");
        builder.AppendLine($"  スポーン状況     : {SpawnContext.Active.Name}");
        builder.AppendLine($"  宣言済みウェーブ : {SpawnContext.AllWaves.Count}");
        builder.AppendLine($"  ボイス経路ルール : {VoiceRoutingApi.RegisteredRules.Count}");
        builder.AppendLine(
            $"  近接チャット     : 使用可 {Handler.CanUsePlayers.Count} 人 / " +
            $"有効化 {Handler.ActivatedPlayers.Count} 人");

        if (!TryGetArgument(0, out string verbose) || verbose is not ("-v" or "full"))
        {
            builder.AppendLine("  (-v で内訳)");

            response = builder.ToString().TrimEnd();

            return true;
        }

        builder.AppendLine();
        builder.AppendLine("<b>Handlers</b>");

        foreach (string name in EventHandlerBase.Active
                     .Select(handler => handler.GetType().Name)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            builder.AppendLine($"  - {name}");
        }

        if (CustomRole.Active.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("<b>Roles</b>");

            foreach (CustomRole role in CustomRole.Active)
            {
                builder.AppendLine($"  - {role.Player?.Nickname ?? "?"} : {role.GetType().Name}");
            }
        }

        response = builder.ToString().TrimEnd();

        return true;
    }
}
