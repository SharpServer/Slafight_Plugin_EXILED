using System;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// 波ではない <see cref="SpawnSet"/> を一覧・実行します。波は <see cref="WaveCommand"/> が扱います。
/// </summary>
public sealed class SpawnSetCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "set";

    public override string Usage => "set [クラス名]";

    public override string Description => "役職の一括割り当てを一覧・実行します。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string name))
        {
            response = BuildOverview();

            return true;
        }

        if (!CoreCatalog.TryResolve<SpawnSet>(name, out Type setType, out string failure))
        {
            response = failure;

            return false;
        }

        if (Activator.CreateInstance(setType) is not SpawnSet set)
        {
            response = $"{setType.Name} を生成できませんでした。";

            return false;
        }

        int assigned = set.Spawn().Count;

        response = assigned > 0
            ? $"'{set.Name}' で {assigned} 人に割り当てました。"
            : $"'{set.Name}' を実行しましたが、対象になるプレイヤーが居ませんでした。";

        return true;
    }

    private static string BuildOverview()
    {
        StringBuilder builder = new StringBuilder();

        var sets = CoreCatalog.Types<SpawnSet>()
            .Select(type => (Type: type, Probe: CoreCatalog.Probe<SpawnSet>(type)))
            .Where(entry => entry.Probe is not null && entry.Probe.RespawnWeight <= 0)
            .ToList();

        builder.AppendLine(CoreCatalog.Header("Spawn Sets", sets.Count));

        foreach (var entry in sets)
        {
            string allowed = entry.Probe.AllowedPlayerCount < 0
                ? "上限なし"
                : $"最大 {entry.Probe.AllowedPlayerCount} 人";

            builder.AppendLine(
                $"  <b>{entry.Type.Name}</b> — {entry.Probe.Name} [{allowed} / {entry.Probe.SpawnRoles.Count} 行]");
        }

        builder.AppendLine("  (リスポーンウェーブは slc wave)");

        return builder.ToString().TrimEnd();
    }
}
