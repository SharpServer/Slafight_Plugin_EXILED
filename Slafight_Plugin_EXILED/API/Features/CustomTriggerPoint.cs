using System.Collections.Generic;
using ProjectMER.Features;
using ProjectMER.Features.Serializable;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public sealed class CustomTriggerPoint
{
    private CustomTriggerPoint(string tag, Vector3 position, Quaternion rotation)
    {
        Tag = tag;
        Position = position;
        Rotation = rotation;
    }

    public string Tag { get; }

    public Vector3 Position { get; }

    public Quaternion Rotation { get; }

    public static IEnumerable<CustomTriggerPoint> GetAll()
    {
        foreach (var point in TriggerPointManager.GetAll())
        {
            if (point.Base is not SerializableCustomTriggerPoint trig || string.IsNullOrEmpty(trig.Tag))
                continue;

            yield return new CustomTriggerPoint(
                trig.Tag,
                TriggerPointManager.GetWorldPosition(point),
                point.transform.rotation);
        }

        foreach (var point in TriggerPointManager.GetAllSchematic())
        {
            if (string.IsNullOrEmpty(point.Tag))
                continue;

            yield return new CustomTriggerPoint(
                point.Tag,
                TriggerPointManager.GetWorldPosition(point),
                point.transform.rotation);
        }
    }
}
