using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using Interactables.Interobjects.DoorUtils;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.DoorAccess;

public sealed class SpecialDoorAccessController
{
    private readonly Dictionary<Vector3, SpecialDoorAccessRule> RulesByDoorPosition = new();
    private readonly float _positionToleranceSq;

    internal SpecialDoorAccessController(float positionToleranceSq)
    {
        _positionToleranceSq = positionToleranceSq;
    }

    public IReadOnlyDictionary<Vector3, SpecialDoorAccessRule> Rules => RulesByDoorPosition;

    internal void ConfigureForCurrentMap()
    {
        RulesByDoorPosition.Clear();

        RulesByDoorPosition[MapFlags.OmegaWarheadJoinPoint] = new()
        {
            RequiredCItemType = typeof(OmegaWarheadAccess)
        };

        RulesByDoorPosition[MapFlags.SqDoorPoint] = new()
        {
            RequiredCode = "0727"
        };
        
        RulesByDoorPosition[MapFlags.CDoorO1] = new()
        {
            // Dr. Masoi
            RequiredCode = "1217",
        };

        RulesByDoorPosition[MapFlags.CDoorO2] = new()
        {
            // Dr. Samuels
            RequiredCode = "1979",
        };

        RulesByDoorPosition[MapFlags.CDoorO3] = new()
        {
            // Dr. Galia
            RequiredCode = "1236",
        };

        RulesByDoorPosition[MapFlags.CDoorO4] = new()
        {
            // Dr. Jlolldo
            RequiredCode = "3125",
        };
    }

    internal void ApplyDoorState()
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

    internal void HandleInteraction(InteractingDoorEventArgs ev)
    {
        if (ev.Player == null || ev.Door == null || RulesByDoorPosition.Count == 0)
            return;

        var rule = FindRule(ev.Door.Position);
        if (rule == null)
            return;

        ev.IsAllowed = rule.CanOpen(ev.Player);
        if (!ev.IsAllowed)
        {
            foreach (var accessTuner in CItem.GetAllInstances().OfType<AccessTunerBase>())
            {
                if (!accessTuner.TryConsumeSpecialDoorAccess(ev.Player))
                    continue;

                ev.IsAllowed = true;
                break;
            }
        }

        if (!ev.IsAllowed)
            ev.Player.ShowHint(rule.HintMessage + "\n<size=22><color=yellow>※ヒントはその辺に落ちている、インタラクトできる報告書などに書いてある事があるよ！</color></size>");
    }

    internal void Clear()
    {
        RulesByDoorPosition.Clear();
    }

    public bool HasRuleAt(Vector3 position)
    {
        foreach (var doorPosition in RulesByDoorPosition.Keys)
        {
            if (Vector3.SqrMagnitude(position - doorPosition) <= _positionToleranceSq)
                return true;
        }

        return false;
    }

    public bool HasRuleAt(Door door)
        => door != null && HasRuleAt(door.Position);

    public SpecialDoorAccessRule? FindRule(Vector3 position)
    {
        SpecialDoorAccessRule? best = null;
        float minSq = float.MaxValue;

        foreach (var kvp in RulesByDoorPosition)
        {
            float sq = Vector3.SqrMagnitude(position - kvp.Key);
            if (sq > _positionToleranceSq || sq >= minSq)
                continue;

            minSq = sq;
            best = kvp.Value;
        }

        return best;
    }

    public SpecialDoorAccessRule? FindRule(Door door)
        => door == null ? null : FindRule(door.Position);

    public bool TryFindRule(Vector3 position, out SpecialDoorAccessRule? rule)
    {
        rule = FindRule(position);
        return rule != null;
    }

    public bool TryFindRule(Door door, out SpecialDoorAccessRule? rule)
    {
        rule = FindRule(door);
        return rule != null;
    }
}
