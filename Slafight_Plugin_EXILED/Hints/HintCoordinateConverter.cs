namespace Slafight_Plugin_EXILED.Hints;

internal static class HintCoordinateConverter
{
    public static int FromRueiY(int rueiY)
    {
        int converted = 1100 - rueiY;
        if (converted < 0)
            return 0;

        if (converted > 1000)
            return 1000;

        return converted;
    }
}
