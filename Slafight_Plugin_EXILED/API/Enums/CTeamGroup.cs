namespace Slafight_Plugin_EXILED.API.Enums;

/// <summary>
/// Profile 内で <see cref="CTeam"/> を勝利判定用にまとめる単位。
/// </summary>
public enum CTeamGroup
{
    /// <summary>
    /// 無効値。通常は Profile 定義では使わず、未初期化や不明値として扱います。
    /// </summary>
    Null,

    /// <summary>
    /// Profile 表には明示するが、Group 勝利判定には参加させないチーム。
    /// <see cref="Undefined"/> のチームは <see cref="CTeam"/> 個別の勝利・終了定義を尊重します。
    /// </summary>
    Undefined,

    /// <summary>
    /// 財団側として扱う勝利グループ。
    /// </summary>
    Foundation,

    /// <summary>
    /// インサージェンシー側として扱う勝利グループ。
    /// </summary>
    Insurgency,

    /// <summary>
    /// FacilityTermination などで、人類側として扱う勝利グループ。
    /// </summary>
    Humanity,

    /// <summary>
    /// FacilityTermination などで、正常性側として扱う勝利グループ。
    /// </summary>
    Normalcy,
}
