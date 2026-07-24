using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.MainHandlers;

/// <summary>
/// プレイヤーに Schematic / AdminToy / GameObject を「着せる」ためのユーティリティ。
/// - 既存呼び出しは default slot として扱うため互換性あり
/// - slot を指定すると、同一プレイヤーへ複数 Wear を同時装着できる
/// - 同じ slot に Wear した場合のみ、その slot の既存 Wear を差し替える
/// - ForceRemoveWear(player) / ForceRemoveWearById(id) でプレイヤー単位の全 Wear を破壊
/// - ForceRemoveWear(player, slot) / ForceRemoveWearById(id, slot) で slot 単位の Wear を破壊
/// - RegisterExternal で外部生成済みオブジェクトを登録
/// - DestroyCoroutine でロール変化時に自動 Destroy
/// </summary>
public static class WearsHandler
{
    public const string DefaultWearSlot = "default";

    private static readonly Dictionary<int, Dictionary<string, WearRegistration>> PlayerWears = new();

    private static CoroutineHandle _cleanupCoroutine;

    private sealed class WearRegistration
    {
        public WearRegistration(
            GameObject gameObject,
            Action destroy,
            PlayerRoleHelpers.PlayerRoleInfo roleInfo)
        {
            GameObject = gameObject;
            Destroy = destroy;
            RoleInfo = roleInfo;
        }

        public GameObject GameObject { get; }
        public Action Destroy { get; }
        public PlayerRoleHelpers.PlayerRoleInfo RoleInfo { get; }
    }

    public static void Register()
    {
        Server.RoundStarted += OnRoundStarted;
        Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
    }

    public static void Unregister()
    {
        Server.RoundStarted -= OnRoundStarted;
        Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

        if (_cleanupCoroutine.IsRunning)
            Timing.KillCoroutines(_cleanupCoroutine);

        CleanupAll();
    }

    // ───────────────────────────────────────────
    //  Public API - Remove / Query
    // ───────────────────────────────────────────

    /// <summary>指定プレイヤーの全 Wear オブジェクトを強制破壊・クリーンアップ。</summary>
    public static bool ForceRemoveWear(Player player)
    {
        if (player == null)
            return false;

        return CleanupPlayer(player);
    }

    /// <summary>指定プレイヤーの指定 slot の Wear オブジェクトを強制破壊・クリーンアップ。</summary>
    public static bool ForceRemoveWear(Player player, string slot)
    {
        if (player == null)
            return false;

        return ForceRemoveWearById(player.Id, slot);
    }

    /// <summary>指定プレイヤーの全 Wear オブジェクトを強制破壊・クリーンアップ。</summary>
    public static bool ForceRemoveAllWears(Player player) => ForceRemoveWear(player);

    /// <summary>全プレイヤーの Wear オブジェクトを一括破壊・クリーンアップ。</summary>
    public static void ForceRemoveAllWears() => CleanupAll();

    /// <summary>プレイヤーオブジェクト不要時用 ID 指定全破壊。</summary>
    public static bool ForceRemoveWearById(int playerId)
    {
        if (!PlayerWears.TryGetValue(playerId, out var wears))
            return false;

        foreach (var wear in wears.Values.ToList())
            DestroyWear(wear);

        PlayerWears.Remove(playerId);
        return true;
    }

    /// <summary>プレイヤーオブジェクト不要時用 ID + slot 指定破壊。</summary>
    public static bool ForceRemoveWearById(int playerId, string slot)
    {
        slot = NormalizeSlot(slot);

        if (!PlayerWears.TryGetValue(playerId, out var wears))
            return false;

        if (!wears.TryGetValue(slot, out var wear))
            return false;

        DestroyWear(wear);
        wears.Remove(slot);

        if (wears.Count == 0)
            PlayerWears.Remove(playerId);

        return true;
    }

