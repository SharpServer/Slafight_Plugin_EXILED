namespace Slafight_Plugin_EXILED.API.Enums;

/// <summary>
/// 銃器サウンドのうち、ゲーム側が名前付きフィールドとして持っているものの種類。
/// </summary>
/// <remarks>
/// <para>
/// 各値は実行時に <c>Assembly-CSharp</c> のモジュールから
/// <see cref="UnityEngine.AudioClip"/> の参照を直接引いて判定する
/// （<see cref="Features.GunSoundResolver"/> 参照）。AudioIndex の意味を列挙した
/// 外部の対応表には依存しないので、銃種が追加されても壊れない。
/// </para>
/// <para>
/// マガジン挿入・ボルト操作などの音はゲーム側でも名前付きフィールドを持たず、
/// 銃器プレハブのアニメーションイベントから <c>AudioModule.PlayQuiet</c> /
/// <c>PlayNormal</c> に渡されるだけ。ただしそれらは
/// <c>IReloaderModule.IsReloading</c> / <c>IsUnloading</c> や
/// <c>IEquipperModule.IsEquipped</c> といったモジュールの状態から
/// <see cref="Reload"/> / <see cref="Unload"/> / <see cref="Equip"/> として一括で判定できる。
/// </para>
/// <para>
/// リロード中の音を 1 つずつ別クリップに分けたい場合だけ、
/// <c>CItemWeapon.OverrideSoundsByClip</c>（クリップ名指定）か
/// <c>OverrideSoundsByIndex</c> を使う。
/// </para>
/// </remarks>
public enum GunSoundKind
{
    /// <summary>
    /// 発砲音。<c>AudioModule.PlayGunshot</c> のみが
    /// <see cref="AudioPooling.MixerChannel.Weapons"/> を使うため、チャンネルだけで判定できる。
    /// サプレッサーの有無や弾種でクリップが変わっても全銃種で成立する。
    /// </summary>
    Gunshot,

    /// <summary>空撃ち音。各アクションモジュールの dry fire クリップ。</summary>
    DryFire,

    /// <summary>
    /// リロード中に鳴る音（マガジン抜き差し・ボルト操作など）。
    /// <c>IReloaderModule.IsReloading</c> が true の間に鳴った音をまとめて指す。
    /// </summary>
    Reload,

    /// <summary>
    /// アンロード中に鳴る音。<c>IReloaderModule.IsUnloading</c> が true の間に鳴った音。
    /// </summary>
    Unload,

    /// <summary>
    /// 抜き出しアニメーション中に鳴った音。
    /// <c>IEquipperModule.IsEquipped</c> がまだ false の間に鳴った音すべてを指す。
    /// </summary>
    /// <remarks>
    /// 「抜き出し音そのもの」ではなく「抜き出し中に鳴った音」なので、範囲が広い。実測例:
    /// ショットガンでは <c>Shotgun Reload Transition</c> / <c>Shotgun PumpIn</c> / <c>PumpOut</c> が、
    /// Crossvec では <c>CrossvecEquipStock</c> に加えて <c>CrossvecAdsDown</c> が、
    /// Logicer では <c>Log Handling Reload Start</c> がこれに含まれた。
    /// 同じ <c>Shotgun PumpIn</c> でも戦闘中のポンプ操作は分類されない（Kind なし）。
    /// 抜き出し音を 1 つだけ狙うなら <c>CItemWeapon.OverrideSoundsByClip</c> を使うこと。
    /// </remarks>
    Equip,

    /// <summary>リボルバーのダブルアクション音。</summary>
    RevolverDoubleAction,

    /// <summary>リボルバーのコッキング音。</summary>
    RevolverCocking,

    /// <summary>リボルバーのデコッキング音。</summary>
    RevolverDecocking,

    /// <summary>ParticleDisruptor の射撃アクション音（発砲音とは別に鳴るチャージ / 排莢系の音）。</summary>
    DisruptorAction,
}
