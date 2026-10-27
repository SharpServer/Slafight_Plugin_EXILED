namespace Slafight_Plugin_EXILED.API.Core.Enums;

/// <summary>カスタムキーカードの外見の種類です。種類ごとに設定できる項目が異なります。</summary>
public enum CustomKeycardType
{
    /// <summary>一般職員型。ラベル・所持者名・摩耗。</summary>
    Site02,

    /// <summary>管理職型。ラベル。</summary>
    Management,

    /// <summary>金属ケース型。ラベル・所持者名・摩耗・シリアル番号。</summary>
    MetalCase,

    /// <summary>機動部隊型。所持者名・シリアル番号・ランク。</summary>
    TaskForce,
}
