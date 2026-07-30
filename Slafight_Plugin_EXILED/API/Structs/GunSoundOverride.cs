#nullable enable

namespace Slafight_Plugin_EXILED.API.Structs;

/// <summary>
/// 銃器の 1 種類の音に対する差し替え定義。
/// <c>CItemWeapon.OverrideSoundsByChannel</c> / <c>OverrideSoundsByClip</c> /
/// <c>OverrideSoundsByIndex</c> の値として使う。
/// </summary>
/// <remarks>
/// <para>
/// バニラ音は定義がある限り常に抑制される。<see cref="Audio"/> が null の場合は
/// 代替音を鳴らさず「消すだけ」になる（<see cref="Silent"/> と同義）。
/// </para>
/// <para>
/// 各チューニング値は null なら CItemWeapon 側の既定値
/// (<c>OverrideAudioRange</c> 等) にフォールバックする。
/// </para>
/// </remarks>
public sealed record GunSoundOverride(
    string? Audio,
    float? Range = null,
    float? Volume = null,
    bool? IsSpatial = null,
    int? Voices = null,
    float? MinInterval = null)
{
    /// <summary>バニラ音を抑制するだけで代替音を鳴らさない定義。</summary>
    public static readonly GunSoundOverride Silent = new(null);

    /// <summary>ファイル名だけを指定する簡易記法。チューニング値は CItemWeapon の既定値を使う。</summary>
    public static implicit operator GunSoundOverride(string audio) => new(audio);
}
