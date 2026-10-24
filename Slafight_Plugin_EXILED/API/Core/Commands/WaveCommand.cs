using System;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// リスポーンウェーブを一覧・強制実行し、抽選の状況を切り替えます。
/// </summary>
public sealed class WaveCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "wave";

    public override string Usage => "wave <クラス名|context <クラス名|reset>|suspend <on|off>>";

    public override string Description => "リスポーンウェーブの強制実行と抽選状況の切り替え。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string first))
        {
            response = BuildOverview();

            return true;
        }

        switch (first.ToLowerInvariant())
        {
            case "context":
                return HandleContext(out response);

            case "suspend":
                return HandleSuspend(out response);
        }

        if (SpawnContext.Find(first) is not { } wave)
        {
            response = $"'{first}' というウェーブはありません。\n{BuildOverview()}";

            return false;
        }

        int assigned = SpawnSystem.ForceSpawn(wave).Count;

        response = assigned > 0
            ? $"'{wave.Name}' で {assigned} 人に割り当てました。"
            : $"'{wave.Name}' を実行しましたが、対象になる観戦者が居ませんでした。";

        return true;
    }

    private bool HandleContext(out string response)
    {
        if (!TryGetArgument(1, out string name))
        {
            response = $"現在の状況: {SpawnContext.Active.Name}\n{CoreCatalog.Names<SpawnContext>()}";

            return true;
        }

        if (string.Equals(name, "reset", StringComparison.OrdinalIgnoreCase))
        {
            SpawnContext.Reset();
            response = $"抽選状況を {SpawnContext.Active.Name} に戻しました。";

            return true;
        }

        if (!CoreCatalog.TryResolve<SpawnContext>(name, out Type contextType, out string failure))
        {
            response = failure;

            return false;
        }

        if (Activator.CreateInstance(contextType) is not SpawnContext created)
        {
            response = $"{contextType.Name} を生成できませんでした。";

            return false;
        }

        SpawnContext.Apply(created);
        response = $"抽選状況を {created.Name} に切り替えました。";

        return true;
    }

    private bool HandleSuspend(out string response)
    {
        if (!TryGetArgument(1, out string value))
        {
            response = $"リスポーン: {(SpawnSystem.IsSuspended ? "停止中" : "動作中")}";

            return true;
        }

        SpawnSystem.IsSuspended = value is "on" or "true" or "1";
        response = $"リスポーンを{(SpawnSystem.IsSuspended ? "停止" : "再開")}しました。";

        return true;
    }

    internal static string BuildOverview(string filter = null)
    {
        StringBuilder builder = new StringBuilder();
        SpawnContext context = SpawnContext.Active;

        builder.AppendLine(CoreCatalog.Header($"Waves (状況: {context.Name})", SpawnContext.AllWaves.Count));

        foreach (bool inContext in new[] { true, false })
        {
            builder.AppendLine(inContext ? "Context中 (自動抽選対象):" : "Context外 (手動実行可能):");
            SpawnSet[] entries = SpawnContext.AllWaves
                .Where(wave => (context.WeightOf(wave) > 0) == inContext &&
                               CoreCatalog.Matches(wave.GetType(), filter))
                .OrderBy(wave => wave.GetType().Name, StringComparer.Ordinal)
                .ToArray();

            if (entries.Length == 0)
                builder.AppendLine("  (該当なし)");

            foreach (SpawnSet wave in entries)
            {
                string kind = wave.IsMiniWave ? "増援" : "本隊";
                builder.AppendLine(
                    $"  <b>{wave.GetType().Name}</b> — {wave.Name} " +
                    $"[{wave.RespawnFaction} / {kind} / 重み {context.WeightOf(wave)} / 割合 {wave.RespawnRatio:P0}]");
            }
        }

        if (SpawnSystem.IsSuspended)
            builder.AppendLine("  <color=#ffd966>リスポーンは停止中です。</color>");

        return builder.ToString().TrimEnd();
    }
}
