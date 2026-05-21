using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

/// <summary>
/// <see cref="CTeam"/> に紐づく表示名・Cassie 読み・色・分類情報を表します。
/// </summary>
public sealed class CTeamDefinition
{
    /// <summary>
    /// チーム定義を作成します。
    /// </summary>
    /// <param name="team">対象のカスタムチーム。</param>
    /// <param name="name">HUD やログなどで使う表示名。</param>
    /// <param name="cassie">Cassie に読ませるための文字列。</param>
    /// <param name="color">チーム表示色。Unity rich text の color 値として使える文字列を想定します。</param>
    /// <param name="isGoI">財団側通常勢力ではなく、独立勢力または敵対勢力として扱うか。</param>
    public CTeamDefinition(
        CTeam team,
        string name,
        string cassie,
        string color,
        bool isGoI)
    {
        Team = team;
        Name = name;
        Cassie = cassie;
        Color = color;
        IsGoI = isGoI;
    }

    /// <summary>
    /// 対象のカスタムチーム。
    /// </summary>
    public CTeam Team { get; }

    /// <summary>
    /// HUD やログなどで使う表示名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Cassie に読ませるための文字列。
    /// </summary>
    public string Cassie { get; }

    /// <summary>
    /// チーム表示色。Unity rich text の color 値として使える文字列を想定します。
    /// </summary>
    public string Color { get; }

    /// <summary>
    /// 財団側通常勢力ではなく、独立勢力または敵対勢力として扱うか。
    /// </summary>
    public bool IsGoI { get; }
}
