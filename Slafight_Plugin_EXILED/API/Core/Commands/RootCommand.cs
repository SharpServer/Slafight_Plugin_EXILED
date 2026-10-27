using CommandSystem;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// 運営コマンドの入口です。引数なしで実行すると、権限のある子コマンドの一覧を出します。
/// </summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class RootCommand : ParentCommandBase
{
    public override string Command => "slc";

    public override string Description => "Slafight Core の運営コマンド。";

    protected override string Title => "Slafight Core";
}
