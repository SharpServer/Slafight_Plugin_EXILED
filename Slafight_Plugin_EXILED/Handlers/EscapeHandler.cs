using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Handlers;

/// <summary>
/// 脱出したプレイヤーの行き先を決めて、実際に切り替えます。
/// </summary>
/// <remarks>
/// <para>
/// <b>行き先を知っているのは役職と陣営自身なので、この配線側に表はありません。</b>
/// 聞く順番は「役職 (<see cref="CustomRole.Escape"/>) → 陣営 (<see cref="CustomTeam.Escape"/>)
/// → バニラの判定」の 3 段だけです。どこも名乗らなければ何もしません。
/// </para>
/// <para>
/// 旧実装 (<c>Changes/EscapeHandler</c>) はバニラの脱出を常時止めたうえで、
/// マップの脱出点を 0.5 秒ごとに総当たりし、規則は
/// <c>CTeamEscapeRegistry</c> + <c>CTeamEscapeRuleSource</c> + 優先度つき 25 行の表で
/// 引いていました。ここでは引き金をバニラの脱出ゾーンに戻し、規則の表は捨てています。
/// </para>
/// <para>
/// 引き金だけバニラに任せ、<b>結果はこちらが全部引き受けます</b>
/// (<see cref="EscapingEventArgs.IsAllowed"/> を倒します)。カスタム役職への脱出を
/// バニラの役職変更に乗せると二重に役職を張り替えることになるためです。
/// その代わり、バニラが出していたクライアント通知はこちらから同じ形で送ります。
/// </para>
/// </remarks>
public sealed class EscapeHandler : EventHandlerBase
{
    /// <summary>役職が変わってからこの秒数は脱出させません。バニラの <c>Escape.MinAliveTime</c> と同じ値です。</summary>
    /// <remarks>
    /// 脱出ゾーンに立っている間は毎フレームここへ来るので、この足切りが
    /// 「1 回の脱出で 1 回だけ処理する」と「脱出先からの連鎖脱出を止める」を兼ねています。
    /// </remarks>
    private const float MinAliveSeconds = 10f;

    /// <summary>同じ人の脱出を続けて処理しない間隔 (秒)。</summary>
    /// <remarks>
    /// 脱出が成立すれば <see cref="MinAliveSeconds"/> が次を止めますが、
    /// 行き先の割り当てに失敗したときは役職が変わらないので止まりません。
    /// この間隔が無いと、失敗したまま脱出ゾーンに立っている人ぶんだけ
    /// 毎フレーム再試行してエラーが流れ続けます。
    /// </remarks>
    private const float RetryDelay = 5f;

    /// <summary>足元に落ちたと見なす距離 (m)。これより遠い落とし物は持ち主のものでも運びません。</summary>
    private const float CarryRadius = 1.05f;

    /// <summary>役職変更後の位置が落ち着くまでの待ち時間 (秒)。</summary>
    private const float CarryDelay = 0.5f;

    /// <summary>運ぶ先の高さ補正。床にめり込ませないための持ち上げです。</summary>
    private static readonly Vector3 CarryLift = new Vector3(0f, 0.15f, 0f);

    private static readonly float CarryRadiusSqr = CarryRadius * CarryRadius;

    /// <summary>直近に脱出を処理した時刻です。ラウンドをまたいで持ち越しません。</summary>
    private static readonly Dictionary<uint, float> LastAttempt = new Dictionary<uint, float>();

