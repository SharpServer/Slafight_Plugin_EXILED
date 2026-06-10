using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Features;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.API.Objects;

/// <summary>
/// 重み付きロール
/// </summary>
public readonly record struct WeightedRoleEntry(object Role, float Weight);

/// <summary>
/// 重み付きロール一覧からランダムに1つ選択
/// </summary>
public static class WeightedRole
{
    public static object? Choose(List<WeightedRoleEntry>? source)
    {
        if (source == null || source.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var item in source)
            totalWeight += item.Weight;

        if (totalWeight <= 0f)
            return null;

        float random = Random.value * totalWeight;
        float current = 0f;

        foreach (var item in source)
        {
            current += item.Weight;
            if (random <= current)
                return item.Role;
        }

        return source[^1].Role;
    }
}

/// <summary>
/// 1 ロールの上限情報
/// </summary>
public readonly record struct RoleLimitEntry(object Role, int Limit);

/// <summary>
/// 1 モード分のロール制限集合
/// </summary>
public readonly record struct RoleLimitPool(List<RoleLimitEntry> Limits);

/// <summary>
/// 1 モード分のロールセット（SCP＋人間全種）
/// </summary>
public record struct RoleTablePool(
    List<WeightedRoleEntry> ScpRoles,
    List<WeightedRoleEntry> ScientistRoles,
    List<WeightedRoleEntry> GuardRoles,
    List<WeightedRoleEntry> ClassDRoles
);

/// <summary>
/// 動的なロールテーブル・ロール上限の切り替え用。
/// 実体は API.Features.RoleTableContextRegistry に集約する。
/// </summary>
public static class RoleTables
{
    public static RoleTableContext CurrentContext => RoleTableContextRegistry.ActiveContext;

    /// <summary>
    /// コンテキストを登録する。既存名の場合は上書きする。
    /// </summary>
    public static void Register(RoleTableContext context)
    {
        RoleTableContextRegistry.Register(context);
    }

    /// <summary>
    /// モードを切り替える（例: "Normal", "April"）
    /// </summary>
    public static void SetCurrentMode(string mode)
        => RoleTableContextRegistry.SetActive(mode);

    /// <summary>
    /// 現在のモードに対応するロールセット（テーブル）を返す
    /// </summary>
    public static RoleTablePool GetCurrentTablePool()
        => CurrentContext.Tables;

    /// <summary>
    /// 現在のモードに対応するロール上限セットを返す
    /// </summary>
    public static RoleLimitPool GetCurrentLimitPool()
        => CurrentContext.LimitPool;

    /// <summary>
    /// SCP ロールテーブル（重み付き）を動的に返す
    /// </summary>
    public static List<WeightedRoleEntry> GetScpRoles()
        => CurrentContext.ScpRoles;

    /// <summary>
    /// 科学者系ロールテーブル
    /// </summary>
    public static List<WeightedRoleEntry> GetScientistRoles()
        => CurrentContext.ScientistRoles;

    /// <summary>
    /// 警備員系ロールテーブル
    /// </summary>
    public static List<WeightedRoleEntry> GetGuardRoles()
        => CurrentContext.GuardRoles;

    /// <summary>
    /// Class-D 系ロールテーブル
    /// </summary>
    public static List<WeightedRoleEntry> GetClassDRoles()
        => CurrentContext.ClassDRoles;
}