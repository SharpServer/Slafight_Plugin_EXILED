using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 動かす・隠す・薄めるなど、生成後に手を加える Schematic の生成口です。
/// </summary>
public static class SchematicSpawner
{
    /// <summary>
    /// クライアント側プリミティブへの最適化を切った生成設定です。
    /// 最適化されたプリミティブは netId を持たず、動かしても色を変えても届きません。
    /// </summary>
    public static readonly SchematicSpawnOptions MovableOptions = new SchematicSpawnOptions { OptimizePrimitives = false };

    /// <summary>最適化を切って Schematic を生成します。</summary>
    public static bool TrySpawnMovable(string schematicName, Vector3 position, Vector3 eulerAngles, out SchematicObject schematic)
    {
        SerializableSchematic serializable = new SerializableSchematic
        {
            SchematicName = schematicName,
            Position = position,
            Rotation = eulerAngles,
        };

        return ObjectSpawner.TrySpawnSchematic(serializable, MovableOptions, out schematic);
    }
}
