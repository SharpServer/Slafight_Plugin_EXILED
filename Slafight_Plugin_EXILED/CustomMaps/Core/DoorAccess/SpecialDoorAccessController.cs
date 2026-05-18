using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using Interactables.Interobjects.DoorUtils;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.DoorAccess;

internal sealed class SpecialDoorAccessController(float positionToleranceSq)
{
    private readonly Dictionary<Vector3, SpecialDoorAccessRule> _rulesByDoorPosition = new();

    public void ConfigureForCurrentMap()
    {
        _rulesByDoorPosition.Clear();

        _rulesByDoorPosition[MapFlags.OmegaWarheadJoinPoint] = new()
        {
            RequiredCItemType = typeof(OmegaWarheadAccess)
        };

        _rulesByDoorPosition[new Vector3(-18.614f, 257.005f, -91.739f)] = new()
        {
            RequiredCode = "55555"
        };

        _rulesByDoorPosition[MapFlags.SqDoorPoint] = new()
        {
            RequiredCode = "0727"
        };
        
        _rulesByDoorPosition[MapFlags.CDoorO1] = new()
        {
            // Dr. Masoi
            RequiredCode = "",
        };

        _rulesByDoorPosition[MapFlags.CDoorO2] = new()
        {
            // Dr. Samuels
            RequiredCode = "",
        };

        _rulesByDoorPosition[MapFlags.CDoorO3] = new()
        {
            // Dr. Galia
            RequiredCode = "",
        };

        _rulesByDoorPosition[MapFlags.CDoorO4] = new()
        {
            // Dr. Jlolldo
            RequiredCode = "",
        };
    }

    public void ApplyDoorState()
    {
        foreach (var door in Door.List)
        {
            if (door is null)
                continue;

            switch (door.Type)
            {
                case DoorType.SurfaceGate:
                    door.RequireAllPermissions = true;
                    door.RequiredPermissions = DoorPermissionFlags.ExitGates;
                    break;

                case DoorType.EscapeFinal:
                    door.Unlock();
                    break;

                default:
                    if (HasRuleAt(door.Position))
                        door.Lock(DoorLockType.AdminCommand);
                    break;
            }
        }
    }

    public void HandleInteraction(InteractingDoorEventArgs ev)
    {
        if (ev.Player == null || ev.Door == null || _rulesByDoorPosition.Count == 0)
            return;

        var rule = FindRule(ev.Door.Position);
        if (rule == null)
            return;

        ev.IsAllowed = rule.CanOpen(ev.Player);
        if (!ev.IsAllowed)
            ev.Player.ShowHint(rule.HintMessage);
    }

    public void Clear()
    {
        _rulesByDoorPosition.Clear();
    }

    private bool HasRuleAt(Vector3 position)
    {
        foreach (var doorPosition in _rulesByDoorPosition.Keys)
        {
            if (Vector3.SqrMagnitude(position - doorPosition) <= positionToleranceSq)
                return true;
        }

        return false;
    }

    private SpecialDoorAccessRule? FindRule(Vector3 position)
    {
        SpecialDoorAccessRule? best = null;
        float minSq = float.MaxValue;

        foreach (var kvp in _rulesByDoorPosition)
        {
            float sq = Vector3.SqrMagnitude(position - kvp.Key);
            if (sq > positionToleranceSq || sq >= minSq)
                continue;

            minSq = sq;
            best = kvp.Value;
        }

        return best;
    }
}
