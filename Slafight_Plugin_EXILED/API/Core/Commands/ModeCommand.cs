using System;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// ゲームモードを一覧・起動・停止します。
/// </summary>
public sealed class ModeCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "mode";

    public override string Usage => "mode [クラス名|stop|roll]";

    public override string Description => "ゲームモードの一覧・起動・停止。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string name))
        {
            response = BuildOverview();

            return true;
        }

        if (string.Equals(name, "stop", StringComparison.OrdinalIgnoreCase))
        {
            if (GameMode.Current is not { } running)
            {
                response = "走っているモードはありません。";

                return false;
            }

            GameMode.StopCurrent();
            response = $"'{running.Name}' を停止しました。";

            return true;
        }

        if (string.Equals(name, "roll", StringComparison.OrdinalIgnoreCase))
        {
            if (GameMode.Roll() is not { } rolled)
            {
                response = "起動できるモードがありません (重み 0 か条件未達)。";

                return false;
            }

            return Start(rolled, out response);
        }

        if (!CoreCatalog.TryResolve<GameMode>(name, out Type modeType, out string failure))
        {
            response = failure;

            return false;
        }

        return Start((GameMode)Activator.CreateInstance(modeType), out response);
    }

    private static bool Start(GameMode mode, out string response)
    {
        if (!mode.Start())
        {
            response = $"'{mode.Name}' を開始できませんでした。";

            return false;
        }

        response = $"'{mode.Name}' を開始しました。";

        return true;
    }

    private static string BuildOverview()
    {
        StringBuilder builder = new StringBuilder();
        var modes = GameMode.All();

        builder.AppendLine(CoreCatalog.Header($"Game Modes (実行中: {GameMode.Current?.Name ?? "なし"})", modes.Count));

        foreach (GameMode mode in modes.OrderBy(mode => mode.GetType().Name, StringComparer.Ordinal))
        {
            string availability = mode.Weight <= 0
                ? "手動のみ"
                : mode.IsAvailable ? $"重み {mode.Weight}" : $"重み {mode.Weight} / 条件未達";

            builder.AppendLine($"  <b>{mode.GetType().Name}</b> — {mode.Name} [{availability}]");
        }

        return builder.ToString().TrimEnd();
    }
}
