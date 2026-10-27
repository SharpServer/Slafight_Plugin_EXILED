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
/// 脱出したプレイヤーの行き先を決めて切り替えます。
/// </summary>
/// <remarks>役職 → 陣営 → バニラの順に聞きます。バニラの脱出は止め、クライアント通知はこちらで送ります。</remarks>
public sealed class EscapeHandler : EventHandlerBase
{
    /// <summary>役職が変わってからこの秒数は脱出させません。バニラの <c>Escape.MinAliveTime</c> と同じ値です。</summary>
    private const float MinAliveSeconds = 10f;

    /// <summary>同じ人の脱出を続けて処理しない間隔 (秒)。割り当て失敗時の再試行を抑えます。</summary>
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
    /// 脱出が成立した直後に配られます。渡されるのは脱出する前の状況です。
    /// </summary>
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
    /// バニラの「脱出した」画面をこちらから送ります。
    /// </summary>
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