    /// <summary>
    /// 現在登録されている Wear の GameObject を取得する。
    /// default slot があればそれを返し、なければ最初の Wear を返す。
    /// </summary>
    public static bool TryGetWornObject(Player player, out GameObject gameObject)
    {
        gameObject = null;
        if (player == null || !PlayerWears.TryGetValue(player.Id, out var wears) || wears.Count == 0)
            return false;

        if (wears.TryGetValue(DefaultWearSlot, out var defaultWear) && defaultWear.GameObject != null)
        {
            gameObject = defaultWear.GameObject;
            return true;
        }

        var first = wears.Values.FirstOrDefault(w => w.GameObject != null);
        if (first == null)
            return false;

        gameObject = first.GameObject;
        return true;
    }

    /// <summary>指定 slot に登録されている Wear の GameObject を取得する。</summary>
    public static bool TryGetWornObject(Player player, string slot, out GameObject gameObject)
    {
        gameObject = null;
        slot = NormalizeSlot(slot);

        if (player == null || !PlayerWears.TryGetValue(player.Id, out var wears))
            return false;

        if (!wears.TryGetValue(slot, out var wear) || wear.GameObject == null)
            return false;

        gameObject = wear.GameObject;
        return true;
    }

    /// <summary>指定プレイヤーに登録されている全 Wear の GameObject を slot 名付きで取得する。</summary>
    public static bool TryGetWornObjects(Player player, out IReadOnlyDictionary<string, GameObject> gameObjects)
    {
        gameObjects = null;

        if (player == null || !PlayerWears.TryGetValue(player.Id, out var wears) || wears.Count == 0)
            return false;

        var result = wears
            .Where(kvp => kvp.Value.GameObject != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GameObject, StringComparer.OrdinalIgnoreCase);

        if (result.Count == 0)
            return false;

        gameObjects = result;
        return true;
    }

    /// <summary>指定プレイヤーに Wear が1つ以上登録されているか。</summary>
    public static bool HasWear(Player player)
        => player != null &&
           PlayerWears.TryGetValue(player.Id, out var wears) &&
           wears.Values.Any(w => w.GameObject != null);

    /// <summary>指定プレイヤーの指定 slot に Wear が登録されているか。</summary>
    public static bool HasWear(Player player, string slot)
    {
        slot = NormalizeSlot(slot);

        return player != null &&
               PlayerWears.TryGetValue(player.Id, out var wears) &&
               wears.TryGetValue(slot, out var wear) &&
               wear.GameObject != null;
    }

    // ───────────────────────────────────────────
    //  Public API - Register External
    // ───────────────────────────────────────────

    /// <summary>
    /// 既にスポーン済みの Schematic を登録する（外部用）。
    /// WearFollower を自動アタッチする。
    /// </summary>
    public static void RegisterExternal(Player player, SchematicObject schem, Vector3? offset = null, string slot = DefaultWearSlot)
    {
        if (schem != null)
            RegisterWear(player, schem.gameObject, player?.Transform, offset, () => schem.Destroy(), null, "RegisterExternal(SchematicObject)", true, slot);
    }

    /// <summary>
    /// 既に生成済みの任意 GameObject を登録する（外部用）。
    /// </summary>
    public static void RegisterExternal(
        Player player,
        GameObject gameObject,
        Vector3? offset = null,
        Transform target = null,
        Action<GameObject> destroy = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        RegisterWear(
            player,
            gameObject,
            target ?? player?.Transform,
            offset,
            () => DestroyGameObject(gameObject, destroy),
            rotationOffset,
            "RegisterExternal(GameObject)",
            true,
            slot);
    }

    /// <summary>
    /// 既に生成済みの AdminToy wrapper を登録する（外部用）。
    /// </summary>
    public static void RegisterExternal(
        Player player,
        AdminToy? toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        if (toy == null)
            return;

        RegisterWear(
            player,
            toy.GameObject,
            target ?? player?.Transform,
            offset,
            () => toy.Destroy(),
            rotationOffset,
            "RegisterExternal(AdminToy)",
            true,
            slot);
    }

