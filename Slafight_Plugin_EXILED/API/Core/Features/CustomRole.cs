using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using PlayerRoles;
using PlayerRoles.PlayableScps.HumeShield;
using Slafight_Plugin_EXILED.API.Core.Interfaces;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using LabPlayerEvents = LabApi.Events.Handlers.PlayerEvents;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// カスタム役職です。プレイヤー 1 人につき 1 インスタンス生成され、型そのものが識別子になります。
/// </summary>
public abstract class CustomRole : IPlayerOwn
{
    private static readonly Dictionary<uint, CustomRole> ActiveRoles = new Dictionary<uint, CustomRole>();

    private static readonly Dictionary<ItemType, ushort> EmptyAmmo = new Dictionary<ItemType, ushort>();

    private static bool globalEventsRegistered;
    private bool eventsRegistered;

    // ShowStatus の置き場。表示層は Status を読むだけなので、ここに持たせて構わない。
    private string status;
    private float statusExpiresAt;
    private bool ownsHumeShieldSettings;
    private bool ownsHumeShieldMaximum;

    /// <summary>
    /// この役職を持っているプレイヤーです。<see cref="Spawn(Player)"/> で設定されます。
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    /// 持ち主を指す安定したキーです。
    /// </summary>
    public uint NetId { get; private set; }

    /// <summary>
    /// このカスタム役職へ切り替わる直前のバニラ役職です。
    /// 改宗解除など、元の状態へ戻す必要がある役職が参照します。
    /// </summary>
    public RoleTypeId PreviousRole { get; private set; } = RoleTypeId.None;

    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 説明文です。既定のヒント表示で使われます。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// 所属チームです。勝利判定に使われます。インスタンスで指してください。
    /// </summary>
    public virtual CustomTeam Team => null;

    /// <summary>
    /// 土台になるバニラ役職です。
    /// </summary>
    public abstract RoleTypeId BaseRole { get; }

    /// <summary>
    /// 部隊システムでの役職優先度です。大きいほど上位で、0 ならバニラ役職の優先度を使います。
    /// </summary>
    /// <remarks>目安: 二等兵 1 / 軍曹・特殊 2 / 隊長 3。それより上位は 4 以上。</remarks>
    public virtual int ForceRolePower => 0;

    /// <summary>
    /// スポーン位置です。null ならバニラのスポーン地点を使います。
    /// </summary>
    public virtual Vector3? SpawnPosition => null;

    /// <summary>
    /// スポーン時の向きです。null なら触りません。
    /// </summary>
    public virtual Quaternion? SpawnRotation => null;

    /// <summary>
    /// 体の大きさです。null なら等倍のままです。役職が外れるときに等倍へ戻します。
    /// </summary>
    public virtual Vector3? Scale => null;

    /// <summary>
    /// 最大 HP です。null ならバニラのままです。
    /// </summary>
    public virtual float? MaxHealth => null;

    /// <summary>
    /// スポーン時の体力です。既定は <see cref="MaxHealth"/> と同じ値です。
    /// </summary>
    public virtual float? Health => MaxHealth;

    /// <summary>
    /// ヒュームシールドの最大値です。null なら土台役職の設定を使います。
    /// </summary>
    public virtual float? MaxHumeShield => null;

    /// <summary>
    /// スポーン時のヒュームシールド値です。既定は <see cref="MaxHumeShield"/> (満タン) です。
    /// </summary>
    public virtual float? HumeShield => MaxHumeShield;

    /// <summary>
    /// ヒュームシールド回復速度の倍率です。土台に回復源がない役職では効きません。
    /// </summary>
    public virtual float HumeShieldRegenerationMultiplier => 1f;

    /// <summary>
    /// 支給アイテムです。1 つでも指定するとバニラの初期装備は付きません。
    /// </summary>
    public virtual IReadOnlyList<ItemType> Items => [];

    /// <summary>
    /// 支給するカスタムアイテムの型です。同じ型を複数並べれば、その個数だけ新規生成します。
    /// 文字列キーや ID ではなく <c>typeof(MyItem)</c> で指定してください。
    /// </summary>
    public virtual IReadOnlyList<Type> CustomItems => [];

    /// <summary>
    /// 支給する予備弾薬です。
    /// </summary>
    public virtual IReadOnlyDictionary<ItemType, ushort> Ammo => EmptyAmmo;

