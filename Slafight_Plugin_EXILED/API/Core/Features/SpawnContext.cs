using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 「いまどの波が出やすいか」を決める状況です。
///
/// 波そのもの (<see cref="SpawnSet"/>) は自分の既定の重みだけを名乗り、
/// 状況ごとの上書きはこちらが持ちます。
/// </summary>
/// <example>
/// <code>
/// public sealed class FacilityTerminationContext : SpawnContext
/// {
///     public override string Name => "Facility Termination";
///
///     // 挙げた波以外は 0。base に落とさないので、想定外の波が混ざらない。
///     public override int WeightOf(SpawnSet wave) => wave switch
///     {
///         LastOperationWave => 100,
///         ChaosRaidWave     => 30,
///         _                 => 0,
///     };
/// }
///
/// SpawnContext.Activate&lt;FacilityTerminationContext&gt;();
/// </code>
/// </example>
public abstract class SpawnContext
{
    private static SpawnSet[] waves;

    /// <summary>
    /// 現在の状況です。既定は <see cref="DefaultContext"/>。
    /// </summary>
    public static SpawnContext Active { get; private set; } = new DefaultContext();

    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 宣言されている波をすべて返します。
    /// </summary>
    /// <remarks>
    /// 波は状態を持たない宣言なので、1 度だけ生成して使い回します。
    /// 重みが 0 以下の定義も保持します。抽選対象かどうかは現在の状況の重みで決まります。
    /// 手動実行では重みに関係なくすべての定義を呼び出せます。
    /// </remarks>
    public static IReadOnlyList<SpawnSet> AllWaves => waves ??= BuildWaves();

    /// <summary>
    /// 状況を切り替えます。
    /// </summary>
    public static void Activate<T>() where T : SpawnContext, new() => Active = new T();

    /// <summary>
    /// 生成済みの状況に切り替えます。
    /// 実行時に型が決まる経路 (コマンド・マップデータ) で使います。
    /// </summary>
    public static void Apply(SpawnContext context) => Active = context ?? new DefaultContext();

    /// <summary>
    /// 既定の状況に戻します。
    /// </summary>
    public static void Reset() => Active = new DefaultContext();

    /// <summary>
    /// 型で波を引きます。
    /// </summary>
    public static SpawnSet Wave<T>() where T : SpawnSet => AllWaves.FirstOrDefault(wave => wave is T);

    /// <summary>
    /// 名前で波を引きます。RA コマンドやマップデータのように、文字列から指す経路で使います。
    /// </summary>
    public static SpawnSet Find(string name)
    {
        return TypeParser.TryParse<SpawnSet>(name, out Type type)
            ? AllWaves.FirstOrDefault(wave => wave.GetType() == type)
            : null;
    }

    /// <summary>
    /// 生成済みの波の一覧を捨てます。次に読まれたときに作り直されます。
    /// </summary>
    public static void Invalidate() => waves = null;

    /// <summary>
    /// この状況での重みです。既定は波が名乗るとおり。
    /// </summary>
    public virtual int WeightOf(SpawnSet wave) => wave?.RespawnWeight ?? 0;

    /// <summary>
    /// 条件に合う波から重み付き抽選で 1 つ選びます。候補が無ければ null。
    /// </summary>
    /// <param name="chooseIndex">0 以上・引数未満の整数を返すもの。試験で固定したいときに差し替えます。</param>
    public SpawnSet Roll(Faction faction, bool miniWave, Func<int, int> chooseIndex)
    {
        List<SpawnSet> candidates =
        [
            .. AllWaves
                .Where(wave => wave.RespawnFaction == faction &&
                               wave.IsMiniWave == miniWave &&
                               WeightOf(wave) > 0)
        ];

        if (candidates.Count == 0) return null;

        int total = candidates.Sum(WeightOf);
        int roll = chooseIndex is null ? UnityEngine.Random.Range(0, total) : chooseIndex(total);

        foreach (SpawnSet wave in candidates)
        {
            roll -= WeightOf(wave);

            if (roll < 0) return wave;
        }

        return candidates[candidates.Count - 1];
    }

    private static SpawnSet[] BuildWaves()
    {
        List<SpawnSet> collected = [];

        foreach (Type type in TypeParser.FindTypes<SpawnSet>())
        {
            try
            {
                SpawnSet candidate = (SpawnSet)Activator.CreateInstance(type);

                collected.Add(candidate);
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] ウェーブ {type.FullName} を生成できませんでした: {exception}");
            }
        }

        return [.. collected];
    }
}

/// <summary>
/// 何も上書きしない既定の状況です。波はそれぞれの <see cref="SpawnSet.RespawnWeight"/> どおりに出ます。
/// </summary>
public sealed class DefaultContext : SpawnContext
{
    public override string Name => "Default";
}
