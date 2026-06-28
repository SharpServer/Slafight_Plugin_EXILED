using System;
using System.Collections.Generic;
using CommandSystem;
using CommandSystem.Commands.RemoteAdmin.Dummies;
using Exiled.API.Features;
using HarmonyLib;
using RemoteAdmin;
using Slafight_Plugin_EXILED.API.Features;
using Utils;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// RemoteAdminの<c>action</c>コマンド(<see cref="ActionDummyCommand"/>)実行の前後で、
/// 押した管理者と選択されたDummyを<see cref="DummyActionInvocation"/>に保持・破棄します。
/// </summary>
/// <remarks>
/// 本体はDummyアクションのコールバックをパラメータなしのSystem.Actionとして呼び出すため、
/// 押下時のコンテキストをコールバックへ渡すにはこのパッチでホルダーへ供給するしかありません。
/// </remarks>
[HarmonyPatch(typeof(ActionDummyCommand), nameof(ActionDummyCommand.Execute))]
internal static class DummyActionContextPatch
{
    private static void Prefix(ArraySegment<string> arguments, ICommandSender sender)
    {
        try
        {
            Player? admin = null;
            if (sender is PlayerCommandSender { ReferenceHub: { } hub })
                admin = Player.Get(hub);

            // 本体と同じ方法で選択対象を解決する。プールから借りたリストは保持せず自前リストへコピーする。
            var hubs = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out _);
            var selected = new List<Npc>();
            if (hubs != null)
            {
                foreach (var target in hubs)
                {
                    if (target == null || !target.IsDummy)
                        continue;

                    var npc = Npc.Get(target);
                    if (npc != null)
                        selected.Add(npc);
                }
            }

            DummyActionInvocation.Set(admin, selected);
        }
        catch (Exception ex)
        {
            // 解析に失敗してもコマンド本体は通常実行させ、フォールバックコンテキストに委ねる。
            Log.Debug($"[DummyAction] コンテキストの解決に失敗しました: {ex}");
            DummyActionInvocation.Clear();
        }
    }

    private static void Finalizer()
    {
        // 例外の有無に関わらず、選択情報が次のコマンドへ漏れないよう必ず破棄する。
        DummyActionInvocation.Clear();
    }
}
