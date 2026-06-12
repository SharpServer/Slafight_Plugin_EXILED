using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.MainHandlers;

/// <summary>
/// プレイヤーに Schematic / AdminToy / GameObject を「着せる」ためのユーティリティ。
/// - Wear / TryWear でスポーンまたは登録＋WearFollowerアタッチ＋ロール情報保存
/// - RegisterExternal で外部生成済みオブジェクトを登録
/// - DestroyCoroutine でロール変化時に自動 Destroy
/// - ForceRemoveWear で外部から強制破壊
/// </summary>
public static class WearsHandler
{
    private static readonly Dictionary<int, WearRegistration> PlayerWears = new();

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
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

        if (_cleanupCoroutine.IsRunning)
            Timing.KillCoroutines(_cleanupCoroutine);

        CleanupAll();
    }

    // ───────────────────────────────────────────
    //  Public API
    // ───────────────────────────────────────────

    /// <summary>指定プレイヤーの Wear オブジェクトを強制破壊・クリーンアップ</summary>
    public static bool ForceRemoveWear(Player player)
    {
        if (player == null)
            return false;

        return CleanupPlayer(player);
    }

    /// <summary>全プレイヤーの Wear オブジェクトを一括破壊・クリーンアップ</summary>
    public static void ForceRemoveAllWears() => CleanupAll();

    /// <summary>プレイヤーオブジェクト不要時用 ID 指定破壊</summary>
    public static bool ForceRemoveWearById(int playerId)
    {
        if (!PlayerWears.TryGetValue(playerId, out var wear))
            return false;

        DestroyWear(wear);
        PlayerWears.Remove(playerId);
        return true;
    }

    /// <summary>現在登録されている Wear の GameObject を取得する。</summary>
    public static bool TryGetWornObject(Player player, out GameObject gameObject)
    {
        gameObject = null;
        if (player == null || !PlayerWears.TryGetValue(player.Id, out var wear))
            return false;

        gameObject = wear.GameObject;
        return gameObject != null;
    }

    /// <summary>指定プレイヤーに Wear が登録されているか。</summary>
    public static bool HasWear(Player player)
        => player != null && PlayerWears.TryGetValue(player.Id, out var wear) && wear.GameObject != null;

    /// <summary>
    /// 既にスポーン済みの Schematic を登録する（外部用）。
    /// WearFollower を自動アタッチする。
    /// </summary>
    public static void RegisterExternal(Player player, SchematicObject schem, Vector3? offset = null)
    {
        if (schem != null)
            RegisterWear(player, schem.gameObject, player?.Transform, offset, () => schem.Destroy(), null, "RegisterExternal(SchematicObject)");
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
        Quaternion? rotationOffset = null)
    {
        RegisterWear(
            player,
            gameObject,
            target ?? player?.Transform,
            offset,
            () => DestroyGameObject(gameObject, destroy),
            rotationOffset,
            "RegisterExternal(GameObject)");
    }

    /// <summary>
    /// 既に生成済みの AdminToy wrapper を登録する（外部用）。
    /// </summary>
    public static void RegisterExternal(
        Player player,
        Exiled.API.Features.Toys.AdminToy? toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null)
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
            "RegisterExternal(AdminToy)");
    }

    /// <summary>
    /// 既に生成済みの AdminToyBase を登録する（外部用）。
    /// </summary>
    public static void RegisterExternal(
        Player player,
        AdminToys.AdminToyBase toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null)
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
            "RegisterExternal(AdminToyBase)");
    }

    /// <summary>
    /// Schematic 名を指定してスポーン＆追従。失敗時は何も返さない簡易版。
    /// </summary>
    public static void Wear(this Player player, string wearSchemName, Vector3? offset = null)
    {
        if (player == null)
            return;

        var id = player.Id;
        var offsetVector = offset ?? Vector3.zero;

        RemoveExisting(id);

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, player.Position + offsetVector);
            if (!RegisterWear(player, schem.gameObject, player.Transform, offsetVector, () => schem.Destroy(), null, "Wear(string)", false))
                schem.Destroy();
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] Wear failed for {player.Nickname}: {e}");
            return;
        }

        if (schem == null)
            return;
    }

    /// <summary>
    /// スポーン済みの SchematicObject を Wear させる版。
    /// </summary>
    public static void Wear(this Player player, SchematicObject schem, Vector3? offset = null)
    {
        if (schem != null)
            RegisterWear(player, schem.gameObject, player?.Transform, offset, () => schem.Destroy(), null, "Wear(SchematicObject)");
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
        Quaternion? rotationOffset = null)
    {
        RegisterWear(
            player,
            gameObject,
            target ?? player?.Transform,
            offset,
            () => DestroyGameObject(gameObject, destroy),
            rotationOffset,
            "Wear(GameObject)");
    }

    /// <summary>
    /// スポーン済みの AdminToy wrapper を Wear させる版。
    /// </summary>
    public static void Wear(
        this Player player,
        Exiled.API.Features.Toys.AdminToy? toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null)
    {
        if (toy != null)
            RegisterWear(player, toy.GameObject, target ?? player?.Transform, offset, () => toy.Destroy(), rotationOffset, "Wear(AdminToy)");
    }

    /// <summary>
    /// スポーン済みの AdminToyBase を Wear させる版。
    /// </summary>
    public static void Wear(
        this Player player,
        AdminToys.AdminToyBase toy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null)
    {
        if (toy != null)
            RegisterWear(player, toy.gameObject, target ?? player?.Transform, offset, () => DestroyAdminToyBase(toy), rotationOffset, "Wear(AdminToyBase)");
    }

    /// <summary>
    /// 成否＋SchematicObject を取得したい場合はこちら。
    /// </summary>
    public static bool TryWear(this Player player, string wearSchemName, out SchematicObject schematicObject, Vector3? offset = null)
    {
        schematicObject = null;

        if (player == null)
            return false;

        var id = player.Id;
        var offsetVector = offset ?? Vector3.zero;

        RemoveExisting(id);

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, player.Position + offsetVector, player.Rotation);
            if (!RegisterWear(player, schem.gameObject, player.Transform, offsetVector, () => schem.Destroy(), null, "TryWear(string)", false))
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
    public static bool TryWear(this Player player, string wearSchemName, Transform parent, out SchematicObject schematicObject, Vector3? offset = null)
    {
        schematicObject = null;

        if (player == null || parent == null)
            return false;

        var id = player.Id;
        var offsetVector = offset ?? Vector3.zero;

        RemoveExisting(id);

        SchematicObject schem;

        try
        {
            schem = ObjectSpawner.SpawnSchematic(wearSchemName, parent.position + offsetVector, player.Rotation);
            if (!RegisterWear(player, schem.gameObject, parent, offsetVector, () => schem.Destroy(), null, "TryWear(parent)", false))
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
        Quaternion? rotationOffset = null)
    {
        wornObject = null;
        if (!RegisterWear(
                player,
                gameObject,
                target ?? player?.Transform,
                offset,
                () => DestroyGameObject(gameObject, destroy),
                rotationOffset,
                "TryWear(GameObject)"))
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
        Quaternion? rotationOffset = null)
        where TToy : Exiled.API.Features.Toys.AdminToy
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
                "TryWear(AdminToy)"))
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
        AdminToys.AdminToyBase toy,
        out AdminToys.AdminToyBase wornToy,
        Vector3? offset = null,
        Transform target = null,
        Quaternion? rotationOffset = null)
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
                "TryWear(AdminToyBase)"))
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
        bool removeExisting = true)
    {
        if (player == null || player.ReferenceHub == null || gameObject == null || target == null || destroy == null)
            return false;

        var id = player.Id;
        var offsetVector = offset ?? Vector3.zero;

        if (removeExisting)
            RemoveExisting(id);

        try
        {
            AttachFollower(gameObject, target, offsetVector, rotationOffset);
        }
        catch (Exception e)
        {
            Log.Error($"[WearsHandler] {logContext} failed for {player.Nickname}: {e}");
            return false;
        }

        PlayerWears[id] = new WearRegistration(gameObject, destroy, player.GetRoleInfo());
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
            UnityEngine.Object.Destroy(existing);

        var follower = gameObject.AddComponent<WearFollower>();
        follower.Initialize(target, offset, rotationOffset);
    }

    /// <summary>指定 ID にすでに Wear が登録されていれば破壊して辞書から除去</summary>
    private static void RemoveExisting(int playerId)
    {
        if (!PlayerWears.TryGetValue(playerId, out var old))
            return;

        DestroyWear(old);
        PlayerWears.Remove(playerId);
    }

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

    /// <summary>ロール変更を監視し、変化したプレイヤーの Wear オブジェクトを自動 Destroy</summary>
    private static IEnumerator<float> DestroyCoroutine()
    {
        while (true)
        {
            if (!Round.InProgress)
            {
                yield return Timing.WaitForSeconds(1f);
                continue;
            }

            foreach (var kvp in PlayerWears.ToList())
            {
                var player = Player.Get(kvp.Key);
                if (player == null || player.ReferenceHub == null)
                {
                    ForceRemoveWearById(kvp.Key);
                    continue;
                }

                var current = player.GetRoleInfo();

                if (kvp.Value.GameObject == null ||
                    kvp.Value.RoleInfo.Vanilla != current.Vanilla ||
                    kvp.Value.RoleInfo.Custom  != current.Custom)
                {
                    CleanupPlayer(player);
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

        var id = player.Id;

        if (!PlayerWears.TryGetValue(id, out var wear))
            return false;

        DestroyWear(wear);
        PlayerWears.Remove(id);
        return true;
    }

    private static void CleanupAll()
    {
        foreach (var wear in PlayerWears.Values.ToList())
            DestroyWear(wear);

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
                UnityEngine.Object.Destroy(follower);

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
            UnityEngine.Object.Destroy(gameObject);
    }

    private static void DestroyAdminToyBase(AdminToys.AdminToyBase toy)
    {
        if (toy == null)
            return;

        var wrapper = Exiled.API.Features.Toys.AdminToy.Get(toy);
        if (wrapper != null)
        {
            wrapper.Destroy();
            return;
        }

        if (toy.gameObject != null)
            UnityEngine.Object.Destroy(toy.gameObject);
    }
}
