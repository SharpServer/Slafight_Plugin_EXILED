#nullable enable
using Exiled.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// ゲーム内表示に合わせたキーカード権限レベル。
/// EXILED の flags 名は Administration を直接表していないため、読み替えはここに集約する。
/// </summary>
public readonly struct KeycardAccessLevels
{
    public static readonly KeycardAccessLevels None = new(KeycardPermissions.None, 0, 0, 0);

    public KeycardPermissions SourcePermissions { get; }
    public int Containment { get; }
    public int Armory { get; }
    public int Administration { get; }
    public int? MaxUses { get; }

    public KeycardAccessLevels(
        KeycardPermissions sourcePermissions,
        int containment,
        int armory,
        int administration,
        int? maxUses = null)
    {
        SourcePermissions = sourcePermissions;
        Containment = containment;
        Armory = armory;
        Administration = administration;
        MaxUses = maxUses;
    }

    public static KeycardAccessLevels FromPermissions(KeycardPermissions permissions)
    {
        return new KeycardAccessLevels(
            permissions,
            GetContainmentLevel(permissions),
            GetArmoryLevel(permissions),
            GetAdministrationLevel(permissions));
    }

    public static KeycardAccessLevels FromVanillaItem(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.KeycardJanitor => new KeycardAccessLevels(KeycardPermissions.None, 1, 0, 0),
            ItemType.KeycardScientist => new KeycardAccessLevels(KeycardPermissions.None, 2, 0, 0),
            ItemType.KeycardResearchCoordinator => new KeycardAccessLevels(KeycardPermissions.None, 2, 0, 1),
            ItemType.KeycardContainmentEngineer => new KeycardAccessLevels(KeycardPermissions.None, 3, 0, 1),
            ItemType.KeycardGuard => new KeycardAccessLevels(KeycardPermissions.None, 1, 1, 1),
            ItemType.KeycardMTFPrivate => new KeycardAccessLevels(KeycardPermissions.None, 2, 2, 1),
            ItemType.KeycardMTFOperative => new KeycardAccessLevels(KeycardPermissions.None, 2, 2, 2),
            ItemType.KeycardMTFCaptain => new KeycardAccessLevels(KeycardPermissions.None, 2, 3, 2),
            ItemType.KeycardChaosInsurgency => new KeycardAccessLevels(KeycardPermissions.None, 2, 3, 2),
            ItemType.KeycardZoneManager => new KeycardAccessLevels(KeycardPermissions.None, 1, 0, 1),
            ItemType.KeycardFacilityManager => new KeycardAccessLevels(KeycardPermissions.None, 3, 0, 3),
            ItemType.KeycardO5 => new KeycardAccessLevels(KeycardPermissions.None, 3, 3, 3),
            ItemType.SurfaceAccessPass => new KeycardAccessLevels(KeycardPermissions.None, 0, 0, 2, 1),
            _ => None,
        };
    }

    public override string ToString()
    {
        var text = $"C{Containment}-A{Armory}-AD{Administration}";
        return MaxUses.HasValue ? $"{text}-Uses{MaxUses.Value}" : text;
    }

    public static int GetContainmentLevel(KeycardPermissions permissions)
    {
        if (Has(permissions, KeycardPermissions.ContainmentLevelThree)) return 3;
        if (Has(permissions, KeycardPermissions.ContainmentLevelTwo)) return 2;
        if (Has(permissions, KeycardPermissions.ContainmentLevelOne)) return 1;
        return 0;
    }

    public static int GetArmoryLevel(KeycardPermissions permissions)
    {
        if (Has(permissions, KeycardPermissions.ArmoryLevelThree)) return 3;
        if (Has(permissions, KeycardPermissions.ArmoryLevelTwo)) return 2;
        if (Has(permissions, KeycardPermissions.ArmoryLevelOne)) return 1;
        return 0;
    }

    public static int GetAdministrationLevel(KeycardPermissions permissions)
    {
        if (Has(permissions, KeycardPermissions.AlphaWarhead)) return 3;
        if (Has(permissions, KeycardPermissions.ExitGates)) return 2;
        if (Has(permissions, KeycardPermissions.Checkpoints) && Has(permissions, KeycardPermissions.Intercom)) return 1;
        return 0;
    }

    private static bool Has(KeycardPermissions permissions, KeycardPermissions flag)
        => (permissions & flag) == flag;
}
