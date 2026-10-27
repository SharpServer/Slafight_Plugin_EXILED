namespace Slafight_Plugin_EXILED.API.Enums;

/// <summary>
/// 銃器サウンドの種類。実行時のクリップ参照とモジュール状態から判定する (<see cref="Features.GunSoundResolver"/>)。
/// </summary>
public enum GunSoundKind
{
    /// <summary>
    /// 発砲音。<see cref="AudioPooling.MixerChannel.Weapons"/> チャンネルで判定する。
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
    /// 抜き出し中 (<c>IEquipperModule.IsEquipped</c> が false の間) に鳴った音すべて。
    /// </summary>
    /// <remarks>抜き出し音を 1 つだけ狙うならクリップ名で指定すること。</remarks>
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
