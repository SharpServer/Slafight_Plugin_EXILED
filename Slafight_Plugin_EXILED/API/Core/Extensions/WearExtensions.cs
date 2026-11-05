using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Mirror;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Extensions;

/// <summary>
/// プレイヤーへ Schematic や AdminToy を着せる拡張です。失敗すると null を返します。
/// </summary>
public static class WearExtensions
{
    /// <summary>
    /// 着せる Schematic は動かすので、クライアント側プリミティブへの最適化を切って生成します。
    /// </summary>
    private static readonly SchematicSpawnOptions WearSpawnOptions = new SchematicSpawnOptions { OptimizePrimitives = false };

    /// <summary>
    /// Schematic を名前で生成して着せます。
    /// </summary>
    public static PlayerWear Wear(this Player player, string schematicName, WearOptions options = null)
    {
        if (!player.IsSafePlayer() || string.IsNullOrEmpty(schematicName)) return null;

        options ??= WearOptions.Default;

        Transform anchor = options.Target != null ? options.Target : player.Transform;
        SerializableSchematic serializable = new SerializableSchematic
        {
            SchematicName = schematicName,
            Position = anchor.position + anchor.TransformDirection(options.Offset),
            Rotation = (anchor.rotation * (options.RotationOffset ?? Quaternion.identity)).eulerAngles,
        };

        if (!ObjectSpawner.TrySpawnSchematic(serializable, WearSpawnOptions, out SchematicObject schematic))
        {
            Log.Warn($"[Slafight] Schematic '{schematicName}' を生成できませんでした。");

            return null;
        }

        PlayerWear wear = WearManager.Attach(player, schematic.gameObject, schematic.Destroy, options);

        if (wear is null)
            schematic.Destroy();

        return wear;
    }

    /// <summary>
    /// 生成済みの Schematic を着せます。
    /// </summary>
    public static PlayerWear Wear(this Player player, SchematicObject schematic, WearOptions options = null)
    {
        if (schematic == null) return null;

        // 既定の生成経路だと一部がクライアント側へ移されていて、動かしても隠してもついて来ない。
        PrimitiveOptimizationManager.PromoteSchematic(schematic);

        return WearManager.Attach(player, schematic.gameObject, schematic.Destroy, options);
    }

    /// <summary>
    /// AdminToy を着せます。
    /// </summary>
    public static PlayerWear Wear(this Player player, AdminToy toy, WearOptions options = null) =>
        toy is null ? null : WearManager.Attach(player, toy.GameObject, toy.Destroy, options);

    /// <summary>
    /// ラッパーの無い AdminToy を着せます。
    /// </summary>
    public static PlayerWear Wear(this Player player, AdminToyBase toy, WearOptions options = null) =>
        toy == null ? null : player.Wear(toy.gameObject, options);

    /// <summary>
    /// 任意の GameObject を着せます。destroy を省くと外すときに破棄します。
    /// </summary>
    public static PlayerWear Wear(
        this Player player,
        GameObject gameObject,
        WearOptions options = null,
        Action<GameObject> destroy = null)
    {
        if (gameObject == null) return null;

        return WearManager.Attach(player, gameObject, () => (destroy ?? DestroyObject)(gameObject), options);
    }

    /// <summary>
    /// 指定スロットの装着物を取得します。
    /// </summary>
    public static bool TryGetWear(this Player player, string slot, out PlayerWear wear) =>
        WearManager.TryGet(player, slot, out wear);

    /// <summary>
    /// このプレイヤーの装着物の一覧です。
    /// </summary>
    public static IReadOnlyList<PlayerWear> GetWears(this Player player) => WearManager.GetAll(player);

    /// <summary>
    /// 何か着ているかどうか。slot を指定するとその枠だけを見ます。
    /// </summary>
    public static bool HasWear(this Player player, string slot = null) =>
        slot is null ? WearManager.GetAll(player).Count > 0 : WearManager.TryGet(player, slot, out _);

    /// <summary>
    /// 指定スロットの装着物を外して破棄します。無ければ false。
    /// </summary>
    public static bool RemoveWear(this Player player, string slot)
    {
        if (!WearManager.TryGet(player, slot, out PlayerWear wear)) return false;

        wear.Remove();

        return true;
    }

    /// <summary>
    /// 装着物をすべて外して破棄します。
    /// </summary>
    public static void RemoveWears(this Player player) => WearManager.RemoveAll(player);

    private static void DestroyObject(GameObject gameObject)
    {
        if (gameObject == null) return;

        if (gameObject.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0)
            NetworkServer.Destroy(gameObject);
        else
            UnityEngine.Object.Destroy(gameObject);
    }
}
