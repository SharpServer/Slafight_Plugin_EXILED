using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Utilities;

internal static class SchematicMover
{
    public static IEnumerator<float> Move(SchematicObject schematic, Vector3 startPos, Vector3 offset, float duration)
    {
        if (schematic?.transform == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        Vector3 endPos = startPos + offset;

        while (elapsed < duration)
        {
            if (Round.IsLobby || Round.IsEnded || schematic?.transform == null)
                yield break;

            elapsed += Time.deltaTime;
            schematic.transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return 0f;
        }

        if (schematic?.transform != null)
            schematic.transform.position = endPos;
    }
}