    /// <summary>
    /// 既に生成済みの AdminToyBase を登録する（外部用）。
    /// </summary>
    public static void RegisterExternal(
        Player player,
        AdminToyBase toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        if (toy == null)
            return;

        RegisterWear(
            player,
            toy.gameObject,
            target ?? player?.Transform,
            offset,
            () => DestroyAdminToyBase(toy),
            rotationOffset,
            "RegisterExternal(AdminToyBase)",
            true,
            slot);
    }

    // ───────────────────────────────────────────
    //  Public API - Wear
    // ───────────────────────────────────────────

    /// <summary>
    /// Schematic 名を指定してスポーン＆追従。失敗時は何も返さない簡易版。
    /// 既存呼び出しは default slot に登録される。
    /// </summary>
    public static void Wear(this Player player, string wearSchemName, Vector3? offset = null, string slot = DefaultWearSlot)
    {
        if (player == null)
            return;

        var offsetVector = offset ?? Vector3.zero;

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, player.Position + offsetVector);
            if (!RegisterWear(player, schem.gameObject, player.Transform, offsetVector, () => schem.Destroy(), null, "Wear(string)", true, slot))
                schem.Destroy();
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] Wear failed for {player.Nickname}: {e}");
        }
    }

    /// <summary>
    /// スポーン済みの SchematicObject を Wear させる版。
    /// </summary>
    public static void Wear(this Player player, SchematicObject schem, Vector3? offset = null, string slot = DefaultWearSlot)
    {
        if (schem != null)
            RegisterWear(player, schem.gameObject, player?.Transform, offset, () => schem.Destroy(), null, "Wear(SchematicObject)", true, slot);
    }

    /// <summary>
    /// スポーン済みの任意 GameObject を Wear させる版。
    /// </summary>
    public static void Wear(
        this Player player,
        GameObject gameObject,
        Vector3? offset = null,
        Transform target = null,
        Action<GameObject> destroy = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        RegisterWear(
            player,
            gameObject,
            target ?? player?.Transform,
            offset,
            () => DestroyGameObject(gameObject, destroy),
            rotationOffset,
            "Wear(GameObject)",
            true,
            slot);
    }

    /// <summary>
    /// スポーン済みの AdminToy wrapper を Wear させる版。
    /// </summary>
    public static void Wear(
        this Player player,
        AdminToy? toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        if (toy != null)
            RegisterWear(player, toy.GameObject, target ?? player?.Transform, offset, () => toy.Destroy(), rotationOffset, "Wear(AdminToy)", true, slot);
    }

    /// <summary>
    /// スポーン済みの AdminToyBase を Wear させる版。
    /// </summary>
    public static void Wear(
        this Player player,
        AdminToyBase toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        if (toy != null)
            RegisterWear(player, toy.gameObject, target ?? player?.Transform, offset, () => DestroyAdminToyBase(toy), rotationOffset, "Wear(AdminToyBase)", true, slot);
    }

    // ───────────────────────────────────────────
    //  Public API - TryWear
    // ───────────────────────────────────────────

    /// <summary>
    /// 成否＋SchematicObject を取得したい場合はこちら。
    /// 既存呼び出しは default slot に登録される。
    /// </summary>
    public static bool TryWear(this Player player, string wearSchemName, out SchematicObject schematicObject, Vector3? offset = null, string slot = DefaultWearSlot)
    {
        schematicObject = null;

        if (player == null)
            return false;

        var offsetVector = offset ?? Vector3.zero;

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, player.Position + offsetVector, player.Rotation);
            if (!RegisterWear(player, schem.gameObject, player.Transform, offsetVector, () => schem.Destroy(), null, "TryWear(string)", true, slot))
            {
                schem.Destroy();
                return false;
            }
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] TryWear failed for {player.Nickname}: {e}");
            return false;
        }

        if (schem == null)
            return false;

        schematicObject = schem;
        return true;
    }

    /// <summary>
    /// 親Transform を明示指定できる TryWear。
    /// プレイヤー以外のオブジェクトに追従させたい場合に使用。
    /// </summary>
    public static bool TryWear(this Player player, string wearSchemName, Transform parent, out SchematicObject schematicObject, Vector3? offset = null, string slot = DefaultWearSlot)
    {
        schematicObject = null;

        if (player == null || parent == null)
            return false;

        var offsetVector = offset ?? Vector3.zero;

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, parent.position + offsetVector, player.Rotation);
            if (!RegisterWear(player, schem.gameObject, parent, offsetVector, () => schem.Destroy(), null, "TryWear(parent)", true, slot))
            {
                schem.Destroy();
                return false;
            }
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] TryWear(parent) failed for {player.Nickname}: {e}");
            return false;
        }

        if (schem == null)
            return false;

        schematicObject = schem;
        return true;
    }

    /// <summary>
    /// 成否＋GameObject を取得したい場合はこちら。
    /// </summary>
    public static bool TryWear(
        this Player player,
        GameObject gameObject,
        out GameObject wornObject,
        Vector3? offset = null,
        Transform target = null,
        Action<GameObject> destroy = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        wornObject = null;
        if (!RegisterWear(
                player,
                gameObject,
                target ?? player?.Transform,
                offset,
                () => DestroyGameObject(gameObject, destroy),
                rotationOffset,
                "TryWear(GameObject)",
                true,
                slot))
        {
            return false;
        }

        wornObject = gameObject;
        return true;
    }

    /// <summary>
    /// 成否＋AdminToy wrapper を取得したい場合はこちら。
    /// </summary>
    public static bool TryWear<TToy>(
        this Player player,
        TToy? toy,
        out TToy wornToy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
        where TToy : AdminToy
    {
        wornToy = null;
        if (toy == null)
            return false;

        if (!RegisterWear(
                player,
                toy.GameObject,
                target ?? player?.Transform,
                offset,
                () => toy.Destroy(),
                rotationOffset,
                "TryWear(AdminToy)",
                true,
                slot))
        {
            return false;
        }

        wornToy = toy;
        return true;
    }

    /// <summary>
    /// 成否＋AdminToyBase を取得したい場合はこちら。
    /// </summary>
    public static bool TryWear(
        this Player player,
        AdminToyBase toy,
        out AdminToyBase wornToy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null,
        string slot = DefaultWearSlot)
    {
        wornToy = null;
        if (toy == null)
            return false;

        if (!RegisterWear(
                player,
                toy.gameObject,
                target ?? player?.Transform,
                offset,
                () => DestroyAdminToyBase(toy),
                rotationOffset,
                "TryWear(AdminToyBase)",
                true,
                slot))
        {
            return false;
        }

        wornToy = toy;
        return true;
    }

    // ───────────────────────────────────────────
    //  Private helpers
    // ───────────────────────────────────────────

    private static bool RegisterWear(
        Player player,
        GameObject gameObject,
        Transform target,
        Vector3? offset,
        Action? destroy,
        Quaternion? rotationOffset,
        string logContext,
        bool removeExisting = true,
        string slot = DefaultWearSlot)
    {
        if (player == null || player.ReferenceHub == null || gameObject == null || target == null || destroy == null)
            return false;

        slot = NormalizeSlot(slot);

        var id = player.Id;
        var offsetVector = offset ?? Vector3.zero;

        try
        {
            AttachFollower(gameObject, target, offsetVector, rotationOffset);
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] {logContext} failed for {player.Nickname}: {e}");
            return false;
        }

        if (!PlayerWears.TryGetValue(id, out var wears))
        {
            wears = new Dictionary<string, WearRegistration>(StringComparer.OrdinalIgnoreCase);
            PlayerWears[id] = wears;
        }

        if (removeExisting && wears.TryGetValue(slot, out var oldWear))
        {
            // 同一 GameObject の再登録時に自分自身を Destroy しないようにする。
            if (oldWear.GameObject != gameObject)
                DestroyWear(oldWear);

            wears.Remove(slot);
        }

        wears[slot] = new WearRegistration(gameObject, destroy, player.GetRoleInfo());
        return true;
    }

    /// <summary>
    /// GameObject に WearFollower をアタッチ（既存があれば差し替え）。
    /// SetParent は使わない。
    /// </summary>
    private static void AttachFollower(GameObject gameObject, Transform target, Vector3 offset, Quaternion? rotationOffset)
    {
        var existing = gameObject.GetComponent<WearFollower>();
        if (existing != null)
            Object.Destroy(existing);

        var follower = gameObject.AddComponent<WearFollower>();
        follower.Initialize(target, offset, rotationOffset);
    }

    /// <summary>指定 ID / slot にすでに Wear が登録されていれば破壊して辞書から除去。</summary>
    private static void RemoveExisting(int playerId, string slot = DefaultWearSlot)
    {
        ForceRemoveWearById(playerId, slot);
    }

    private static string NormalizeSlot(string slot)
        => string.IsNullOrWhiteSpace(slot) ? DefaultWearSlot : slot.Trim();

    // ───────────────────────────────────────────
    //  Event handlers
    // ───────────────────────────────────────────

    private static void OnRoundStarted()
    {
        if (_cleanupCoroutine.IsRunning)
            Timing.KillCoroutines(_cleanupCoroutine);

        _cleanupCoroutine = Timing.RunCoroutine(DestroyCoroutine());
    }

    private static void OnRoundRestarting()
    {
        if (_cleanupCoroutine.IsRunning)
            Timing.KillCoroutines(_cleanupCoroutine);

        CleanupAll();
    }

    private static void OnPlayerLeft(LeftEventArgs ev) => CleanupPlayer(ev.Player);

    // ───────────────────────────────────────────
    //  Coroutine
    // ───────────────────────────────────────────

    /// <summary>ロール変更を監視し、変化したプレイヤーの Wear オブジェクトを自動 Destroy。</summary>
    private static IEnumerator<float> DestroyCoroutine()
    {
        while (true)
        {
            if (!Round.InProgress)
            {
                yield return Timing.WaitForSeconds(1f);
                continue;
            }

            foreach (var playerKvp in PlayerWears.ToList())
            {
                var playerId = playerKvp.Key;
                var wears = playerKvp.Value;

                var player = Player.Get(playerId);
                if (player == null || player.ReferenceHub == null)
                {
                    ForceRemoveWearById(playerId);
                    continue;
                }

                var current = player.GetRoleInfo();

                foreach (var wearKvp in wears.ToList())
                {
                    var slot = wearKvp.Key;
                    var wear = wearKvp.Value;

                    if (wear.GameObject == null ||
                        wear.RoleInfo.Vanilla != current.Vanilla ||
                        wear.RoleInfo.Custom != current.Custom)
                    {
                        ForceRemoveWearById(playerId, slot);
                    }
                }
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    // ───────────────────────────────────────────
    //  Cleanup
    // ───────────────────────────────────────────

    private static bool CleanupPlayer(Player player)
    {
        if (player == null)
            return false;

        return ForceRemoveWearById(player.Id);
    }

    private static void CleanupAll()
    {
        foreach (var wears in PlayerWears.Values.ToList())
        {
            foreach (var wear in wears.Values.ToList())
                DestroyWear(wear);
        }

        PlayerWears.Clear();
    }

    private static void DestroyWear(WearRegistration? wear)
    {
        if (wear == null)
            return;

        try
        {
            var follower = wear.GameObject != null ? wear.GameObject.GetComponent<WearFollower>() : null;
            if (follower != null)
                Object.Destroy(follower);

            wear.Destroy();
        }
        catch (Exception e)
        {
            Log.Warn($"[WearsHandler] Wear destroy failed: {e}");
        }
    }

    private static void DestroyGameObject(GameObject gameObject, Action<GameObject>? customDestroy)
    {
        if (customDestroy != null)
        {
            customDestroy(gameObject);
            return;
        }

        if (gameObject != null)
            Object.Destroy(gameObject);
    }

    private static void DestroyAdminToyBase(AdminToyBase toy)
    {
        if (toy == null)
            return;

        var wrapper = AdminToy.Get(toy);
        if (wrapper != null)
        {
            wrapper.Destroy();
            return;
        }

        if (toy.gameObject != null)
            Object.Destroy(toy.gameObject);
    }
}