    /// <summary>
    /// スポーン時に付与する効果です。
    /// </summary>
    public virtual IReadOnlyList<RoleEffect> Effects => [];

    /// <summary>
    /// この役職の声の扱い (近接ボイスの可否と独自の伝達経路) です。
    /// </summary>
    public virtual RoleVoiceSettings Voice => RoleVoiceSettings.None;

    /// <summary>
    /// この役職だけに適用する定型文通信の上書きです。既定は <see cref="Team"/> の設定を引き継ぎます。
    /// </summary>
    public virtual CommunicationPolicy Communication => CommunicationPolicy.Inherit();

    /// <summary>
    /// ネームプレートに出す追加情報です。null なら変更しません。
    /// </summary>
    /// <remarks><see cref="CustomInfoDisplay"/> を通して表示します。</remarks>
    public virtual string CustomInfo => null;

    /// <summary>
    /// ネームプレートの組み立て方です。並び順や部隊名の扱いを変えたい役職だけ override します。
    /// </summary>
    public virtual CustomInfoDisplayOptions CustomInfoOptions => CustomInfoDisplayOptions.Default;

    /// <summary>
    /// HUD の役職欄に出す文字列です。既定はチーム色を巻いた <see cref="Name"/> です。
    /// </summary>
    public virtual string HudLabel => Team.Colorize(CustomSeason.Current.LocalizeScpText(Name));

    /// <summary>
    /// この役職で脱出したときの行き先です。null なら <see cref="CustomTeam.Escape"/> に任せます。
    /// </summary>
    public virtual SpawnSetRoleDefinition? Escape(EscapeContext escape) => null;

    /// <summary>
    /// HUD の陣営欄に出す文字列です。null ならチームが名乗るものを使います。
    /// 2 つの陣営にまたがる役職だけ override します。
    /// </summary>
    public virtual string TeamLabel => null;

    /// <summary>
    /// HUD の目的欄に出す文字列です。null なら陣営の既定を使います。
    /// </summary>
    public virtual string Objective => null;

    /// <summary>
    /// HUD の役職固有欄に出す状態です。null / 空なら何も出しません。
    /// </summary>
    /// <remarks>既定は <see cref="ShowStatus"/> の値を返します。毎秒読まれるので override 時は読むだけにしてください。</remarks>
    public virtual string Status => Time.time < statusExpiresAt ? status : null;

    /// <summary>
    /// このプレイヤーに紐づくコルーチン置き場です。役職が外れた時点でまとめて止まります。
    /// </summary>
    protected PlayerScope Scope => PlayerScope.Of(Player);

    /// <summary>
    /// 指定プレイヤーの現在のカスタム役職です。持っていなければ null。
    /// </summary>
    public static CustomRole Of(Player player)
    {
        if (player is null) return null;

        return ActiveRoles.TryGetValue(player.GetNetId(), out CustomRole role) ? role : null;
    }

    /// <summary>
    /// 指定プレイヤーが T の役職かどうか。
    /// </summary>
    public static bool Is<T>(Player player) where T : CustomRole => Of(player) is T;

    /// <summary>
    /// 現在有効なカスタム役職の一覧です。
    /// </summary>
    public static IReadOnlyCollection<CustomRole> Active => ActiveRoles.Values;

    /// <summary>
    /// いま有効な <typeparamref name="T"/> の役職インスタンス (派生含む) のスナップショットです。
    /// </summary>
    public static IReadOnlyList<T> AllOf<T>() where T : CustomRole => [.. ActiveRoles.Values.OfType<T>()];

    /// <summary>
    /// <typeparamref name="T"/> の役職を持っているプレイヤーです。
    /// </summary>
    public static IReadOnlyList<Player> PlayersOf<T>() where T : CustomRole =>
        [.. AllOf<T>().Select(role => role.Player).Where(player => player.IsSafePlayer())];

    /// <summary>
    /// 指定したアセンブリで定義された役職だけを解除します。
    /// </summary>
    public static void RemoveAssembly(Assembly assembly)
    {
        if (assembly is null) return;

        foreach (KeyValuePair<uint, CustomRole> entry in new List<KeyValuePair<uint, CustomRole>>(ActiveRoles))
        {
            if (entry.Value.GetType().Assembly == assembly)
                RemoveInternal(entry.Key, entry.Value);
        }
    }

