using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// プレイヤーごとのデバッグ表示の状態です。寿命は <see cref="PlayerScope"/> に従います。
/// </summary>
public static class DebugMode
{
    private static readonly HashSet<uint> Enabled = [];

    /// <summary>後始末を仕掛け済みの netId です。同じ人へ二重に仕掛けないために持ちます。</summary>
    private static readonly HashSet<uint> Hooked = [];

    /// <summary>
    /// このプレイヤーがデバッグ表示を出しているか。
    /// </summary>
    public static bool IsEnabled(Player player) =>
        player is not null && Enabled.Contains(player.GetNetId());

    /// <summary>
    /// 切り替えます。
    /// </summary>
    /// <returns>切り替えた結果。</returns>
    public static bool Toggle(Player player) => Set(player, !IsEnabled(player));

    /// <summary>
    /// 明示的に設定します。
    /// </summary>
    /// <returns>設定後の状態。</returns>
    public static bool Set(Player player, bool enabled)
    {
        if (player is null) return false;

        uint netId = player.GetNetId();

        if (netId == 0) return false;

        bool was = Enabled.Contains(netId);

        if (enabled)
        {
            Enabled.Add(netId);

            // 後始末は 1 人につき 1 回だけ仕掛ける。ON/OFF のたびに積むと閉じ手が溜まる。
            if (Hooked.Add(netId))
                PlayerScope.Of(player).OnDispose(_ =>
                {
                    Enabled.Remove(netId);
                    Hooked.Remove(netId);
                });
        }
        else
        {
            Enabled.Remove(netId);
        }

        // 設定画面の表示もこちらへ合わせる。コマンドで切り替えたときに
        // 画面が ON のままだと、同じ値を押しても設定は飛ばず切り替えられなくなる。
        if (was != enabled)
            ServerSpecificUserSettings.TrySetTwoButtonIsB(player, ServerSpecifics.DebugModeSettingId, !enabled);

        return enabled;
    }

    /// <summary>
    /// 全員ぶん消します。
    /// </summary>
    public static void ClearAll()
    {
        Enabled.Clear();
        Hooked.Clear();
    }
}
