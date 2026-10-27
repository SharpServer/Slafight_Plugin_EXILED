using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// <see cref="Seasonal{T}"/> の入口です。
/// </summary>
public static class Seasonal
{
    /// <summary>
    /// いまの季節で差し替わる値を作ります。<c>Seasonal.Of("通常").On(SeasonTypeId.Halloween, "ハロウィン")</c>
    /// </summary>
    /// <param name="normal">どの <c>On</c> にも当たらなかったときの値。</param>
    public static Seasonal<T> Of<T>(T normal = default) => new(CustomSeason.CurrentId, normal);
}

/// <summary>
/// 季節ごとに差し替わる値です。<c>.On(季節, 値)</c> を並べ、そのまま <typeparamref name="T"/> として使えます。
/// </summary>
public readonly struct Seasonal<T>(SeasonTypeId current, T value)
{
    /// <summary>
    /// いまの季節での値です。
    /// </summary>
    public T Value { get; } = value;

    /// <summary>
    /// いまの季節が <paramref name="season"/> なら <paramref name="seasonalValue"/> に差し替えます。
    /// </summary>
    public Seasonal<T> On(SeasonTypeId season, T seasonalValue) =>
        season == current ? new Seasonal<T>(current, seasonalValue) : this;

    public static implicit operator T(Seasonal<T> seasonal) => seasonal.Value;

    public override string ToString() => Value?.ToString() ?? string.Empty;
}
