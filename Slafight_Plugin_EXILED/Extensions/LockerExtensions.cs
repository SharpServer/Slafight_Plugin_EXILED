#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Lockers;
using Exiled.API.Features.Pickups;
using Mirror;
using ProjectMER.Features;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using PrefabType = Exiled.API.Enums.PrefabType;
using BaseLocker = MapGeneration.Distributors.Locker;
using Locker = Exiled.API.Features.Lockers.Locker;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.Extensions;

/// <summary>
/// Locker をランタイムでスポーンするファクトリ。
/// Exiled の <see cref="PrefabHelper"/>（StructurePositionSync 同期 + NetworkServer.Spawn 込み）を利用する。
/// </summary>
public static class LockerFactory
{
    /// <summary>
    /// 指定タイプの Locker をスポーンする。
    /// </summary>
    /// <param name="type">Locker の種類。</param>
    /// <param name="position">ワールド座標。</param>
    /// <param name="rotation">回転（省略時 identity）。</param>
    /// <param name="scale">スケール（省略時 prefab のまま。指定時は再スポーンで同期する）。</param>
    /// <param name="clearNativeLoot">true ならゲーム標準のルート抽選を無効化して空で出す。</param>
    /// <param name="permissions">全チャンバーに設定するキーカード権限。</param>
    public static Locker? Spawn(
        LockerType type,
        Vector3 position,
        Quaternion? rotation = null,
        Vector3? scale = null,
        bool clearNativeLoot = true,
        KeycardPermissions? permissions = null)
    {
        if (!TryGetPrefabType(type, out PrefabType prefabType))
        {
            Log.Warn($"[LockerFactory] Locker prefab for {type} is not available.");
            return null;
        }

        BaseLocker? baseLocker = PrefabHelper.Spawn<BaseLocker>(prefabType, position, rotation);
        if (baseLocker == null)
        {
            Log.Warn($"[LockerFactory] PrefabHelper could not spawn {prefabType}.");
            return null;
        }

        // "(Clone)" を除去して Exiled の LockerType 判定を保つ
        baseLocker.name = baseLocker.name.Replace("(Clone)", string.Empty);

        // ネイティブのルート抽選は初回 Update で走るため、ここで消せば間に合う
        if (clearNativeLoot)
            baseLocker.Loot = [];

        if (scale.HasValue)
        {
            baseLocker.transform.localScale = scale.Value;
            NetworkServer.UnSpawn(baseLocker.gameObject);
            NetworkServer.Spawn(baseLocker.gameObject);
        }

        Locker? locker = Locker.Get(baseLocker);
        if (locker != null && permissions.HasValue)
            locker.SetAllPermissions(permissions.Value);

        return locker;
    }

    /// <summary>
    /// Exiled の LockerType に対応する PrefabType を取得する。
    /// </summary>
    public static bool TryGetPrefabType(LockerType type, out PrefabType prefabType)
    {
        PrefabType? resolved = type switch
        {
            LockerType.LargeGun => PrefabType.LargeGunLockerStructure,
            LockerType.RifleRack => PrefabType.RifleRackStructure,
            LockerType.Misc => PrefabType.MiscLocker,
            LockerType.Medkit => PrefabType.RegularMedkitStructure,
            LockerType.Adrenaline => PrefabType.AdrenalineMedkitStructure,
            LockerType.ExperimentalWeapon => PrefabType.ExperimentalLockerStructure,
            LockerType.Scp500Pedestal => PrefabType.Scp500PedestalStructure,
            LockerType.AntiScp207Pedestal => PrefabType.AntiScp207PedestalStructure,
            LockerType.Scp207Pedestal => PrefabType.Scp207PedestalStructure,
            LockerType.Scp268Pedestal => PrefabType.Scp268PedestalStructure,
            LockerType.Scp1344Pedestal => PrefabType.Scp1344PedestalStructure,
            LockerType.Scp018Pedestal => PrefabType.Scp018PedestalStructure,
            LockerType.Scp1576Pedestal => PrefabType.Scp1576PedestalStructure,
            LockerType.Scp244Pedestal => PrefabType.Scp244PedestalStructure,
            LockerType.Scp2176Pedestal => PrefabType.Scp2176PedestalStructure,
            LockerType.Scp1853Pedestal => PrefabType.Scp1853PedestalStructure,
            LockerType.Scp1509Pedestal => PrefabType.Scp1509PedestalStructureVariant,
            _ => null,
        };

        prefabType = resolved ?? default;
        return resolved.HasValue;
    }
}

/// <summary>
/// Exiled の Locker / Chamber ラッパーを楽に操作するための拡張。
/// アイテム指定は ItemSpawnpoint と同じ統一書式
/// （bare 名 = CItem 優先 → ItemType、"(ItemType)X" / "(CItem)X" で種類固定）。
/// </summary>
public static class LockerExtensions
{
    // ==== Locker 全体 ====

    /// <summary>全チャンバーを開閉する。</summary>
    public static void SetAllOpen(this Locker locker, bool isOpen)
    {
        foreach (Chamber chamber in locker.Chambers)
            chamber.IsOpen = isOpen;
    }

    /// <summary>全チャンバーのキーカード権限を設定する。</summary>
    public static void SetAllPermissions(this Locker locker, KeycardPermissions permissions)
    {
        foreach (Chamber chamber in locker.Chambers)
            chamber.RequiredPermissions = permissions;
    }

