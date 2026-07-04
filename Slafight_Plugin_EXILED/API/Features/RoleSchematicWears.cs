using System;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleSchematicWears
{
    private const float AttachDelay = 1.5f;
    private const float PostAttachDelay = 0.5f;

    public static void WearScp3005(Player player)
    {
        WearScp3005Schematic(
            player,
            "SCP3005",
            accessibilityModel: false,
            slot: "scp3005-default");

        WearScp3005Schematic(
            player,
            "SCP3005_A",
            accessibilityModel: true,
            slot: "scp3005-accessibility");
    }

    public static void WearScp999(Player player)
        => WearRoleSchematic(
            player,
            CRoleTypeId.Scp999,
            "Scp999Model",
            nameof(WearScp999),
            playerScale: new Vector3(0.35f, 0.2f, 0.35f),
            offset: new Vector3(0f, 0f, 0.05f),
            afterAttach: HideFromWearer);

    public static void WearSnowWarrior(Player player)
        => WearWarrior(player, CRoleTypeId.SnowWarrior, "SnowWarrier", Color.white);

    public static void WearCandyWarrior(Player player, CRoleTypeId roleType)
        => WearRoleSchematic(
            player,
            roleType,
            "CandyWarrier",
            nameof(WearCandyWarrior),
            playerScale: Vector3.one);

    public static void WearWarrior(Player player, CRoleTypeId roleType, string schematicName, Color? lightColor = null)
        => WearRoleSchematic(
            player,
            roleType,
            schematicName,
            nameof(WearWarrior),
            afterAttach: (_, schem) =>
            {
                if (lightColor.HasValue)
                    AttachSchematicLight(schem, lightColor.Value);
            });

    public static void SpawnScp3005DeathModel(Player player)
        => SpawnDeathModel(player, "SCP3005_N");

    public static void SpawnScp999DeathModel(Player player)
        => SpawnDeathModel(player, "Scp999Model");

    private static void WearScp3005Schematic(
        Player player,
        string schematicName,
        bool accessibilityModel,
        string slot)
        => WearRoleSchematic(
            player,
            CRoleTypeId.Scp3005,
            schematicName,
            nameof(WearScp3005),
            playerScale: new Vector3(0.01f, 1f, 0.01f),
            slot: slot,
            afterAttach: (_, schem) =>
            {
                if (schem.transform.childCount > 0)
                    schem.transform.GetChild(0).localScale = Vector3.one;

                AttachSchematicLight(schem, Color.magenta);
                ConfigureScp3005Visibility(schem, accessibilityModel);
            });

    private static void WearRoleSchematic(
        Player player,
        CRoleTypeId expectedRole,
        string schematicName,
        string logName,
        Vector3? playerScale = null,
        Vector3? offset = null,
        Action<Player, SchematicObject>? afterAttach = null,
        string slot = WearsHandler.DefaultWearSlot)
    {
        if (player == null)
            return;

        var playerId = player.Id;

        Timing.CallDelayed(AttachDelay, () =>
        {
            var current = Player.Get(playerId);
            if (current?.ReferenceHub == null)
            {
                Log.Warn($"[RoleSchematicWears] {logName}: player not found.");
                return;
            }

            if (current.GetCustomRole() != expectedRole)
                return;

            if (playerScale.HasValue)
                current.Scale = playerScale.Value;

            if (!current.TryWear(schematicName, out var schem, offset, slot))
                return;

            Timing.CallDelayed(PostAttachDelay, () =>
            {
                var attachedPlayer = Player.Get(playerId);
                if (attachedPlayer?.ReferenceHub == null || attachedPlayer.GetCustomRole() != expectedRole)
                {
                    WearsHandler.ForceRemoveWearById(playerId, slot);
                    return;
                }

                if (schem == null || schem.transform == null)
                    return;

                afterAttach?.Invoke(attachedPlayer, schem);
            });
        });
    }

    private static void ConfigureScp3005Visibility(SchematicObject schem, bool accessibilityModel)
    {
        if (schem?.gameObject == null)
            return;

        schem.NetworkIdentities.InitShowState(new NetworkShowState
        {
            VisibilityPredicate = receiver =>
                ServerSpecificUserSettings.IsAccessibilityModeEnabled(receiver) == accessibilityModel,
        });
    }

    private static void HideFromWearer(Player player, SchematicObject schem)
    {
        if (player == null || schem == null)
            return;

        foreach (var identity in schem.NetworkIdentities)
            player.HideNetworkIdentity(identity);
    }

    private static void AttachSchematicLight(SchematicObject schem, Color color)
    {
        if (schem == null || schem.transform == null)
            return;

        var light = Light.Create(Vector3.zero);
        light.Position = schem.transform.position + new Vector3(0f, -0.08f, 0f);
        light.Transform.parent = schem.transform;
        light.Scale = Vector3.one;
        light.Range = 10f;
        light.Intensity = 1.25f;
        light.Color = color;
    }

    private static void SpawnDeathModel(Player player, string schematicName)
    {
        if (player == null)
            return;

        try
        {
            ObjectSpawner.SpawnSchematic(schematicName, player.Position, player.Rotation, Vector3.one);
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSchematicWears] Failed to spawn death model '{schematicName}': {ex.Message}");
        }
    }
}
