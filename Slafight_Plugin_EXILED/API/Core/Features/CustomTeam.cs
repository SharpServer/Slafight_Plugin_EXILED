using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 陣営です。所属判定と勝利条件を持ち、型そのものが識別子になります。
/// </summary>
public abstract class CustomTeam
{
    private static readonly Dictionary<Type, CustomTeam> Instances = new Dictionary<Type, CustomTeam>();

    private static CustomTeam[] all;

    /// <summary>
    /// 宣言されているすべてのチームです。型を探して 1 つずつ生成し、以後は使い回します。
    /// </summary>
    public static IReadOnlyList<CustomTeam> All => all ??= Build();

    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 表示色です。
    /// </summary>
    public virtual string Color => "#FFFFFF";

    /// <summary>
    /// 勝利判定で同じ側として数えるチームです。
    /// 片側にだけ書いても <see cref="IsSameSide"/> は両方向で成立します。
    /// </summary>
    public virtual IReadOnlyList<CustomTeam> Allies => [];

    /// <summary>
    /// 勝利条件です。null を返すと勝利側になりません (中立・観戦向け)。
    /// </summary>
    public virtual VictoryCondition Victory => null;

    /// <summary>
    /// C.A.S.S.I.E に読ませる綴りです。既定は表示名そのまま。
    /// 季節で変えるなら <see cref="Seasonal.Of{T}"/> で書きます。
    /// </summary>
    public virtual string CassieName => Name;

    /// <summary>
    /// 放送の字幕に出す名前です。既定は <see cref="Name"/>。
    /// HUD や勝利表示は変えずに、放送だけ季節で呼び名を変えたいときに使います。
    /// </summary>
    public virtual string SubtitleName => Name;

    /// <summary>
    /// 財団の通常勢力ではなく、独立勢力・敵対勢力として扱うか。
    /// </summary>
    public virtual bool IsGroupOfInterest => true;

    /// <summary>
    /// この陣営の定型文通信設定です。Prefix の既定は <see cref="Name"/> です。
    /// </summary>
    public virtual CommunicationPolicy Communication => CommunicationPolicy.Inherit(Name);

    /// <summary>
    /// バニラのラウンド終了処理に任せるか。
    /// false のときは <see cref="VictoryText"/> を全員に出して手動で終わらせます。
    /// </summary>
    public virtual bool UsesVanillaEnding => true;

    /// <summary>
    /// 独自終了のときに全員へ出す文言です。
    /// </summary>
    public virtual string VictoryText => $"<b><size=80><color={Color}>{Name}</color>の勝利</size></b>";

    /// <summary>
    /// 蘇生させたとき、この陣営が仲間として何を立てるかです。
    /// null なら蘇生させた側の役職をそのまま複製します。
    /// </summary>
    public virtual SpawnSetRoleDefinition? Resurrection => null;

    /// <summary>
    /// この陣営のプレイヤーが脱出したときの行き先です。null なら脱出しません。
    /// </summary>
    /// <remarks>役職の <see cref="CustomRole.Escape"/> が優先されます。</remarks>
    public virtual SpawnSetRoleDefinition? Escape(EscapeContext escape) => null;

    /// <summary>
    /// 表示層が陣営欄に出す表記です。既定は陣営色を付けた表示名。
    /// </summary>
    public virtual string HudName => this.Colorize(Name);

    /// <summary>
    /// 陣営の既定の目的です。役職が <see cref="CustomRole.Objective"/> を名乗らないときに使われます。
    /// </summary>
    public virtual string Objective => string.Empty;

    /// <summary>
    /// 表示層に味方一覧を出す陣営かどうか。既定は false です。
    /// </summary>
    public virtual bool ShowsRoster => false;

    /// <summary>
    /// 味方一覧の末尾に添える、この陣営の状況表示です。<paramref name="viewer"/> は画面の持ち主です。
    /// </summary>
    public virtual string RosterFooter(Player viewer) => string.Empty;

    /// <summary>
    /// 現在このチームに属している生存プレイヤーです。
    /// </summary>
    public IEnumerable<Player> Members =>
        Player.List.Where(player => player.IsSafePlayer() && player.IsAlive && Includes(player));

