using System.Collections.Generic;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Utilities;

internal static class SchematicMover
{
    public static IEnumerator<float> Move(SchematicObject? schematic, Vector3 startPos, Vector3 offset, float duration)
        => AnimationApi.MoveByCoroutine(schematic, startPos, offset, duration);
}
