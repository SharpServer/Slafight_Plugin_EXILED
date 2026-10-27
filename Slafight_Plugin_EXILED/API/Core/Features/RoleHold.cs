using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using LabPlayerEvents = LabApi.Events.Handlers.PlayerEvents;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 演出などのためにバニラ役職を一時的に差し替え、<see cref="Restore"/> で元の状態へ戻します。
/// </summary>
/// <remarks>
/// 所持品・カスタム役職・部隊は差し替え中もそのまま残ります。
/// 保留中に外から役職を変えられたら、その時点で保留を捨てて戻しません。キーは netId です。
/// </remarks>
public sealed class RoleHold
{
    private static readonly Dictionary<uint, RoleHold> Holds = new Dictionary<uint, RoleHold>();

    private static bool hooked;

    private readonly List<EffectState> effects = [];
    private readonly List<AhpState> ahpProcesses = [];

    private bool swapping;
    private Vector3 position;
    private Quaternion rotation;
    private Vector3 scale;
    private float health;
    private float maxHealth;
    private float humeShield;
    private float stamina;
    private ushort? currentItemSerial;
    private int? scp079Experience;
    private float? scp079Energy;

    private RoleHold(Player player, uint netId)
    {
        Player = player;
        NetId = netId;
    }

    /// <summary>
    /// 保留しているプレイヤーです。
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// 持ち主を指す安定したキーです。
    /// </summary>
    public uint NetId { get; }

    /// <summary>
    /// 差し替える前の役職です。
    /// </summary>
    public RoleTypeId OriginalRole { get; private set; }

    /// <summary>
    /// 復元または破棄が済んだかどうか。
    /// </summary>
    public bool IsReleased { get; private set; }

    /// <summary>
    /// 指定プレイヤーが役職を保留中かどうか。
    /// </summary>
    public static bool IsHeld(Player player) => Of(player) is not null;

    /// <summary>
    /// 保留による役職変更の最中かどうか。この間の変更ではカスタム役職を外しません。
    /// </summary>
    public static bool IsSwapping(Player player) => Of(player)?.swapping == true;

    /// <summary>
    /// 指定プレイヤーの保留です。無ければ null。
    /// </summary>
    public static RoleHold Of(Player player)
    {
        if (player is null) return null;

        if (!Holds.TryGetValue(player.GetNetId(), out RoleHold hold)) return null;

        // 同じ netId に別人が入っていたら、それは前の人の残骸。
        return ReferenceEquals(hold.Player.ReferenceHub, player.ReferenceHub) ? hold : null;
    }

    /// <summary>
    /// 今の状態を控えてから <paramref name="temporaryRole"/> へ差し替えます。失敗したら null。
    /// </summary>
    public static RoleHold Begin(Player player, RoleTypeId temporaryRole, out string failure)
    {
        failure = null;

        if (!player.IsSafePlayer())
        {
            failure = "対象が存在しません。";

            return null;
        }

        uint netId = player.GetNetId();

        if (netId == 0)
        {
            failure = $"{player.Nickname} の netId が未割り当てです。";

            return null;
        }

        if (IsHeld(player))
        {
            failure = $"{player.Nickname} は既に役職を保留しています。";

            return null;
        }

        RoleHold hold = new RoleHold(player, netId);
        hold.Capture();

        Holds[netId] = hold;
        Hook();

        if (!hold.TrySetRole(temporaryRole))
        {
            hold.Release();
            failure = $"{temporaryRole} への役職変更が拒否されました (現在: {player.Role.Type})。";

            return null;
        }

        return hold;
    }

    /// <summary>
    /// 控えた役職と状態へ戻し、保留を終えます。
    /// </summary>
    public bool Restore(out string failure)
    {
        failure = null;

        if (IsReleased)
        {
            failure = "保留は既に終わっています。";

            return false;
        }

        if (!Player.IsSafePlayer())
        {
            Release();
            failure = "対象が退出しています。";

            return false;
        }

        // 位置を先に戻しておく。役職はその場で作り直されるので、クライアントへ届く役職情報も元の位置になる。
        if (Player.Role is FpcRole)
            Player.Position = position;

        if (!TrySetRole(OriginalRole))
        {
            Release();
            failure = $"{OriginalRole} への役職変更が拒否されました (現在: {Player.Role.Type})。";

            return false;
        }

        Apply();
        Release();

        return true;
    }

    /// <summary>
    /// 復元せずに保留だけを終えます。
    /// </summary>
    public void Release()
    {
        if (IsReleased) return;

        IsReleased = true;

        if (Holds.TryGetValue(NetId, out RoleHold current) && ReferenceEquals(current, this))
            Holds.Remove(NetId);
    }

