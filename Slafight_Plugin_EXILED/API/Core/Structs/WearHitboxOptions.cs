using System;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Objects;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// 着せた Schematic の当たり判定 1 グループの指定です。
/// </summary>
public sealed class WearHitboxOptions
{
    /// <summary>
    /// 省略時のグループ名です。
    /// </summary>
    public const string DefaultGroup = "Wear";

    /// <summary>
    /// CustomHitbox のグループ名です。Unity 側で同名のグループを作ってあれば、そのブロックを使います。
    /// </summary>
    public string Group { get; init; } = DefaultGroup;

    /// <summary>
    /// 体のどの部位として扱うかです。銃の部位倍率に効きます。
    /// </summary>
    public HitboxType HitboxType { get; init; } = HitboxType.Body;

    /// <summary>
    /// 体へ流すダメージの倍率です。
    /// </summary>
    public float DamageMultiplier { get; init; } = 1f;

    /// <summary>
    /// 当たり判定にするブロックの条件です。null ならすべての Primitive です。
    /// </summary>
    public Predicate<SchematicBlock> Filter { get; init; }

    internal bool Matches(SchematicBlock block) =>
        Filter?.Invoke(block) ?? block.BlockType == BlockType.Primitive;
}
