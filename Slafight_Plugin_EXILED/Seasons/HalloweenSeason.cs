using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.Seasons;

/// <summary>
/// ハロウィンです。SCP を Specter と呼びます。それ以外の呼び名は各所が <see cref="Seasonal.Of{T}"/> で書き分けます。
/// </summary>
public sealed class HalloweenSeason : CustomSeason
{
    /// <inheritdoc />
    public override SeasonTypeId Id => SeasonTypeId.Halloween;

    /// <inheritdoc />
    public override string ScpCassie => "HALLOWEEN_SPECTER";

    /// <inheritdoc />
    public override string ScpLabel => "Specter";

    /// <inheritdoc />
    public override string AccentColor => ServerColors.Pumpkin;
}