    /// <summary>
    /// すべての保留を復元せずに捨てます。
    /// </summary>
    public static void ReleaseAll()
    {
        foreach (RoleHold hold in Holds.Values.ToArray())
        {
            hold.Release();
        }

        Holds.Clear();
    }

    /// <summary>
    /// すべての保留を捨て、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        ReleaseAll();

        if (!hooked) return;

        LabPlayerEvents.ChangedRole -= OnChangedRole;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RestartingRound -= ReleaseAll;
        hooked = false;
    }

    private bool TrySetRole(RoleTypeId role)
    {
        swapping = true;

        try
        {
            // 所持品と位置はそのまま持ち越す。
            Player.Role.Set(role, SpawnReason.ForceClass, RoleSpawnFlags.None);
        }
        finally
        {
            swapping = false;
        }

        return Player.Role.Type == role;
    }

    private void Capture()
    {
        OriginalRole = Player.Role.Type;
        position = Player.Position;
        rotation = Player.Rotation;
        scale = Player.Scale;
        health = Player.Health;
        maxHealth = Player.MaxHealth;
        humeShield = Player.HumeShield;
        stamina = Player.Stamina;
        currentItemSerial = Player.CurrentItem?.Serial;

        foreach (StatusEffectBase effect in Player.ActiveEffects)
        {
            // Duration 0 は無期限。
            float duration = effect.Duration > 0f ? Mathf.Max(effect.TimeLeft, 0.1f) : 0f;
            effects.Add(new EffectState(effect, effect.Intensity, duration));
        }

        foreach (AhpProcess process in Player.ActiveArtificialHealthProcesses)
        {
            ahpProcesses.Add(new AhpState(
                process.CurrentAmount,
                process.Limit,
                process.DecayRate,
                process.Efficacy,
                process.SustainTime,
                process.Persistant));
        }

        if (Player.Role is Scp079Role scp079)
        {
            scp079Experience = scp079.Experience;
            scp079Energy = scp079.Energy;
        }
    }

    private void Apply()
    {
        // 観戦者へ戻したなら、戻す体はない。
        if (!Player.IsAlive) return;

        if (Player.Role is FpcRole)
        {
            Player.Position = position;
            Player.Rotation = rotation;
        }

        if (Player.Scale != scale)
            Player.Scale = scale;

        // 役職の既定と同じなら上書きを残さない。
        if (!Mathf.Approximately(Player.MaxHealth, maxHealth))
            Player.MaxHealth = maxHealth;

        Player.Health = Mathf.Clamp(health, 1f, Player.MaxHealth);
        Player.HumeShield = humeShield;
        Player.Stamina = stamina;

        foreach (AhpState ahp in ahpProcesses)
        {
            Player.AddAhp(ahp.Amount, ahp.Limit, ahp.Decay, ahp.Efficacy, ahp.Sustain, ahp.Persistant);
        }

        foreach (EffectState state in effects)
        {
            if (state.Effect != null)
                state.Effect.ServerSetState(state.Intensity, state.Duration);
        }

        if (Player.Role is Scp079Role scp079)
        {
            if (scp079Experience is { } experience)
                scp079.Experience = experience;

            if (scp079Energy is { } energy)
                scp079.Energy = energy;
        }

        if (currentItemSerial is { } serial && Item.Get(serial) is { } item && item.Owner == Player)
            Player.CurrentItem = item;
    }

    private static void Hook()
    {
        if (hooked) return;

        hooked = true;

        LabPlayerEvents.ChangedRole += OnChangedRole;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RestartingRound += ReleaseAll;
    }

    private static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.Player?.ReferenceHub is not { } hub) return;
        if (Of(Player.Get(hub)) is not { } hold || hold.swapping) return;

        // 外から役職を変えられた。戻すと上書きになるので手放す。
        hold.Release();
    }

    private static void OnLeft(LeftEventArgs ev) => Of(ev.Player)?.Release();

    private readonly struct EffectState(StatusEffectBase effect, byte intensity, float duration)
    {
        public StatusEffectBase Effect { get; } = effect;

        public byte Intensity { get; } = intensity;

        public float Duration { get; } = duration;
    }

    private readonly struct AhpState(float amount, float limit, float decay, float efficacy, float sustain, bool persistant)
    {
        public float Amount { get; } = amount;

        public float Limit { get; } = limit;

        public float Decay { get; } = decay;

        public float Efficacy { get; } = efficacy;

        public float Sustain { get; } = sustain;

        public bool Persistant { get; } = persistant;
    }
}
