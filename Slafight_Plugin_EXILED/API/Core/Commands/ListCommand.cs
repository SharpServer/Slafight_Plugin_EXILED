using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// 宣言されているものを種類ごとに一覧します。出た名前はそのまま他のコマンドの引数に使えます。
/// </summary>
public sealed class ListCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "list";

    public override string[] Aliases { get; } = ["ls"];

    public override string Usage => "list <roles|items|abilities|teams|modes|waves|sets|handlers> [絞り込み]";

    public override string Description => "宣言されている役職・アイテム・陣営などを一覧します。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string category))
        {
            response = $"使い方: {Usage}";

            return false;
        }

        TryGetArgument(1, out string filter);

        response = category.ToLowerInvariant() switch
        {
            "roles" or "role" => Build<CustomRole>("Roles", filter, DescribeRole),
            "items" or "item" => Build<CustomItem>("Items", filter, DescribeItem),
            "abilities" or "ability" => Build<AbilityBase>("Abilities", filter, DescribeAbility),
            "teams" or "team" => Build<CustomTeam>("Teams", filter, DescribeTeam),
            "modes" or "mode" => Build<GameMode>("Game Modes", filter, DescribeMode),
            "waves" or "wave" => WaveCommand.BuildOverview(filter),
            "sets" or "set" => Build<SpawnSet>("Spawn Sets", filter, DescribeSet, OnlySets),
            "handlers" or "handler" => BuildHandlers(filter),
            _ => null,
        };

        if (response is null)
        {
            response = $"不明な種類です: {category}\n使い方: {Usage}";

            return false;
        }

        return true;
    }

    private static string Build<TBase>(
        string title,
        string filter,
        Func<TBase, string> describe,
        Func<TBase, bool> include = null)
        where TBase : class
    {
        List<string> lines = [];

        foreach (Type type in CoreCatalog.Types<TBase>())
        {
            if (!CoreCatalog.Matches(type, filter)) continue;

            if (CoreCatalog.Probe<TBase>(type) is not { } probe) continue;

            if (include is not null && !include(probe)) continue;

            lines.Add($"  <b>{type.Name}</b> — {describe(probe)}");
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(CoreCatalog.Header(title, lines.Count));

        if (lines.Count == 0)
            builder.AppendLine("  (該当なし)");
        else
            lines.ForEach(line => builder.AppendLine(line));

        return builder.ToString().TrimEnd();
    }

    private static bool OnlySets(SpawnSet set) => set.RespawnWeight <= 0;

    private static string DescribeRole(CustomRole role)
    {
        string voice = role.Voice.Proximity.IsAvailable
            ? $" / 近接ボイス:{role.Voice.Proximity.SourceChannel}"
            : string.Empty;

        return $"{role.Name} [{role.BaseRole} / {role.Team?.Name ?? "陣営なし"}{voice}]";
    }

    private static string DescribeItem(CustomItem item) => $"{item.Name} [{item.BaseType}]";

    private static string DescribeAbility(AbilityBase ability)
    {
        string uses = ability.MaxUses < 0 ? "∞" : ability.MaxUses.ToString();
        string options = ability.Options.Count > 1 ? $" / 選択肢 {ability.Options.Count}" : string.Empty;

        return $"{ability.Name} [CD {ability.Cooldown:0.#}s / 回数 {uses}{options}]";
    }

    private static string DescribeTeam(CustomTeam team)
    {
        string victory = team.Victory is null ? "勝利条件なし" : $"優先度 {team.Victory.Priority}";

        return $"{team.Name} [{victory} / 生存 {team.Members.Count()}]";
    }

    private static string DescribeMode(GameMode mode)
    {
        string availability = mode.Weight <= 0
            ? "手動のみ"
            : mode.IsAvailable ? $"重み {mode.Weight}" : $"重み {mode.Weight} / 条件未達";

        return $"{mode.Name} [{availability}]";
    }

    private static string DescribeSet(SpawnSet set)
    {
        string allowed = set.AllowedPlayerCount < 0 ? "上限なし" : $"最大 {set.AllowedPlayerCount} 人";

        return $"{set.Name} [{allowed} / {set.SpawnRoles.Count} 行]";
    }

    private static string BuildHandlers(string filter)
    {
        List<string> lines =
        [
            .. EventHandlerBase.Active
                .Select(handler => handler.GetType())
                .Where(type => CoreCatalog.Matches(type, filter))
                .Select(type => $"  <b>{type.Name}</b>")
                .OrderBy(line => line, StringComparer.Ordinal)
        ];

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(CoreCatalog.Header("Handlers", lines.Count));
        lines.ForEach(line => builder.AppendLine(line));

        return builder.ToString().TrimEnd();
    }
}
