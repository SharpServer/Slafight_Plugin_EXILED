using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// <see cref="SpawnSetRoleDefinition.When"/> の条件に渡される、割り当て途中の状況です。
/// </summary>
public readonly struct SpawnRoleContext(SpawnSet set, int spawnedCount, int plannedCount, int rowCount)
{
    /// <summary>
    /// 割り当て中の SpawnSet です。
    /// </summary>
    public SpawnSet Set { get; } = set;

    /// <summary>
    /// この実行で既に割り当てた人数です。0 なら 1 人目を選んでいるところ。
    /// </summary>
    public int SpawnedCount { get; } = spawnedCount;

    /// <summary>
    /// この実行で割り当てる予定の総人数です。
    /// </summary>
    public int PlannedCount { get; } = plannedCount;

    /// <summary>
    /// この行に既に割り当てた人数です。
    /// </summary>
    public int RowCount { get; } = rowCount;
}
