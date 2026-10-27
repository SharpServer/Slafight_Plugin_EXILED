using System;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// EXILED の FPC 可視性パッチで、受信者ごとのプレイヤー可視性を制御します。
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

    /// <summary>
    /// 全員 (後から入った人も含む) から見えなくします。役職が変わると解除されます。
    /// </summary>
    public static bool SetInvisible(Player owner, bool invisible)
    {
        try
        {
            if (!IsValid(owner) || owner.Role is not FpcRole fpcRole)
                return false;

            fpcRole.IsInvisible = invisible;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: SetInvisible failed: {ex.Message}");
            return false;
        }
    }

    public static void SetHiddenRule(Player owner, Func<Player, bool>? shouldHide)
    {
        try
        {
            if (!IsValid(owner) || shouldHide is null)
                return;

            foreach (var viewer in Player.List)
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
            foreach (var owner in Player.List)
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
            if (viewer == null)
                return;

            var viewerId = viewer.Id;
            foreach (var owner in Player.List)
            {
                if (!IsValid(owner) || owner.Role is not FpcRole fpcRole)
                    continue;

                foreach (var hiddenViewer in fpcRole.IsInvisibleFor.ToArray())
                {
                    if (hiddenViewer == null || hiddenViewer.ReferenceHub == null || hiddenViewer.Id == viewerId)
                        fpcRole.IsInvisibleFor.Remove(hiddenViewer);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"PlayerVisibilitySyncProvider: RemoveViewer failed: {ex.Message}");
        }
    }

    private static bool IsValid(Player? player)
        => player?.ReferenceHub != null;
}
