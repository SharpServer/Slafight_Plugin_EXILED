using System;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>クラス名を指定してシーケンスを開始します。</summary>
public sealed class SequenceCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "sequence";

    public override string Usage => "sequence <クラス名> [force]";

    public override string Description => "シーケンスを通常または強制で開始します。";

    protected override bool OnExecute(out string response)
    {
        if (Arguments.Count == 0)
        {
            response = $"使い方: slc {Usage}\n{CoreCatalog.Names<SequenceBase>()}";
            return true;
        }

        bool force = Arguments.Count == 2 &&
                     TryGetArgument(1, out string mode) &&
                     string.Equals(mode, "force", StringComparison.OrdinalIgnoreCase);
        if (!TryGetArgument(0, out string name) || (Arguments.Count != 1 && !force))
        {
            response = $"使い方: slc {Usage}";
            return false;
        }

        if (!CoreCatalog.TryResolve<SequenceBase>(name, out Type type, out response))
            return false;

        SequenceBase sequence;
        try
        {
            sequence = (SequenceBase)Activator.CreateInstance(type);
        }
        catch (Exception exception)
        {
            SequenceManager.Report(exception);
            response = $"'{type.FullName}' を生成できませんでした。公開の引数なしコンストラクターとサーバーログを確認してください。";
            return false;
        }

        if (!SequenceManager.TryStart(sequence, force))
        {
            response = $"'{type.Name}' を開始できませんでした。";
            return false;
        }

        response = force ? $"'{type.Name}' を強制開始しました。" : $"'{type.Name}' を開始しました。";
        return true;
    }
}