    /// <summary>
    /// 脱出が成立した直後に配られます。渡されるのは<b>脱出する前</b>の状況です。
    /// </summary>
    /// <remarks>
    /// バニラの <c>Player.Escaped</c> はこちらが脱出を引き受けている間は飛ばないので、
    /// 脱出を拾いたい側はこちらを見てください。
    /// </remarks>
    public static event Action<EscapeContext> Escaped;

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerHandlers.Escaping += OnEscaping;
        ServerHandlers.RestartingRound += LastAttempt.Clear;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerHandlers.Escaping -= OnEscaping;
        ServerHandlers.RestartingRound -= LastAttempt.Clear;
        LastAttempt.Clear();
    }

    private static void OnEscaping(EscapingEventArgs ev)
    {
        if (ev?.Player is not { } player || !player.IsSafePlayer()) return;

        if (player.Role.ActiveTime.TotalSeconds < MinAliveSeconds) return;

        uint netId = player.GetNetId();

        if (LastAttempt.TryGetValue(netId, out float last) && Time.time - last < RetryDelay) return;

        // IsAllowed を倒すと EscapeScenario は None を返すようになるので、先に読んでおく。
        EscapeScenario scenario = ev.EscapeScenario;

        EscapeContext escape = new EscapeContext(player);

        if (Resolve(escape, scenario, ev.NewRole) is not { } target) return;

        // ここから先はこちらの担当。バニラの役職変更は走らせない。
        ev.IsAllowed = false;
        LastAttempt[netId] = Time.time;

        Apply(escape, target, scenario);
    }

    /// <summary>
    /// 行き先を決めます。誰も名乗らなければ null。
    /// </summary>
    /// <remarks>
    /// 最後の段でバニラの答え (<paramref name="vanillaTarget"/>) をそのまま使うので、
    /// 「D クラスはカオス、研究員は NTF」を再実装する必要がありません。
    /// バニラが脱出と見なさなかった役職 (<see cref="EscapeScenario.None"/>) は、
    /// 役職か陣営が名乗らないかぎり脱出しません。
    /// </remarks>
    private static SpawnSetRoleDefinition? Resolve(
        in EscapeContext escape,
        EscapeScenario scenario,
        RoleTypeId vanillaTarget)
    {
        if (escape.Role?.Escape(escape) is { } byRole) return byRole;

        if (escape.Team?.Escape(escape) is { } byTeam) return byTeam;

        return scenario is EscapeScenario.None
            ? null
            : SpawnSetRoleDefinition.Vanilla(vanillaTarget);
    }

    /// <summary>
    /// 実際に脱出させます。
    /// </summary>
    private static void Apply(in EscapeContext escape, SpawnSetRoleDefinition target, EscapeScenario scenario)
    {
        Player player = escape.Player;

        NotifyClient(player, scenario);

        // 役職が変わると持ち物は消えるので、変わる前に落としておく。
        List<Pickup> carried = DropForCarry(player);

        if (!target.SpawnFor(player))
        {
            Log.Error($"[Slafight] {player.Nickname} の脱出先を割り当てられませんでした。");

            return;
        }

        CarryAfterEscape(player, carried);

        try
        {
            Escaped?.Invoke(escape);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] 脱出の通知で例外が発生しました: {exception}");
        }
    }

    /// <summary>
    /// バニラが出している「脱出した」画面を、こちらから同じ形で送ります。
    /// </summary>
    /// <remarks>
    /// バニラの脱出処理を止めている以上、この通知もこちらが出さないと画面に何も出ません。
    /// <see cref="EscapeScenario"/> の並びは <see cref="Escape.EscapeScenarioType"/> と
    /// 同じなので、そのままキャストして構いません。
    /// </remarks>
    private static void NotifyClient(Player player, EscapeScenario scenario)
    {
        if (scenario is EscapeScenario.None || !NetGuards.IsReadyClient(player)) return;

        player.Connection.Send(new Escape.EscapeMessage
        {
            ScenarioId = (byte)scenario,
            EscapeTime = (ushort)Mathf.CeilToInt((float)player.Role.ActiveTime.TotalSeconds),
        });
    }

    /// <summary>
    /// 持ち物を足元へ落とし、あとで運ぶぶんを拾い出します。
    /// </summary>
    private static List<Pickup> DropForCarry(Player player)
    {
        Vector3 origin = player.Position;

        player.DropItems();

        // 落とした瞬間の足元にあるものだけが対象。前に置きっぱなしにした所持品は運ばない。
        return
        [
            .. Pickup.List
                .Where(pickup => pickup is not null &&
                                 pickup.PreviousOwner == player &&
                                 (pickup.Position - origin).sqrMagnitude <= CarryRadiusSqr)
        ];
    }

    /// <summary>
    /// 落としておいた持ち物を、脱出先の足元へ運びます。
    /// </summary>
    /// <remarks>
    /// 役職変更後の位置が確定するのは次のフレーム以降なので少し待ちます。
    /// 待っている間にラウンドが終われば <see cref="RoundScope"/> が破棄するので、
    /// 次のラウンドの地面に前ラウンドの落とし物が現れることはありません。
    /// </remarks>
    private static void CarryAfterEscape(Player player, List<Pickup> carried)
    {
        if (carried.Count == 0) return;

        uint netId = player.GetNetId();

        RoundScope.Current.Delay(CarryDelay, () =>
        {
            // 待っている間に抜けた・別人が同じ枠に入った場合は運ばない。
            if (!player.IsSafePlayer() || player.GetNetId() != netId) return;

            Vector3 destination = player.Position + CarryLift;

            foreach (Pickup pickup in carried)
            {
                if (pickup is { IsSpawned: true })
                    pickup.Position = destination;
            }
        });
    }
}