    /// <summary>全チャンバーのインタラクトクールダウンを設定する。</summary>
    public static void SetAllCooldown(this Locker locker, float cooldown)
    {
        foreach (Chamber chamber in locker.Chambers)
            chamber.Cooldown = cooldown;
    }

    /// <summary>全チャンバーの中身（スポーン済み + スポーン待ち）を破棄する。</summary>
    public static void ClearAllContents(this Locker locker)
    {
        foreach (Chamber chamber in locker.Chambers)
            chamber.ClearContents();
    }

    /// <summary>
    /// ゲーム標準のルート抽選を無効化する。
    /// まだ抽選前（スポーン直後など）なら中身が湧かなくなる。
    /// </summary>
    public static void ClearNativeLoot(this Locker locker)
        => locker.Base.Loot = [];

    /// <summary>インデックスでチャンバーを取得する（範囲外は null）。</summary>
    public static Chamber? GetChamber(this Locker locker, int index)
        => index >= 0 && index < locker.Chambers.Count ? locker.Chambers.ElementAt(index) : null;

    /// <summary>
    /// 統一書式でアイテムを追加する。
    /// <paramref name="chamberIndex"/> を省略するとランダムなチャンバーに入る。
    /// </summary>
    /// <returns>生成された Pickup。解決できなければ null。</returns>
    public static Pickup? AddItem(this Locker locker, string itemSpec, int chamberIndex = -1)
    {
        Chamber? chamber = chamberIndex >= 0
            ? locker.GetChamber(chamberIndex)
            : locker.Chambers.Count > 0
                ? locker.Chambers.ElementAt(Random.Range(0, locker.Chambers.Count))
                : null;

        if (chamber == null)
        {
            Log.Warn($"[LockerExtensions] {locker.Type} has no chamber at index {chamberIndex}.");
            return null;
        }

        return chamber.AddItem(itemSpec);
    }

    /// <summary>Locker をネットワークごと破棄する。</summary>
    public static void Destroy(this Locker locker)
        => NetworkServer.Destroy(locker.GameObject);

    // ==== Chamber ====

    /// <summary>
    /// 統一書式でアイテムを追加する（CItem 優先 → ItemType）。
    /// 閉じたチャンバーに入れた場合は開けるまでロックされる（Exiled の AddItem 準拠）。
    /// </summary>
    /// <returns>生成された Pickup。解決できなければ null。</returns>
    public static Pickup? AddItem(this Chamber chamber, string itemSpec)
    {
        Pickup? pickup = CreatePickupFromSpec(itemSpec, chamber.GetRandomSpawnPoint());
        if (pickup == null)
            return null;

        chamber.AddItem(pickup);
        return pickup;
    }

    /// <summary>複数の統一書式アイテムをまとめて追加する。</summary>
    public static List<Pickup> AddItems(this Chamber chamber, params string[] itemSpecs)
    {
        List<Pickup> pickups = [];
        foreach (string spec in itemSpecs)
        {
            Pickup? pickup = chamber.AddItem(spec);
            if (pickup != null)
                pickups.Add(pickup);
        }

        return pickups;
    }

    /// <summary>チャンバー内の Pickup（スポーン済み + スポーン待ち）を列挙する。</summary>
    public static IEnumerable<Pickup> GetPickups(this Chamber chamber, bool includePending = true)
    {
        foreach (var pickupBase in chamber.Base.Content)
        {
            if (pickupBase != null)
                yield return Pickup.Get(pickupBase);
        }

        if (!includePending)
            yield break;

        foreach (var pickupBase in chamber.Base.ToBeSpawned)
        {
            if (pickupBase != null)
                yield return Pickup.Get(pickupBase);
        }
    }

    /// <summary>チャンバーの中身を破棄する。</summary>
    public static void ClearContents(this Chamber chamber, bool includePending = true)
    {
        foreach (Pickup pickup in chamber.GetPickups(includePending).ToList())
        {
            try
            {
                pickup.Destroy();
            }
            catch (Exception e)
            {
                Log.Warn($"[LockerExtensions] Failed to destroy pickup in chamber: {e.Message}");
            }
        }

        chamber.Base.Content.Clear();
        if (includePending)
            chamber.Base.ToBeSpawned.Clear();
    }

    // ==== 統一 Item 解決 ====

    /// <summary>
    /// 統一書式（bare 名 = CItem 優先 → ItemType、"(ItemType)X" / "(CItem)X"）から
    /// 未設置の Pickup を生成する。CItem は指定位置にスポーンされた状態で返る。
    /// </summary>
    public static Pickup? CreatePickupFromSpec(string itemSpec, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(itemSpec))
            return null;

        ItemSpawnSpec spec = ItemSpawnSpec.Parse(itemSpec, null);

        if (spec.AllowsCustom && CItem.TryResolve(spec.Name, out CItem? cItem) && cItem != null)
        {
            Pickup? custom = cItem.Spawn(position);
            if (custom != null)
                return custom;

            Log.Warn($"[LockerExtensions] CItem '{spec.Name}' failed to spawn.");
            return null;
        }

        if (!spec.AllowsVanilla)
        {
            Log.Warn($"[LockerExtensions] Custom item '{spec.Name}' was not found.");
            return null;
        }

        if (!spec.TryGetItemType(ItemType.None, out ItemType itemType) || itemType == ItemType.None)
        {
            Log.Warn($"[LockerExtensions] '{itemSpec}' matched no custom item and is not a valid ItemType.");
            return null;
        }

        return Pickup.Create(itemType);
    }
}
