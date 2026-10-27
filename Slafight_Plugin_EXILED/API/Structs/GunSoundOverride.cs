#nullable enable

namespace Slafight_Plugin_EXILED.API.Structs;

/// <summary>
/// 銃器の 1 種類の音の差し替え定義。定義した音のバニラ音は常に抑制される。
/// </summary>
/// <param name="Audio">鳴らす音声ファイル名 (<c>AudioReferences</c> 配下)。null なら消すだけ。</param>
/// <param name="Range">可聴距離。</param>
/// <param name="Volume">音量。</param>
/// <param name="IsSpatial">3D 空間音として鳴らすか。</param>
/// <param name="Voices">プレイヤー 1 人あたりのスピーカー数。</param>
/// <param name="MinInterval">同じ音の要求がこの秒数より短く続いたら捨てる。</param>
public sealed record GunSoundOverride(
    string? Audio,
    float Range = 15f,
    float Volume = 1f,
    bool IsSpatial = true,
    int Voices = 2,
    float MinInterval = 0.04f)
{
    /// <summary>バニラ音を抑制するだけで代替音を鳴らさない定義。</summary>
    public static readonly GunSoundOverride Silent = new((string?)null);

    /// <summary>ファイル名だけを指定する簡易記法。</summary>
    public static implicit operator GunSoundOverride(string audio) => new(audio);
}
