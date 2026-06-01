using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.MainHandlers;

public static class SpecificFlagsHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Player.PickingUpItem += OnPicking;
        Exiled.Events.Handlers.Player.DroppingItem += OnDropping;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Player.PickingUpItem -= OnPicking;
        Exiled.Events.Handlers.Player.DroppingItem -= OnDropping;
        SpecificFlagsManager.ClearAll();
    }

    private static void OnPicking(PickingUpItemEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (ev.Player.HasFlag(SpecificFlagType.GunsDisabled) && ev.Pickup.Category is ItemCategory.Firearm)
        {
            ev.IsAllowed = false;
        }

        if (ev.Player.HasFlag(SpecificFlagType.SpecialWeaponsDisabled) &&
            ev.Pickup.Category is ItemCategory.SpecialWeapon)
        {
            ev.IsAllowed = false;
        }

        if (ev.Player.HasFlag(SpecificFlagType.MedicalsDisabled) && ev.Pickup.Category is ItemCategory.Medical)
        {
            ev.IsAllowed = false;
        }
        
        if (ev.Player.HasFlag(SpecificFlagType.PickingDisabled)) ev.IsAllowed = false;

        if (!ev.IsAllowed)
        {
            ev.Player.ShowHint("<size=18>あなたはこのアイテムを拾うことができません！</size>");
        }
    }

    private static void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (ev.Player.HasFlag(SpecificFlagType.DroppingDisabled))
        {
            ev.IsAllowed = false;
        }
        
        if (!ev.IsAllowed)
        {
            ev.Player.ShowHint("<size=18>あなたはこのアイテムを捨てることができません！</size>");
        }
    }
}

