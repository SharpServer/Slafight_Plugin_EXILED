using System;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using Exiled.API.Enums;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 次に条件が合ったウェーブを、この波に差し替える 1 回きりの予約です。
/// </summary>
public sealed class SpawnOverride(SpawnSet wave)
{
    /// <summary>
    /// 差し替え先の波です。
    /// </summary>
    public SpawnSet Wave { get; } = wave;

    /// <summary>
    /// この陣営のウェーブにだけ効かせます。null ならどの陣営でも。
    /// </summary>
    public Faction? SourceFaction { get; set; }

    /// <summary>
    /// この <see cref="SpawnableFaction"/> のウェーブにだけ効かせます。null ならどれでも。
    /// </summary>
    public SpawnableFaction? SourceSpawnableFaction { get; set; }

    /// <summary>
    /// 本隊 / 増援のどちらに効かせるかです。null ならどちらでも。
    /// </summary>
    public bool? SourceIsMiniWave { get; set; }

    /// <summary>
    /// 上の条件だけで足りないときの追加判定です。
    /// </summary>
    public Func<RespawningTeamEventArgs, bool> Predicate { get; set; }

    /// <summary>
    /// 同時に条件を満たした予約のうち、大きいものが先に使われます。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// このウェーブに適用できるか。
    /// </summary>
    internal bool Matches(RespawningTeamEventArgs ev, Faction faction, bool miniWave)
    {
        if (SourceFaction.HasValue && faction != SourceFaction.Value)
            return false;

        if (SourceIsMiniWave.HasValue && miniWave != SourceIsMiniWave.Value)
            return false;

        if (SourceSpawnableFaction.HasValue &&
            (ev?.Wave is null || ev.Wave.SpawnableFaction != SourceSpawnableFaction.Value))
            return false;

        // ForceSpawn 経由では元イベントが無いので、それを見る述語は成立させない。
        if (Predicate is not null && (ev is null || !Predicate(ev)))
            return false;

        return true;
    }
}
