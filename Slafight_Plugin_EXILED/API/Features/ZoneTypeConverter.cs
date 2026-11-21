using Exiled.API.Enums;
using FacilityZone = MapGeneration.FacilityZone;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Bridges Exiled zone declarations with the game's facility zones and parses the
/// human-friendly zone strings used by map marker options.
/// </summary>
internal static class ZoneTypeConverter
{
    public static FacilityZone ToFacilityZone(ZoneType zone)
        => zone switch
        {
            ZoneType.LightContainment => FacilityZone.LightContainment,
            ZoneType.HeavyContainment => FacilityZone.HeavyContainment,
            ZoneType.Entrance => FacilityZone.Entrance,
            ZoneType.Surface => FacilityZone.Surface,
            // Pocket does not exist as a FacilityZone; treat it as "other".
            ZoneType.Pocket => FacilityZone.Other,
            ZoneType.Other => FacilityZone.Other,
            _ => FacilityZone.None,
        };

    /// <summary>
    /// Parses a zone declaration. Accepts short aliases and full enum names, case-insensitively.
    /// Unspecified / numeric / combined values are rejected on purpose.
    /// </summary>
    public static bool TryParseZone(string value, out ZoneType zone)
    {
        zone = ZoneType.Unspecified;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "lcz":
            case "lightcontainment":
                zone = ZoneType.LightContainment;
                return true;
            case "hcz":
            case "heavycontainment":
                zone = ZoneType.HeavyContainment;
                return true;
            case "ez":
            case "entrance":
                zone = ZoneType.Entrance;
                return true;
            case "surface":
            case "outside":
                zone = ZoneType.Surface;
                return true;
            case "pocket":
                zone = ZoneType.Pocket;
                return true;
            case "other":
                zone = ZoneType.Other;
                return true;
            default:
                return false;
        }
    }
}
