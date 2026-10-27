using PlayerRoles;

namespace Slafight_Plugin_EXILED.ForceSystem.Forces;

/// <summary>
/// 機動部隊の隊です。標準ルールをそのまま使います。
/// </summary>
public sealed class MobileTaskForce : ForceBase
{
    private readonly string vanillaName;

    internal MobileTaskForce(string vanillaName, byte? unitId)
    {
        this.vanillaName = vanillaName;
        UnitId = unitId;
    }

    /// <inheritdoc />
    public override byte? UnitId { get; }

    /// <summary>
    /// 本隊はバニラが採番した部隊名をそのまま使います。
    /// </summary>
    protected override string BuildMainName() => ForceNaming.Adopt(vanillaName);

    /// <inheritdoc />
    public override Faction Faction => Faction.FoundationStaff;
}