    /// <summary>
    /// 指定したチームのインスタンスを返します。全体で 1 つだけ生成されます。
    /// </summary>
    public static T Get<T>() where T : CustomTeam, new()
    {
        if (Instances.TryGetValue(typeof(T), out CustomTeam cached))
            return (T)cached;

        T instance = new T();
        Instances[typeof(T)] = instance;

        // All を作り直させて、明示生成した分も一覧に載るようにする。
        all = null;

        return instance;
    }

    /// <summary>
    /// 実行時に決まった型のチームインスタンスを返します。
    /// コマンドや設定など、型を値として持ち回る経路から使います。
    /// </summary>
    public static CustomTeam Get(Type type)
    {
        if (type is null || !typeof(CustomTeam).IsAssignableFrom(type) || type.IsAbstract)
        {
            Log.Error($"[Slafight] {type?.FullName ?? "null"} はチームとして生成できません。");

            return null;
        }

        if (Instances.TryGetValue(type, out CustomTeam cached))
            return cached;

        try
        {
            CustomTeam instance = (CustomTeam)Activator.CreateInstance(type);
            Instances[type] = instance;

            // All を作り直させて、明示生成した分も一覧に載るようにする。
            all = null;

            return instance;
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] チーム {type.FullName} を生成できませんでした: {exception}");

            return null;
        }
    }

    /// <summary>
    /// このプレイヤーが属しているチームです。どこにも属していなければ null。
    /// <see cref="CustomRole.Of"/> と対になります。
    /// </summary>
    public static CustomTeam Of(Player player)
    {
        if (!player.IsSafePlayer()) return null;

        return All.FirstOrDefault(team => team.Includes(player));
    }

    /// <summary>
    /// 2 人が勝敗判定上おなじ側かどうか。
    /// どちらかがチームに属していない場合は、ゲーム本体の陣営で判定します。
    /// </summary>
    public static bool AreAllies(Player self, Player other)
    {
        if (self is null || other is null) return false;
        if (ReferenceEquals(self, other)) return true;

        if (Of(self) is { } mine && Of(other) is { } theirs)
            return mine.IsSameSide(theirs);

        return self.Role.Side == other.Role.Side;
    }

    /// <summary>
    /// 条件を満たしているチームのうち、最も優先度の高いものを返します。
    /// 満たしているチームが無ければ null。
    /// </summary>
    public static CustomTeam FindWinner()
    {
        return All
            .Where(team => team.Victory is { } victory && victory.IsMet(team))
            .OrderByDescending(team => team.Victory.Priority)
            .FirstOrDefault();
    }

    /// <summary>
    /// カスタム役職を持たないプレイヤーが、この陣営に属するかを判定します。
    /// </summary>
    protected abstract bool IncludesVanilla(Player player);

    /// <summary>
    /// このプレイヤーがこのチームに属するかを判定します。カスタム役職があればそのチーム、なければバニラ役職で判定します。
    /// </summary>
    public bool Includes(Player player)
    {
        if (!player.IsSafePlayer()) return false;

        // 役職が自分でチームを名乗っているなら、それが唯一の正解。
        if (CustomRole.Of(player) is { } role)
            return ReferenceEquals(role.Team, this);

        return IncludesVanilla(player);
    }

    /// <summary>
    /// 勝利判定で同じ側として扱うかを判定します。自分自身と <see cref="Allies"/> が該当します。
    /// </summary>
    public bool IsSameSide(CustomTeam other)
    {
        if (other is null) return false;
        if (ReferenceEquals(other, this)) return true;

        // 同盟は片方向に書かれていることが多いので両側から見る。
        return Allies.Contains(other) || other.Allies.Contains(this);
    }

    /// <summary>
    /// 勝因の表示テキストです。<see cref="VictoryCondition.Reason"/> が未指定ならチーム名から作ります。
    /// </summary>
    public string GetVictoryReason() => Victory?.Reason ?? $"{Name} の勝利";

    private static CustomTeam[] Build()
    {
        foreach (Type type in TypeParser.FindTypes<CustomTeam>())
        {
            if (Instances.ContainsKey(type)) continue;

            try
            {
                Instances[type] = (CustomTeam)Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] チーム {type.FullName} を生成できませんでした: {exception}");
            }
        }

        return [.. Instances.Values];
    }
}
