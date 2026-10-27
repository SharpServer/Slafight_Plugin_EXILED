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
/// RA の <c>action</c> コマンド実行の前後で、押した管理者と選択された Dummy を <see cref="DummyActionInvocation"/> に保持・破棄し、
/// 実行後に <see cref="DummyActions.Invoked"/> を起こします。
/// </summary>
[HarmonyPatch(typeof(ActionDummyCommand), nameof(ActionDummyCommand.Execute))]
internal static class DummyActionContextPatch
{
    // 実行後にはアクション一覧が変わる (Equip → Holster など) ので、実行前に一致したダミーを控える。
    private static readonly List<(Npc Dummy, string Module, string Name)> Matched = [];

    private static void Prefix(ArraySegment<string> arguments, ICommandSender sender)
    {
        Matched.Clear();

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
            CaptureMatches(arguments, selected);
        }
        catch (Exception ex)
        {
            // 解析に失敗してもコマンド本体は通常実行させ、フォールバックコンテキストに委ねる。
            Log.Debug($"[DummyAction] コンテキストの解決に失敗しました: {ex}");
            DummyActionInvocation.Clear();
        }
    }

    private static void Postfix(bool __result)
    {
        if (!__result)
            return;

        Player? sender = DummyActionInvocation.Sender;
        foreach (var (dummy, module, name) in Matched)
            DummyActions.RaiseInvoked(dummy, module, name, sender);
    }

    private static void Finalizer()
    {
        // 例外の有無に関わらず、選択情報が次のコマンドへ漏れないよう必ず破棄する。
        DummyActionInvocation.Clear();
        Matched.Clear();
    }

    private static void CaptureMatches(ArraySegment<string> arguments, IReadOnlyList<Npc> selected)
    {
        if (arguments.Count < 3)
            return;

        // 本体と同じく空白を '_' にして大文字小文字まで一致させる。
        string module = arguments.At(1);
        string name = arguments.At(2);
        foreach (Npc dummy in selected)
        {
            foreach (DummyActionEntry entry in DummyActions.List(dummy))
            {
                if (entry.Module.Replace(' ', '_') == module && entry.Name.Replace(' ', '_') == name)
                {
                    Matched.Add((dummy, entry.Module, entry.Name));
                    break;
                }
            }
        }
    }
}
