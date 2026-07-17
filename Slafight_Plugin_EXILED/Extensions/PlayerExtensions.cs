using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerExtensions
{
    public static IReadOnlyCollection<Player> ConnectedList()
    {
        return Player.List.Where(p => p.IsSafePlayer()).ToList();
    }
    
    public static void SetRole(this Player player, RoleTypeId roleTypeId,
        RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (!CanSetRoleSafely(player, roleTypeId))
            return;

        Log.Debug($"[SetRole-Vanilla] {player.Nickname} -> {roleTypeId} (flags: {roleSpawnFlags})");
        switch (roleTypeId)
        {
            // ==== SCP ====
            case RoleTypeId.Scp173:
                player.SetRole(CRoleTypeId.Scp173, roleSpawnFlags);
                break;

            case RoleTypeId.Scp049:
                player.SetRole(CRoleTypeId.Scp049, roleSpawnFlags);
                break;

            case RoleTypeId.Scp079:
                player.SetRole(CRoleTypeId.Scp079, roleSpawnFlags);
                break;

            case RoleTypeId.Scp096:
                player.Role.Set(RoleTypeId.Scp096, roleSpawnFlags);
                break;

            case RoleTypeId.Scp106:
                player.SetRole(CRoleTypeId.Scp106, roleSpawnFlags);
                break;

            case RoleTypeId.Scp0492:
                player.Role.Set(RoleTypeId.Scp0492, roleSpawnFlags);
                break;

            case RoleTypeId.Scp939:
                player.Role.Set(RoleTypeId.Scp939, roleSpawnFlags);
                break;

            case RoleTypeId.Scp3114:
                player.SetRole(CRoleTypeId.Scp3114, roleSpawnFlags);
                break;

            // ==== Neutrals ====
            case RoleTypeId.ClassD:
                player.Role.Set(RoleTypeId.ClassD, roleSpawnFlags);
                break;

            case RoleTypeId.Scientist:
                player.Role.Set(RoleTypeId.Scientist, roleSpawnFlags);
                break;

            case RoleTypeId.FacilityGuard:
                player.Role.Set(RoleTypeId.FacilityGuard, roleSpawnFlags);
                break;

            // ==== NTF ====
            case RoleTypeId.NtfPrivate:
                player.Role.Set(RoleTypeId.NtfPrivate, roleSpawnFlags);
                break;

            case RoleTypeId.NtfSergeant:
                player.Role.Set(RoleTypeId.NtfSergeant, roleSpawnFlags);
                break;

            case RoleTypeId.NtfCaptain:
                player.Role.Set(RoleTypeId.NtfCaptain, roleSpawnFlags);
                break;

            case RoleTypeId.NtfSpecialist:
                player.SetRole(CRoleTypeId.NtfSpecialist, roleSpawnFlags);
                break;

            // ==== Chaos ====
            case RoleTypeId.ChaosConscript:
                player.Role.Set(RoleTypeId.ChaosConscript, roleSpawnFlags);
                foreach (var item in player.Items.ToList())
                {
                    if (item.Type == ItemType.KeycardChaosInsurgency)
                    {
                        player.RemoveItem(item);
                    }
                }
                CItem.Get<KeycardConscripts>()?.Give(player); // Conscripts Card
                break;

            case RoleTypeId.ChaosRifleman:
                player.Role.Set(RoleTypeId.ChaosRifleman, roleSpawnFlags);
                break;

            case RoleTypeId.ChaosMarauder:
                player.Role.Set(RoleTypeId.ChaosMarauder, roleSpawnFlags);
                break;

            case RoleTypeId.ChaosRepressor:
                player.Role.Set(RoleTypeId.ChaosRepressor, roleSpawnFlags);
                break;
            
            // ==== Flamingos ===
            case RoleTypeId.AlphaFlamingo:
                player.Role.Set(RoleTypeId.AlphaFlamingo, roleSpawnFlags);
                break;
            case RoleTypeId.Flamingo:
                player.Role.Set(RoleTypeId.Flamingo, roleSpawnFlags);
                break;
            case RoleTypeId.ZombieFlamingo:
                player.Role.Set(RoleTypeId.ZombieFlamingo, roleSpawnFlags);
                break;
            case RoleTypeId.NtfFlamingo:
                player.Role.Set(RoleTypeId.NtfFlamingo, roleSpawnFlags);
                break;
            case RoleTypeId.ChaosFlamingo:
                player.Role.Set(RoleTypeId.ChaosFlamingo, roleSpawnFlags);
                break;
            
            // ==== Others ===
            case RoleTypeId.Spectator:
                player.Role.Set(RoleTypeId.Spectator, roleSpawnFlags);
                break;
            
            case RoleTypeId.Overwatch:
                player.Role.Set(RoleTypeId.Overwatch, roleSpawnFlags);
                break;
            
            case RoleTypeId.Filmmaker:
                player.Role.Set(RoleTypeId.Filmmaker, roleSpawnFlags);
                break;
            
            case RoleTypeId.Tutorial:
                player.Role.Set(RoleTypeId.Tutorial, roleSpawnFlags);
                break;
        }
    }

    public static void SetRole(this Player player, CRoleTypeId roleTypeId,
        RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        if (!CanSetRoleSafely(player, roleTypeId))
            return;

        Log.Debug($"[SetRole-Custom] {player.Nickname} -> {roleTypeId} (flags: {roleSpawnFlags})");
        if (!CRole.TrySpawn(player, roleTypeId, roleSpawnFlags))
        {
            Log.Warn($"[SetRole-Custom] Unknown or unavailable CRoleTypeId: {roleTypeId}");
            return;
        }

        KillCounter.ResetRoleSession(player);
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
        catch (System.Exception ex)
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
}
