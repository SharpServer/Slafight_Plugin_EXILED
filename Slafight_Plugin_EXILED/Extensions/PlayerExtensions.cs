using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using InventorySystem;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerExtensions
{
    public static IReadOnlyCollection<Player> ConnectedList()
    {
        return Player.List.Where(p => p.IsSafePlayer()).ToList();
    }
    
    /// <summary>
    /// バニラ役職を割り当てます。
    /// </summary>
    /// <remarks>
    /// 以前はここに「バニラ役職 → カスタム役職」の読み替え表 (30 分岐の switch) がありました。
    /// <c>RoleTypeId.Scp173</c> を渡すと黙って <c>CRoleTypeId.Scp173</c> になる、という作りです。
    ///
    /// 新 API では役職は型そのもので指すため、この読み替えは要りません。
    /// カスタム役職を出したいときは <c>CustomRole.Spawn&lt;T&gt;(player)</c> を、
    /// バニラ役職を出したいときはこれを呼んでください。
    /// </remarks>
    public static void SetRole(this Player player, RoleTypeId roleTypeId,
        RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (!CanSetRoleSafely(player, roleTypeId))
            return;

        player.Role.Set(roleTypeId, roleSpawnFlags);
    }

    private static bool CanSetRoleSafely(Player player, object role)
    {
        try
        {
            if (player == null)
            {
                Log.Warn($"[SetRole] Skipped {role}: player is null.");
                return false;
            }

            if (player.ReferenceHub == null)
            {
                Log.Warn($"[SetRole] Skipped {role} for {player.Nickname}: ReferenceHub is null.");
                return false;
            }

            if (!player.IsNPC && !player.IsConnected)
            {
                Log.Warn($"[SetRole] Skipped {role} for {player.Nickname}: player is not connected.");
                return false;
            }

            if (player.Role.Type == RoleTypeId.Destroyed)
            {
                Log.Warn($"[SetRole] Skipped {role} for {player.Nickname}: current role is Destroyed.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[SetRole] Skipped {role}: invalid player target ({ex.Message}).");
            return false;
        }
    }

    public static void SetCustomInfo(this Player player, string Info)
        => CustomInfoDisplay.Apply(player, Info);

    public static void SetCustomInfo(this Player player, string Info, CustomInfoDisplayOptions options)
        => CustomInfoDisplay.Apply(player, Info, options);

    public static void SetCustomInfo(this Player player, string Info, CustomInfoUnitNameMode unitNameMode)
        => CustomInfoDisplay.Apply(player, Info, new CustomInfoDisplayOptions { UnitNameMode = unitNameMode });

    public static void RefreshCustomInfo(this Player player)
        => CustomInfoDisplay.Refresh(player);

    public static void ClearCustomInfo(this Player player)
        => CustomInfoDisplay.Clear(player);
    
    public static void SetAmmo(this Player player, ItemType ammoType, ushort amount)
    {
        ItemType itemType = ammoType;
        if (itemType == ItemType.None)
            return;
        player.Inventory.ServerSetAmmo(itemType, (int) amount);
    }
}
