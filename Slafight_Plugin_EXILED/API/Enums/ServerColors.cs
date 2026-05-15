using System;
using System.Collections.Generic;

namespace Slafight_Plugin_EXILED.API.Enums;

public static class ServerColors
{
    public const string Pink = "#FF96DE";
    public const string Red = "#C50000";
    public const string Brown = "#944710";
    public const string Silver = "#A0A0A0";
    public const string LightGreen = "#32CD32";
    public const string Crimson = "#DC143C";
    public const string Cyan = "#00B7EB";
    public const string Aqua = "#00FFFF";
    public const string DeepPink = "#FF1493";
    public const string Tomato = "#FF6448";
    public const string Yellow = "#FAFF86";
    public const string Magenta = "#FF0090";
    public const string BlueGreen = "#4DFFB8";
    public const string Orange = "#FF9966";
    public const string Lime = "#BFFF00";
    public const string Green = "#228B22";
    public const string Emerald = "#50C878";
    public const string Carmine = "#960018";
    public const string Nickel = "#727472";
    public const string Mint = "#98FB98";
    public const string ArmyGreen = "#4B5320";
    public const string Pumpkin = "#EE7600";

    public static readonly IReadOnlyDictionary<string, string> ByServerName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pink"] = Pink,
            ["red"] = Red,
            ["brown"] = Brown,
            ["silver"] = Silver,
            ["light_green"] = LightGreen,
            ["crimson"] = Crimson,
            ["cyan"] = Cyan,
            ["aqua"] = Aqua,
            ["deep_pink"] = DeepPink,
            ["tomato"] = Tomato,
            ["yellow"] = Yellow,
            ["magenta"] = Magenta,
            ["blue_green"] = BlueGreen,
            ["orange"] = Orange,
            ["lime"] = Lime,
            ["green"] = Green,
            ["emerald"] = Emerald,
            ["carmine"] = Carmine,
            ["nickel"] = Nickel,
            ["mint"] = Mint,
            ["army_green"] = ArmyGreen,
            ["pumpkin"] = Pumpkin
        };
}
