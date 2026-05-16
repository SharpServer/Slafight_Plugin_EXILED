using System;
using System.Collections.Generic;
using System.Linq;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleParseHelper
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        /*{ "173", "Scp173" },
        { "3114", "Scp3114" },
        { "3005", "Scp3005" },
        { "966", "Scp966" },
        { "CI_Commando", "ChaosCommando" },
        { "NtfLieutenant", "NtfLieutenant" },
        { "Fifthist", "FifthistRescure" },
        { "SnowWarrier", "SnowWarrier" },
        { "Janitor", "Janitor" },*/
    };

    public static bool TryParseRole(string input, out RoleTypeId? vanilla, out CRoleTypeId? custom)
    {
        vanilla = null;
        custom  = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var key = input.Trim();
        if (Aliases.TryGetValue(key, out var alias))
            key = alias;

        // まず RoleTypeId を試す
        if (Enum.TryParse(key, true, out RoleTypeId r))
        {
            vanilla = r;
            return true;
        }

        // ダメなら CRoleTypeId
        if (Enum.TryParse(key, true, out CRoleTypeId cr))
        {
            custom = cr;
            return true;
        }

        return false;
    }

    // ★ここが「被りを消して一つにする」一覧生成
    public static IEnumerable<string> GetAllRoleNames()
    {
        var vanillaNames = Enum.GetNames(typeof(RoleTypeId));
        var customNames  = Enum.GetNames(typeof(CRoleTypeId));

        // 名前が被っているものは1つだけにする
        return vanillaNames
            .Concat(customNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n);
    }
}
