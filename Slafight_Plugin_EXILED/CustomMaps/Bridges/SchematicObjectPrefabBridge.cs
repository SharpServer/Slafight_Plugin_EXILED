using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exiled.API.Features;
using LabApi.Events.Handlers;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// ProjectMER のマーカー（SchematicObjectPrefabObject）と ObjectPrefab を結び付けるブリッジ。
/// フォーク側の Spawned / Destroyed イベント駆動で動作し、発見ポーリングは行わない。
/// transform 追従はマーカー GO に付与する <see cref="MarkerPrefabFollower"/> が担当する。
/// </summary>
public class SchematicObjectPrefabBridge : SlafightLabApiHandler, IBootstrapHandler
{
    private static SchematicObjectPrefabBridge _instance;

    /// <summary>1フレームあたりの Bind 処理時間バジェット（ミリ秒）。</summary>
    private const float BindFrameBudgetMs = 4f;

    /// <summary>Interactable のネットワークスポーンをスキマティック構築と同フレームにしないための猶予。</summary>
    private const float BindReadyDelaySeconds = 0.1f;

    private static readonly List<(SchematicObjectPrefabObject Marker, float ReadyAt)> PendingBinds = new();
    private static readonly HashSet<SchematicObjectPrefabObject> PendingMarkers = new();
    private static int _pendingBindIndex;
    private static CoroutineHandle _bindCoroutineHandle;

    public static void Register() => _instance = LabApiHandlerRegistry.Register(_instance);

    public static void Unregister()
    {
        if (_bindCoroutineHandle.IsRunning)
            Timing.KillCoroutines(_bindCoroutineHandle);
        PendingBinds.Clear();
        PendingMarkers.Clear();
        _pendingBindIndex = 0;

        MarkerPrefabFollower.ReleaseAll();
        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(
            () => SchematicObjectPrefabObject.Spawned += OnMarkerSpawned,
            () => SchematicObjectPrefabObject.Spawned -= OnMarkerSpawned);
        subscriptions.Add(
            () => SchematicObjectPrefabObject.Destroyed += OnMarkerDestroyed,
            () => SchematicObjectPrefabObject.Destroyed -= OnMarkerDestroyed);
        subscriptions.Add(
            () => ServerEvents.WaitingForPlayers += SweepUnboundMarkers,
            () => ServerEvents.WaitingForPlayers -= SweepUnboundMarkers);
    }

    private static void OnMarkerSpawned(SchematicObjectPrefabObject marker)
    {
        // 同じマーカーが待機中なら重複登録しない。既に Bind 済みのマーカーからの
        // 再通知は ProjectMER の更新経路なので、再適用するためキューへ入れる。
        if (marker == null || !PendingMarkers.Add(marker))
            return;

        PendingBinds.Add((marker, Time.time + BindReadyDelaySeconds));

        if (!_bindCoroutineHandle.IsRunning)
            _bindCoroutineHandle = Timing.RunCoroutine(ProcessBindQueue());
    }

    private static void OnMarkerDestroyed(SchematicObjectPrefabObject marker)
    {
        PendingMarkers.Remove(marker);
    }

    /// <summary>
    /// キュー済みマーカーを猶予秒数の経過後、1フレームあたりの処理時間バジェット内で順次 Bind する。
    /// ラウンド開始直後など大量のマーカーが一斉スポーンした場合でも、
    /// スキマティック生成などの重い処理を複数フレームへ分散し、フレームストールを避ける。
    /// </summary>
    private static IEnumerator<float> ProcessBindQueue()
    {
        Log.Debug($"[SchematicObjectPrefabBridge] ProcessBindQueue start. pending={PendingBinds.Count}");
        var stopwatch = Stopwatch.StartNew();
        int processed = 0;

        while (_pendingBindIndex < PendingBinds.Count)
        {
            var pending = PendingBinds[_pendingBindIndex];
            var marker = pending.Marker;
            float readyAt = pending.ReadyAt;

            if (Time.time < readyAt)
            {
                yield return Timing.WaitForOneFrame;
                continue;
            }

            _pendingBindIndex++;
            PendingMarkers.Remove(marker);

            if (marker != null)
            {
                var markerStopwatch = Stopwatch.StartNew();
                BindMarker(marker);
                markerStopwatch.Stop();
                if (markerStopwatch.Elapsed.TotalMilliseconds >= 20d)
                {
                    Log.Warn(
                        $"[SchematicObjectPrefabBridge] BindMarker slow: type={marker.PrefabType} " +
                        $"took={markerStopwatch.Elapsed.TotalMilliseconds:F1}ms");
                }
            }

            processed++;

            if (stopwatch.Elapsed.TotalMilliseconds >= BindFrameBudgetMs)
            {
                yield return Timing.WaitForOneFrame;
                stopwatch.Restart();
            }
        }

        PendingBinds.Clear();
        _pendingBindIndex = 0;
        Log.Debug($"[SchematicObjectPrefabBridge] ProcessBindQueue done. processed={processed}");
    }

