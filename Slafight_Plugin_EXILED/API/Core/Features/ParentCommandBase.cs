using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CommandBase.Parent"/> でこの型を指したコマンドを子として集める親コマンドの基底です。
/// </summary>
/// <remarks>引数なしなら実行できる子コマンドの一覧を返します。</remarks>
public abstract class ParentCommandBase : ParentCommand
{
    protected ParentCommandBase() => LoadGeneratedCommands();

    public override string[] Aliases { get; } = [];

    public override string Description => string.Empty;

    /// <summary>
    /// 一覧の先頭に出す見出しです。
    /// </summary>
    protected virtual string Title => Command;

    /// <summary>
    /// <see cref="CommandBase.Parent"/> がこの型を指しているコマンドを集めて登録します。
    /// </summary>
    public override void LoadGeneratedCommands()
    {
        ClearCommands();

        Type self = GetType();

        foreach (Type type in TypeParser.FindTypes<CommandBase>())
        {
            CommandBase child;

            try
            {
                child = (CommandBase)Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] コマンド {type.FullName} を生成できませんでした: {exception}");

                continue;
            }

            if (child.Parent != self) continue;

            // CommandHandler.RegisterCommand は同名で例外を投げるので、ここで弾いておく。
            if (TryGetCommand(child.Command, out ICommand existing))
            {
                Log.Error(
                    $"[Slafight] {self.Name} の子コマンド '{child.Command}' が重複しています " +
                    $"({existing.GetType().FullName} と {type.FullName})。後者は登録しません。");

                continue;
            }

            RegisterCommand(child);
        }
    }

    /// <summary>
    /// 送信者が実行できる子コマンドだけを並べた一覧を作ります。
    /// </summary>
    protected string BuildCatalog(ICommandSender sender)
    {
        List<CommandBase> visible =
        [
            .. AllCommands
                .OfType<CommandBase>()
                .Where(command => command.IsAllowedFor(sender))
                .OrderBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
        ];

        if (visible.Count == 0)
            return $"{Title}: 実行できるコマンドがありません。";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"<b>{Title}</b>  ({visible.Count} commands)");

        foreach (CommandBase command in visible)
        {
            string aliases = command.Aliases is { Length: > 0 }
                ? $" [{string.Join(", ", command.Aliases)}]"
                : string.Empty;

            builder.AppendLine($"  <b>{Command} {command.Usage}</b>{aliases}");

            if (command.Description is { Length: > 0 } description)
                builder.AppendLine($"      {description}");
        }

        return builder.ToString().TrimEnd();
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = BuildCatalog(sender);

        return true;
    }
}
