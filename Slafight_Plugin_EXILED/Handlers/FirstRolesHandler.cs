using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpawnSets.RoundStart;
using Slafight_Plugin_EXILED.SpawnSets.Specials;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Handlers;

/// <summary>
/// ラウンド開始時の役職をこちらで配ります。
/// </summary>
/// <remarks>
/// やることは 2 つだけです。<b>バニラの割り当てを止めて、自分で配る。</b>
///
/// 旧実装 (<c>MainHandlers/FirstRolesHandler</c>) はここに
/// 重み表・上限表・20 回のリトライ・季節ごとのテーブル切り替え・
/// 10 秒後の遅延アンロックまで抱えていましたが、その大半は
/// <see cref="SpawnSet"/> 側に構造として畳まれたので不要になりました。
/// <list type="bullet">
/// <item>重み表と上限表 → <c>SpawnRoles</c> の 1 行 (<c>weight</c> と <c>count</c>)</item>
/// <item>抽選のリトライ → <c>PickNext</c> が枠の埋まった行を候補から外すので要らない</item>
/// <item>季節ごとの差し替え → <see cref="SpawnContext"/></item>
/// <item>ラウンドロック → <see cref="SpawnSet.Spawn"/> が自分で掛けて必ず戻す</item>
/// </list>
/// ここに割り当てロジックを書き足したくなったら、それは
/// <see cref="SpawnSet"/> の派生に書くべきものだという合図です。
/// </remarks>
public sealed class FirstRolesHandler : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerHandlers.ChangingRole += CancelVanillaAssignment;
        ServerHandlers.RoundStarted += AssignFirstRoles;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerHandlers.ChangingRole -= CancelVanillaAssignment;
        ServerHandlers.RoundStarted -= AssignFirstRoles;
    }

    /// <summary>
    /// バニラのラウンド開始割り当てを止めます。
    /// </summary>
    /// <remarks>
    /// 止めないとバニラが先に配ってしまい、こちらの割り当て対象
    /// (<c>RoleTypeId.None</c> のロビー在席者) が居なくなります。
    /// <see cref="SpawnReason.RoundStart"/> だけを弾くので、
    /// リスポーンや観戦復帰など他の経路には触りません。
    /// </remarks>
    private static void CancelVanillaAssignment(ChangingRoleEventArgs ev)
    {
        if (ev.Reason is SpawnReason.RoundStart)
            ev.IsAllowed = false;
    }

    /// <summary>
    /// SCP を先に、残り全員に人間役職を配ります。
    /// </summary>
    /// <remarks>
    /// 順番に意味があります。SCP を先に配ると、その人たちは
    /// <c>RoleTypeId.None</c> でなくなるので人間側の対象から自動的に外れます。
    /// 人数を数えて分ける処理を書く必要がありません。
    /// </remarks>
    private static void AssignFirstRoles()
    {
        new FirstRolesSCPsSet().Spawn();
        new FirstRolesHumanSet().Spawn();
    }
}
