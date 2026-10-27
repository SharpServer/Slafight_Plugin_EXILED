namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 部隊内での階級です。<see cref="Alone"/> は表示上の状態で、保持はされません。
/// </summary>
public enum ForceClassLevel
{
    /// <summary>
    /// 隊長級。本隊を率います。一人になっても本隊であり続けます。
    /// </summary>
    TopLead,

    /// <summary>
    /// 軍曹・補佐官級。分隊編成時のバフを強化します。
    /// </summary>
    SubLead,

    /// <summary>
    /// 部隊を構成する一般的な隊員です。
    /// </summary>
    Member,

    /// <summary>
    /// 隊から外れて行動していて、分隊も組めていない状態です。
    /// </summary>
    Alone,
}
