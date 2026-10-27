using System.Linq;
using Exiled.API.Features;
using LabApi.Events.Arguments.ServerEvents;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// ゲーム本体の終了判定に答えてラウンドの終了を決めます。勝者は <see cref="CustomTeam.FindWinner"/> が決めます。
/// </summary>
public sealed class RoundEndHandler : EventHandlerBase
{
    /// <summary>
    /// 勝敗表示を出してから実際に終わらせるまでの秒数です。
    /// </summary>
    private const float EndDelay = 10f;

    private bool ended;

    /// <inheritdoc />
    public override HandlerLifetime Lifetime => HandlerLifetime.Round;

    /// <inheritdoc />
    public override void OnServerRoundEndingConditionsCheck(RoundEndingConditionsCheckEventArgs ev)
    {
        // 独自終了を始めた後は、バニラに二重で終わらせない。
        if (ended)
        {
            ev.CanEnd = false;

            return;
        }

        if (CustomTeam.FindWinner() is { } winner)
        {
            // バニラに任せる陣営なら、そのまま通す。
            if (winner.UsesVanillaEnding) return;

            ev.CanEnd = false;
            End(winner);

            return;
        }

        // 独自終了を持つ陣営がまだ生きているなら、バニラに終わらせない。
        // ここで通すと、勝敗が決まる前にラウンドが畳まれる。
        if (HasCustomEndingTeamAlive())
            ev.CanEnd = false;
    }

    /// <summary>
    /// 独自の終了処理を持つ陣営に、まだ生存者が居るか。
    /// </summary>
    private static bool HasCustomEndingTeamAlive() =>
        CustomTeam.All.Any(team => !team.UsesVanillaEnding && team.Members.Any());

    /// <summary>
    /// 勝敗表示を出してラウンドを畳みます。
    /// </summary>
    private void End(CustomTeam winner)
    {
        ended = true;

        // 同じ陣営でも、走っているゲームモードが別の名乗りをすることがある。
        string text = GameMode.Current?.VictoryText(winner) ?? winner.VictoryText;

        if (text is { Length: > 0 })
        {
            // 大きさは VictoryText 側が <size=80> を持っているのでそちらに任せる。
            // 縦位置は互換アダプタが Y≈700 中心に決めるため、ここでは指定できない。
            foreach (Player player in Player.List.Where(candidate => candidate.IsSafePlayer()))
            {
                player.ShowHint(text, EndDelay);
            }
        }

        Log.Debug($"[Slafight] ラウンド終了: {winner.GetVictoryReason()}");

        RoundScope.Current.Delay(EndDelay, () => Round.EndRound(true));
    }
}
