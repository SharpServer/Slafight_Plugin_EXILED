using System;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Controls receiver-specific player visibility through EXILED's FPC visibility patch.
/// This is intentionally separate from StatusEffect fake sync; effects do not reliably control
/// whether another client receives a player's movement/role visibility data.
/// </summary>
public static class PlayerVisibilitySyncProvider
{
    public static bool TrySetHiddenFor(Player owner, Player viewer, bool hidden)
    {
        if (!IsValid(owner) || !IsValid(viewer) || owner.Id == viewer.Id)
            return false;

        try
        {
            if (owner.Role is not FpcRole fpcRole)
                return false;

            if (hidden)
                fpcRole.IsInvisibleFor.Add(viewer);
            else
                fpcRole.IsInvisibleFor.Remove(viewer);

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: TrySetHiddenFor failed: {ex.Message}");
            return false;
        }
    }

    public static void SetHiddenRule(Player owner, Func<Player, bool>? shouldHide)
    {
        try
        {
            if (!IsValid(owner) || shouldHide is null)
                return;

            foreach (var viewer in Player.List.ToArray())
            {
                if (!IsValid(viewer) || viewer.Id == owner.Id)
                    continue;

                TrySetHiddenFor(owner, viewer, shouldHide(viewer));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: SetHiddenRule failed: {ex.Message}");
        }
    }

    public static void ShowToAll(Player owner)
    {
        try
        {
            if (!IsValid(owner) || owner.Role is not FpcRole fpcRole)
                return;

            fpcRole.IsInvisibleFor.Clear();
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: ShowToAll failed: {ex.Message}");
        }
    }

    public static int GetHiddenViewerCount(Player owner)
    {
        try
        {
            return IsValid(owner) && owner.Role is FpcRole fpcRole
                ? fpcRole.IsInvisibleFor.Count
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void ClearAll()
    {
        try
        {
            foreach (var owner in Player.List.ToArray())
                ShowToAll(owner);
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: ClearAll failed: {ex.Message}");
        }
    }

    public static void RemoveViewer(Player viewer)
    {
        try
        {
            if (!IsValid(viewer))
                return;

            foreach (var owner in Player.List.ToArray())
            {
                if (!IsValid(owner) || owner.Role is not FpcRole fpcRole)
                    continue;

                fpcRole.IsInvisibleFor.Remove(viewer);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: RemoveViewer failed: {ex.Message}");
        }
    }

    private static bool IsValid(Player? player)
        => player != null && player.IsConnected;
}