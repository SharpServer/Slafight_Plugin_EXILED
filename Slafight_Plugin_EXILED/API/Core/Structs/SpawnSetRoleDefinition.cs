using System;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// <see cref="SpawnSet"/> に並べる 1 行分の役職定義です。count が人数上限、Weight が出やすさです。
/// </summary>
public readonly struct SpawnSetRoleDefinition
{
    private readonly Func<Player, bool> spawn;

    private readonly Func<SpawnRoleContext, bool> condition;

    private SpawnSetRoleDefinition(
        Func<Player, bool> spawn,
        int count,
        bool isForced,
        float weight,
        Func<SpawnRoleContext, bool> condition = null)
    {
        this.spawn = spawn;
        this.condition = condition;
        Count = count;
        IsForced = isForced;
        Weight = weight;
    }

    /// <summary>
    /// この行を何人まで割り当てるかです。上限の役割も兼ねます。
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 重み抽選を通さず、他の行より先に 1 人ずつ確保するかどうかです。
    /// </summary>
    public bool IsForced { get; }

    /// <summary>
    /// 抽選の重みです。既定の 1 なら他の行と等確率。
    /// 0 以下にすると <see cref="IsForced"/> でない限り選ばれません。
    /// </summary>
    public float Weight { get; }

    /// <summary>
    /// バニラ役職を割り当てます。
    /// </summary>
    public static SpawnSetRoleDefinition Vanilla(
        RoleTypeId role,
        int count = 1,
        bool isForced = false,
        float weight = 1f)
    {
        return new SpawnSetRoleDefinition(
            player =>
            {
                player.Role.Set(role);

                return true;
            },
            count,
            isForced,
            weight);
    }

    /// <summary>
    /// カスタム役職を割り当てます。割り当てごとに新しいインスタンスが作られます。
    /// </summary>
    public static SpawnSetRoleDefinition Custom<T>(
        int count = 1,
        bool isForced = false,
        float weight = 1f)
        where T : CustomRole, new()
    {
        return new SpawnSetRoleDefinition(
            player => CustomRole.Spawn<T>(player) is not null,
            count,
            isForced,
            weight);
    }

    /// <summary>
    /// 条件を満たす間だけ抽選枠に入れます。重ねると AND になります。
    /// </summary>
    public SpawnSetRoleDefinition When(Func<SpawnRoleContext, bool> predicate)
    {
        if (predicate is null)
            return this;

        Func<SpawnRoleContext, bool> previous = condition;

        return new SpawnSetRoleDefinition(
            spawn,
            Count,
            IsForced,
            Weight,
            previous is null ? predicate : context => previous(context) && predicate(context));
    }

    /// <summary>
    /// この実行で <paramref name="count"/> 人割り当てた後から抽選枠に入れます。
    /// <c>AfterSpawned(1)</c> なら 2 人目以降。
    /// </summary>
    public SpawnSetRoleDefinition AfterSpawned(int count) => When(context => context.SpawnedCount >= count);

    /// <summary>
    /// この実行の総人数が <paramref name="count"/> 人以上のときだけ抽選枠に入れます。
    /// </summary>
    public SpawnSetRoleDefinition MinPlanned(int count) => When(context => context.PlannedCount >= count);

    /// <summary>
    /// 条件を満たしているか。条件が無ければ常に true。
    /// </summary>
    public bool Allows(SpawnRoleContext context) => condition is null || condition(context);

    /// <summary>
    /// 割り当てを実行します。
    /// </summary>
    public bool SpawnFor(Player player) => spawn is not null && spawn(player);
}