    /// <summary>
    /// 全役職を解除し、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        RemoveAll();

        UnregisterGlobalEvents();
    }

    /// <summary>
    /// 新しいインスタンスを作って付与します。呼び出しごとに別インスタンスになります。
    /// </summary>
    public static T Spawn<T>(Player player) where T : CustomRole, new()
    {
        T role = new T();

        return role.Spawn(player) ? role : null;
    }

    /// <summary>
    /// 実行時に決まった型の役職を付与します。スポーンセットや脱出規則など、
    /// 外部データではなく型を値として持ち回る経路で使います。
    /// </summary>
    public static CustomRole Spawn(Type type, Player player)
    {
        if (type is null || !typeof(CustomRole).IsAssignableFrom(type) || type.IsAbstract)
        {
            Log.Error($"[Slafight] {type?.FullName ?? "null"} はカスタム役職として生成できません。");

            return null;
        }

        try
        {
            CustomRole role = (CustomRole)Activator.CreateInstance(type);

            return role.Spawn(player) ? role : null;
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] カスタム役職 {type.FullName} の生成に失敗しました: {exception}");

            return null;
        }
    }

    /// <summary>
    /// このプレイヤーの役職を解除します。
    /// </summary>
    public static void Remove(Player player)
    {
        if (player is null) return;

        uint netId = player.GetNetId();

        if (ActiveRoles.TryGetValue(netId, out CustomRole role))
            RemoveInternal(netId, role);
    }

    /// <summary>
    /// この役職をプレイヤーに付与します。
    /// </summary>
    public bool Spawn(Player player) => Spawn(player, out _);

    /// <summary>
    /// この役職をプレイヤーに付与します。失敗したときは理由を返します。
    /// </summary>
    public bool Spawn(Player player, out string failure)
    {
        failure = null;

        if (!player.IsSafePlayer())
        {
            failure = "対象が存在しません。";

            return false;
        }

        if (!player.IsNPC && !player.IsVerified)
        {
            failure = $"{player.Nickname} はまだ参加処理が終わっていません (認証待ち)。";

            return false;
        }

        uint netId = player.GetNetId();

        if (netId == 0)
        {
            failure = $"{player.Nickname} の netId が未割り当てです。";

            return false;
        }

        PreviousRole = player.Role.Type;

        // 以前の役職があれば先に片付ける。
        Remove(player);

        Player = player;
        NetId = netId;

        // バニラに任せる分だけフラグを立てる。
        // 装備を自分で配るならバニラの初期装備は要らないし、
        // 位置を指定するならバニラのスポーン地点も要らない。
        RoleSpawnFlags flags = RoleSpawnFlags.None;

        if (Items.Count == 0 && CustomItems.Count == 0)
            flags |= RoleSpawnFlags.AssignInventory;

        if (!SpawnPosition.HasValue)
            flags |= RoleSpawnFlags.UseSpawnpoint;

        player.Role.Set(BaseRole, SpawnReason.ForceClass, flags);

        if (player.Role.Type != BaseRole)
        {
            // ServerSetRole は ChangingRole が拒否されると何もせずに戻る。
            // 役職が変わっていない = 誰かに止められた、というのがほぼ唯一の筋。
            failure =
                $"{BaseRole} への役職変更が拒否されました " +
                $"(現在: {player.Role.Type})。他のハンドラが ChangingRole を止めている可能性があります。";

            Player = null;
            NetId = 0;

            return false;
        }

        ActiveRoles[netId] = this;
        RegisterGlobalEvents();

        Apply();

        try
        {
            eventsRegistered = true;
            RegisterEvents();
        }
        catch (Exception exception)
        {
            failure = $"{GetType().Name} のイベント登録に失敗しました: {exception.Message}";
            Log.Error($"[Slafight] {failure}\n{exception}");
            RemoveInternal(netId, this);
            return false;
        }

        try
        {
            OnSpawned();
            ShowHint();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name} のスポーン処理で例外が発生しました: {exception}");
        }

        return true;
    }

    /// <summary>役職付与後・OnSpawned の前に追加イベントを購読します。</summary>
    protected virtual void RegisterEvents() { }

    /// <summary>役職解除・退出・ラウンド再開時に追加イベントを解除します。Player はまだ参照できます。</summary>
    protected virtual void UnregisterEvents() { }

    /// <summary>この役職インスタンスを現在持っているプレイヤーかを判定します。</summary>
    public bool Check(Player player) => player.IsSafePlayer() &&
        ReferenceEquals(player.ReferenceHub, Player?.ReferenceHub) && ReferenceEquals(Of(player), this);

    /// <summary>ReferenceHub からこの役職の持ち主を判定します。</summary>
    public bool Check(ReferenceHub hub) => hub != null && Check(Exiled.API.Features.Player.Get(hub));

    /// <summary>LabAPI プレイヤーからこの役職の持ち主を判定します。</summary>
    public bool Check(LabApi.Features.Wrappers.Player player) => player is not null && Check(player.ReferenceHub);

    /// <summary>
    /// スポーン完了後に呼ばれます。能力やループ処理はここで <see cref="Scope"/> に載せてください。
    /// </summary>
    protected virtual void OnSpawned()
    {
    }

    /// <summary>
    /// 役職が外れたときに呼ばれます。
    /// <see cref="Scope"/> に載せたものは自動で止まるので、ここに書く必要はありません。
    /// </summary>
    protected virtual void OnRemoved()
    {
    }

    /// <summary>
    /// スポーン時に説明を出しておく秒数です。
    /// </summary>
    protected virtual float HintDuration => 5f;

    /// <summary>
    /// 役職の説明を本人にヒントで見せます。
    /// </summary>
    protected virtual void ShowHint()
    {
        Player.ShowHint(
            $"<size=24>{Team.Colorize(Name)}\n{Description}</size>",
            HintDuration);
    }

    /// <summary>
    /// このイベントの対象が自分かどうか。
    /// </summary>
    protected bool IsMine(Player other) => other is not null && IsMine(other.ReferenceHub);

    /// <summary>
    /// ReferenceHub でも比べられるようにします。
    /// </summary>
    protected bool IsMine(ReferenceHub hub) =>
        Player is not null && ReferenceEquals(hub, Player.ReferenceHub);

    /// <summary>
    /// 役職が続くあいだだけイベントを購読します。解除は <see cref="Scope"/> が引き受けるので、
    /// 死亡・役職変更・退出・ラウンド再開のどれでも取り残されません。
    /// </summary>
    protected void Hook(Action subscribe, Action unsubscribe)
    {
        subscribe();
        Scope.OnDispose(_ => unsubscribe());
    }

    /// <summary>
    /// <see cref="Status"/> に一定時間だけ値を置きます。
    /// </summary>
    protected void ShowStatus(string text, float duration)
    {
        status = text;
        statusExpiresAt = Time.time + Mathf.Max(0f, duration);
    }

    /// <summary>
    /// ヒュームシールドの最大値を決めて満タンにします。
    /// </summary>
    protected void SetHumeShield(float max)
    {
        ownsHumeShieldSettings = true;
        ownsHumeShieldMaximum = true;
        Player.MaxHumeShield = max;
        Player.HumeShield = max;
    }

    /// <summary>
    /// ヒュームシールドの回復速度に倍率を掛けます。
    /// </summary>
    protected void BoostHumeShieldRegen(float multiplier)
    {
        ownsHumeShieldSettings = true;
        Player.CustomHumeShieldStat.ShieldRegenerationMultiplier *= multiplier;
    }

    /// <summary>
    /// スポーン記述を実際に反映します。
    /// </summary>
    private void Apply()
    {
        if (SpawnPosition is { } position)
            Player.Position = position;

        if (SpawnRotation is { } rotation)
            Player.Rotation = rotation;

        if (Scale is { } scale)
            Player.Scale = scale;

        if (MaxHealth is { } maxHealth)
            Player.MaxHealth = maxHealth;

        if (Health is { } health)
            Player.Health = Mathf.Clamp(health, 0f, Player.MaxHealth);

        ApplyHumeShieldSettings();

        if (Items.Count > 0 || CustomItems.Count > 0)
        {
            Player.ClearInventory();

            foreach (ItemType item in Items)
            {
                Player.AddItem(item);
            }

            foreach (Type itemType in CustomItems)
            {
                CustomItem.Give(itemType, Player);
            }
        }

        foreach (KeyValuePair<ItemType, ushort> ammo in Ammo)
        {
            Player.SetAmmo(ammo.Key, ammo.Value);
        }

        foreach (RoleEffect effect in Effects)
        {
            effect.ApplyTo(Player);
        }

        if (CustomInfo is { } info)
            CustomInfoDisplay.Apply(Player, info, CustomInfoOptions);
    }

    private void ApplyHumeShieldSettings()
    {
        float? configuredMaximum = MaxHumeShield;
        float regenerationMultiplier = Mathf.Max(0f, HumeShieldRegenerationMultiplier);

        ownsHumeShieldMaximum = configuredMaximum.HasValue;
        ownsHumeShieldSettings = ownsHumeShieldMaximum ||
                                 !Mathf.Approximately(regenerationMultiplier, 1f);

        if (!ownsHumeShieldSettings)
            return;

        Player.HumeShieldRegenerationMultiplier = regenerationMultiplier;

        if (configuredMaximum is not { } maximum)
            return;

        maximum = Mathf.Max(0f, maximum);
        Player.MaxHumeShield = maximum;
        Player.HumeShield = Mathf.Clamp(HumeShield ?? maximum, 0f, maximum);
    }

    private void ReleaseHumeShieldSettings()
    {
        if (!ownsHumeShieldSettings || Player?.ReferenceHub is null)
            return;

        Player.HumeShieldRegenerationMultiplier = 1f;

        if (ownsHumeShieldMaximum)
        {
            Player.MaxHumeShield = float.MinValue;

            // 次の役職・所持品・効果が提供する通常上限へ戻す。プロバイダーがなければ 0 になる。
            IHumeShieldProvider.GetForHub(
                Player.ReferenceHub,
                out _,
                out float providerMaximum,
                out _,
                out _);
            Player.HumeShield = Mathf.Clamp(Player.HumeShield, 0f, providerMaximum);
        }

        ownsHumeShieldSettings = false;
        ownsHumeShieldMaximum = false;
    }

    private static void RemoveInternal(uint netId, CustomRole role)
    {
        ActiveRoles.Remove(netId);

        Player player = role.Player;

        if (role.eventsRegistered)
        {
            role.eventsRegistered = false;
            try
            {
                role.UnregisterEvents();
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] {role.GetType().Name} の UnregisterEvents で例外が発生しました: {exception}");
            }
        }

        if (player is not null)
            PlayerScope.Close(player);

        // 基底が書き換えたバニラ状態は基底が戻す。
        // ここに書いていないもの (体力・所持品・効果) は役職変更時にゲーム側が作り直す。
        if (player.IsSafePlayer())
        {
            role.ReleaseHumeShieldSettings();

            if (role.Scale is not null)
                player.Scale = Vector3.one;

            if (role.CustomInfo is not null)
                CustomInfoDisplay.Clear(player);
        }

        try
        {
            role.OnRemoved();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {role.GetType().Name} の OnRemoved で例外が発生しました: {exception}");
        }
    }

    private static void RemoveAll()
    {
        foreach (KeyValuePair<uint, CustomRole> entry in new List<KeyValuePair<uint, CustomRole>>(ActiveRoles))
        {
            RemoveInternal(entry.Key, entry.Value);
        }

        ActiveRoles.Clear();
    }

    private static void RegisterGlobalEvents()
    {
        if (globalEventsRegistered) return;

        globalEventsRegistered = true;

        LabPlayerEvents.ChangedRole += OnChangedRole;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RestartingRound += RemoveAll;
    }

    private static void UnregisterGlobalEvents()
    {
        if (!globalEventsRegistered) return;

        LabPlayerEvents.ChangedRole -= OnChangedRole;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RestartingRound -= RemoveAll;
        globalEventsRegistered = false;
    }

    /// <summary>LabApi の役職変更イベントを EXILED の <see cref="Player"/> に戻して処理します。</summary>
    private static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.Player?.ReferenceHub is not { } hub) return;
        if (Player.Get(hub) is not { } player) return;

        // 自分が付けた役職から別の役職に変わったら解除する。
        if (Of(player) is { } role && ev.NewRole.RoleTypeId != role.BaseRole)
        {
            Remove(player);
        }
    }

    private static void OnLeft(LeftEventArgs ev) => Remove(ev.Player);
}