    /// <summary>
    /// イベント購読前に生成済みのマーカー（プラグイン再ロード時など）を一度だけ拾う。
    /// </summary>
    private static void SweepUnboundMarkers()
    {
        foreach (SchematicObjectPrefabObject marker in
                 Object.FindObjectsByType<SchematicObjectPrefabObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (marker.GetComponent<MarkerPrefabFollower>() == null)
                OnMarkerSpawned(marker);
        }
    }

    private static void BindMarker(SchematicObjectPrefabObject marker)
    {
        if (string.IsNullOrWhiteSpace(marker.PrefabType))
            return;

        if (!ObjectPrefabRegistry.TryResolveExact(marker.PrefabType, out ObjectPrefabDescriptor descriptor, out string error))
        {
            Log.Warn($"[SchematicObjectPrefabBridge] {error}");
            return;
        }

        MarkerPrefabFollower follower = marker.GetComponent<MarkerPrefabFollower>();
        ObjectPrefab? existing = follower != null ? follower.Prefab : null;

        // 更新経路: 同じ型ならオプションと transform を再適用するだけ
        if (existing != null && ObjectPrefabInstances.Get(existing.ObjectInstanceID) == existing)
        {
            if (existing.GetType() == descriptor.PrefabType)
            {
                ApplyMarker(existing, marker);
                existing.SyncManagedObjects();
                return;
            }

            existing.Destroy();
        }

        if (!ObjectPrefabRegistry.TryCreate(descriptor, out ObjectPrefab prefab, out error))
        {
            Log.Warn($"[SchematicObjectPrefabBridge] {error}");
            return;
        }

        ApplyMarker(prefab, marker);
        prefab.IsSaveable = false;
        prefab.Create();

        follower ??= marker.gameObject.AddComponent<MarkerPrefabFollower>();
        follower.Bind(prefab);
    }

    private static void ApplyMarker(ObjectPrefab prefab, SchematicObjectPrefabObject marker)
    {
        Transform markerTransform = marker.transform;
        prefab.Position = markerTransform.position;
        prefab.Rotation = markerTransform.rotation;
        prefab.Scale = markerTransform.lossyScale;
        prefab.MaxRooms = Math.Max(1, marker.MaxRooms);
        prefab.AutoDestroyEnabled = marker.AutoDestroyEnabled;
        prefab.AutoDestroyTime = marker.AutoDestroyTime;
        prefab.Tag = marker.EffectiveTag;

        if (marker.Options.Count > 0)
            prefab.ApplyOptions(marker.Options);
    }
}

/// <summary>
/// マーカー GO に付与され、Prefab の transform をマーカーに追従させるコンポーネント。
/// マーカー破棄 = Prefab 破棄。
/// </summary>
public sealed class MarkerPrefabFollower : MonoBehaviour
{
    private static readonly List<MarkerPrefabFollower> Active = [];

    private Vector3 _lastPosition;
    private Quaternion _lastRotation;
    private Vector3 _lastScale;

    public ObjectPrefab? Prefab { get; private set; }

    /// <summary>プラグイン無効化時に全バインドを解除し、Prefab を破棄する。</summary>
    public static void ReleaseAll()
    {
        foreach (MarkerPrefabFollower follower in Active.ToList())
            follower.Release(destroyPrefab: true);
    }

    public void Bind(ObjectPrefab prefab)
    {
        Prefab = prefab;
        CacheTransform();
    }

    private void Awake() => Active.Add(this);

    private void OnDestroy()
    {
        Active.Remove(this);
        ObjectPrefab? prefab = Prefab;
        Prefab = null;
        prefab?.Destroy();
    }

    private void LateUpdate()
    {
        ObjectPrefab? prefab = Prefab;
        if (prefab == null)
            return;

        // 外部から破棄された Prefab には追従しない
        if (ObjectPrefabInstances.Get(prefab.ObjectInstanceID) != prefab)
        {
            Prefab = null;
            return;
        }

        if (!prefab.FollowMarkerTransform)
            return;

        Transform markerTransform = transform;
        Vector3 position = markerTransform.position;
        Quaternion rotation = markerTransform.rotation;
        Vector3 scale = markerTransform.lossyScale;

        if (position == _lastPosition && rotation == _lastRotation && scale == _lastScale)
            return;

        _lastPosition = position;
        _lastRotation = rotation;
        _lastScale = scale;

        prefab.Position = position;
        prefab.Rotation = rotation;
        prefab.Scale = scale;
    }

    private void Release(bool destroyPrefab)
    {
        ObjectPrefab? prefab = Prefab;
        Prefab = null;

        if (destroyPrefab)
            prefab?.Destroy();

        Destroy(this);
    }

    private void CacheTransform()
    {
        Transform markerTransform = transform;
        _lastPosition = markerTransform.position;
        _lastRotation = markerTransform.rotation;
        _lastScale = markerTransform.lossyScale;
    }
}
