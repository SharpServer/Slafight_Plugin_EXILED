using Exiled.API.Features;
using Exiled.API.Features.Items;
using Slafight_Plugin_EXILED.CustomItems;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class CItemNvg : CItem
{
    protected virtual NvgProfile NvgProfile => NvgProfile.Default;
    protected override ItemType BaseItem => ItemType.SCP1344;
    protected override bool IsGoggles => true;

    protected override void OnGogglesWorn(Player player, Scp1344 goggles)
        => NvgManager.StartNvg(player, goggles.Serial, NvgProfile);

    protected override void OnGogglesRemoved(Player player, Scp1344 goggles)
        => NvgManager.StopNvg(player, goggles.Serial);

    public static bool HasBattery(ushort serial)
        => NvgManager.HasBattery(serial);

    public static float GetBattery(ushort serial, float fallback = 0f)
        => NvgManager.GetBattery(serial, fallback);

    public static bool SetBattery(ushort serial, float battery, bool reviveIfDead = true)
        => NvgManager.TrySetBattery(serial, battery, reviveIfDead);

    public static bool Recharge(ushort serial)
        => NvgManager.TrySetBattery(serial, 100f, reviveIfDead: true);

    public static bool IsActive(ushort serial)
        => NvgManager.IsActive(serial);
}