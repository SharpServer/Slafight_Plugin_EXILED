using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// デバッグ HUD の表示を切り替えます。
/// </summary>
public sealed class DebugCommand : CommandBase
{
    /// <summary>
    /// デバッグ表示に必要な権限ノードです。Server Specifics の切り替えも同じノードを見ます。
    /// </summary>
    public const string PermissionNode = "slperm.debug";

    public override Type Parent => typeof(RootCommand);

    public override string Command => "debug";

    public override string Permission => PermissionNode;

    public override string Usage => "debug [対象]";

    public override string Description => "デバッグ HUD の表示を切り替えます。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetPlayer(0, out Player target))
        {
            response = "対象が見つかりません。";

            return false;
        }

        bool enabled = DebugMode.Toggle(target);

        response = $"{target.Nickname} のデバッグ表示を {(enabled ? "有効" : "無効")} にしました。";

        return true;
    }
}
