#nullable enable

using System;
using AudioPooling;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Structs;

/// <summary>
/// 銃器サウンドの指定キー。<c>CItemWeapon.OverrideSounds</c> の辞書キーとして使う。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GunSoundKind"/> / クリップ名 / <see cref="MixerChannel"/> / AudioIndex の
/// どれからでも暗黙変換できるので、1 つの辞書に混ぜて書ける。
/// </para>
/// <example>
/// <code>
/// protected override IReadOnlyDictionary&lt;GunSoundSelector, GunSoundOverride&gt; OverrideSounds => new Dictionary&lt;GunSoundSelector, GunSoundOverride&gt;
/// {
///     [GunSoundKind.Gunshot]                 = "MyGun_Fire.ogg",
///     [GunSoundClips.Crossvec.MagInsert]     = "MyGun_MagIn.ogg",
///     [MixerChannel.DefaultSfx]              = GunSoundOverride.Silent,
///     [GunSoundSelector.FromIndex(14)]       = "MyGun_Rare.ogg",
/// };
/// </code>
/// </example>
/// <para>
/// 同じ音に複数の指定が当たる場合は AudioIndex → クリップ名 → <see cref="GunSoundKind"/> →
/// <see cref="MixerChannel"/> の順で具体的な方が勝つ。
/// </para>
/// </remarks>
public readonly struct GunSoundSelector : IEquatable<GunSoundSelector>
{
    /// <summary>何で指定しているか。</summary>
    public enum SelectorType
    {
        /// <summary>音の種類で指定。</summary>
        Kind,

        /// <summary>AudioClip 名で指定。</summary>
        Clip,

        /// <summary>ミキサーチャンネルで指定。</summary>
        Channel,

        /// <summary>AudioIndex で指定。</summary>
        Index,
    }

    private GunSoundSelector(SelectorType type, GunSoundKind kind, string? clip, MixerChannel channel, int index)
    {
        Type = type;
        Kind = kind;
        Clip = clip;
        Channel = channel;
        Index = index;
    }

    public SelectorType Type { get; }
    public GunSoundKind Kind { get; }

    /// <summary>クリップ名（前後空白は除去済み）。<see cref="Type"/> が <see cref="SelectorType.Clip"/> のときのみ有効。</summary>
    public string? Clip { get; }

    public MixerChannel Channel { get; }
    public int Index { get; }

    public static GunSoundSelector FromKind(GunSoundKind kind)
        => new(SelectorType.Kind, kind, null, default, 0);

    public static GunSoundSelector FromClip(string clip)
        => new(SelectorType.Clip, default, (clip ?? string.Empty).Trim(), default, 0);

    public static GunSoundSelector FromChannel(MixerChannel channel)
        => new(SelectorType.Channel, default, null, channel, 0);

    /// <summary>
    /// AudioIndex 指定。<see cref="MixerChannel"/> との曖昧さを避けるため暗黙変換は用意していない。
    /// </summary>
    public static GunSoundSelector FromIndex(int index)
        => new(SelectorType.Index, default, null, default, index);

    public static implicit operator GunSoundSelector(GunSoundKind kind) => FromKind(kind);
    public static implicit operator GunSoundSelector(string clip) => FromClip(clip);
    public static implicit operator GunSoundSelector(MixerChannel channel) => FromChannel(channel);

    public bool Equals(GunSoundSelector other)
    {
        if (Type != other.Type)
            return false;

        return Type switch
        {
            SelectorType.Kind => Kind == other.Kind,
            SelectorType.Clip => string.Equals(Clip, other.Clip, StringComparison.OrdinalIgnoreCase),
            SelectorType.Channel => Channel == other.Channel,
            SelectorType.Index => Index == other.Index,
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is GunSoundSelector other && Equals(other);

    public override int GetHashCode()
    {
        int payload = Type switch
        {
            SelectorType.Kind => (int)Kind,
            SelectorType.Clip => Clip == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Clip),
            SelectorType.Channel => (int)Channel,
            SelectorType.Index => Index,
            _ => 0,
        };

        // Type と payload を混ぜて、種類違いの同値衝突を避ける。
        return ((int)Type * 397) ^ payload;
    }

    public override string ToString() => Type switch
    {
        SelectorType.Kind => Kind.ToString(),
        SelectorType.Clip => "clip:" + Clip,
        SelectorType.Channel => "channel:" + Channel,
        SelectorType.Index => "index:" + Index,
        _ => "?",
    };
}
