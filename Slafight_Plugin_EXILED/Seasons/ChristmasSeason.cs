using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.Seasons;

/// <summary>
/// クリスマスです。放送の文面は各 SpawnSet 側で書き分けます。
/// </summary>
public sealed class ChristmasSeason : CustomSeason
{
    /// <inheritdoc />
    public override SeasonTypeId Id => SeasonTypeId.Christmas;

    /// <inheritdoc />
    public override string AccentColor => ServerColors.Red;
}
