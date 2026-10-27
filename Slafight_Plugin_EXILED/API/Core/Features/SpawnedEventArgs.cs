using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Respawning.NamingRules;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 波を出した直後の情報です。曲やアナウンスはこれを拾って出します。
/// </summary>
public sealed class SpawnedEventArgs(SpawnSet wave, IReadOnlyList<Player> players, byte? unitId, string unitName) : EventArgs
{
    /// <summary>
    /// 実際に出た波です。
    /// </summary>
    public SpawnSet Wave { get; } = wave;

    /// <summary>
    /// この波で実際に役職を割り当てられたプレイヤーです (割り当て順)。
    /// </summary>
    public IReadOnlyList<Player> Players { get; } = players ?? [];

    /// <summary>
    /// この波の全員に割り当てた部隊番号です。FoundationStaff の波でなければ null。
    /// </summary>
    public byte? UnitId { get; } = unitId;

    /// <summary>
    /// この波に付いた部隊名 (<c>ALPHA-01</c> 形式) です。FoundationStaff の波でなければ null。
    /// </summary>
    public string UnitName { get; } = unitName;

    /// <summary>
    /// 実際に割り当てた人数です。
    /// </summary>
    public int SpawnCount => Players.Count;
}
