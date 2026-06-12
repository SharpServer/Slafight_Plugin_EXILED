using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomMaps;

public static class Communications
{
    private const float SetupDelay = 2.25f;
    private const float InteractionDuration = 3f;

    private static readonly List<string> MonitorTexts =
    [
        "Scanning...",
        "Calling Mobile Task Force...",
        "Reporting to Overseer Council...",
        "Updating Defence Model...",
        "ERROR: 0x55555\nDESC: UNKNOWN ERROR"
    ];

    private static CoroutineHandle _setupHandle;
    private static CoroutineHandle _animHandle;

    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += Setup;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= Setup;
        Reset();
    }

    public static InteractableToy? InteractableToy { get; private set; }
    public static Text? TextToy { get; private set; }
    public static SchematicObject? Broadcaster { get; private set; }
    private static SpeakerApi.Playback? _playback = null;
    private static Light? _light = null;
    public static bool IsSecretActivated { get; private set; }

    public static string MonitorText
    {
        get => TextToy?.TextFormat ?? string.Empty;
        set => TextToy?.TextFormat = value;
    }

    private static void Setup()
    {
        Reset();
        _setupHandle = Timing.CallDelayed(SetupDelay, SetupToys);
    }

    private static void Reset()
    {
        Timing.KillCoroutines(_setupHandle);
        Timing.KillCoroutines(_animHandle);
        UnsubscribeInteractable();

        if (_playback is not null)
            SpeakerApi.Stop((SpeakerApi.Playback)_playback);

        TryDestroy(InteractableToy);
        TryDestroy(TextToy);
        TryDestroy(Broadcaster);
        TryDestroy(_light);

        InteractableToy = null;
        TextToy = null;
        Broadcaster = null;
        _playback = null;
        _light = null;
        IsSecretActivated = false;
    }

    private static void TryDestroy(Exiled.API.Features.Toys.AdminToy? toy)
    {
        if (toy == null)
            return;

        try { toy.Destroy(); }
        catch { /* ignored */ }
    }

    private static void TryDestroy(SchematicObject? schematic)
    {
        if (schematic == null)
            return;

        try { schematic.Destroy(); }
        catch { /* ignored */ }
    }

    private static void SetupToys()
    {
        SetupInteractable();
        SetupText();
        SetupMonitor();
        SubscribeInteractable();
    }

    private static void SetupInteractable()
    {
        if (MapFlags.EzcInteractablePoint == Vector3.zero) return;

        var interactableToy = InteractableToy.Create();
        interactableToy.Position = MapFlags.EzcInteractablePoint;
        interactableToy.Rotation = Quaternion.identity;
        interactableToy.Scale = Vector3.one;
        interactableToy.Shape = InvisibleInteractableToy.ColliderShape.Box;
        interactableToy.InteractionDuration = InteractionDuration;
        InteractableToy = interactableToy;
    }

    private static void SetupText()
    {
        if (MapFlags.EzcScreenPoint == Vector3.zero) return;

        var textToy = Text.Create();
        textToy.Scale = new Vector3(0.55f, 0.65f, 1f);
        textToy.Position = MapFlags.EzcScreenPoint;
        textToy.Rotation = StaticUtils
            .GetWorldFromRoomLocal(RoomType.EzDownstairsPcs, Vector3.zero, new Vector3(-10f, 0f, 0f)).worldRotation;
        textToy.DisplaySize = new Vector2(120f, 20f);
        TextToy = textToy;
    }

    private static void SetupMonitor()
    {
        MonitorText = $"<size=10>{MonitorTexts.RandomItem()}</size>";
    }

    private static void SubscribeInteractable()
    {
        if (InteractableToy == null) return;

        InteractableToy.Base.OnSearched -= OnInteracted;
        InteractableToy.Base.OnSearched += OnInteracted;
    }

    private static void UnsubscribeInteractable()
    {
        if (InteractableToy == null) return;

        InteractableToy.Base.OnSearched -= OnInteracted;
    }

    private static void OnInteracted(ReferenceHub referenceHub)
    {
        var player = Player.Get(referenceHub);
        if (player == null) return;
        if (player.HasCItem<KeycardA>())
        {
            if (!IsSecretActivated)
            {
                IsSecretActivated = true;
                ObjectSpawner.TrySpawnSchematic("Broadcaster", MapFlags.BroadcasterPoint, out var schematic);
                Broadcaster = schematic;
                var rot = StaticUtils.GetWorldFromRoomLocal(RoomType.EzDownstairsPcs, Vector3.zero, new Vector3(0f, 0f, 0f)).worldRotation;
                Broadcaster?.Rotation = rot;
                _light = Light.Create(MapFlags.BroadcasterPoint);
                _light?.Range = 50f;
                _light?.LightType = LightType.Box;
                _light?.SpotAngle = 125f;
                _light?.InnerSpotAngle = 100f;
                _light?.Intensity = 1800f;
                _light?.Color = Color.black;
                _light?.ShadowType = LightShadows.Hard;
                _light?.ShadowStrength = 50f;
                if (Broadcaster is null || _light is null) return;
                _animHandle = Timing.RunCoroutine(AnimCoroutine());
            }
            else
            {
                player.ShowHint("[アクセス拒否 - 既に実行されています]");
            }
        }
        else
        {
            player.ShowHint("[アクセス拒否 - 特定の認証キーが必要です]");
        }
    }

    private static IEnumerator<float> AnimCoroutine()
    {
        float elapsed = 0f;
        yield return Timing.WaitForOneFrame;
        MonitorText = $"<size=5>[===LOG-A71-005555.5===]</size>";
        if (Broadcaster is null || _light is null) yield break;
        if (TextToy is not null)
        {
            float duration = 0.05f;
            Vector3 start = Vector3.one;
            Vector3 end = Vector3.zero;

            // 閾値を使って「ほぼ 0」なら終わるようにする
            const float threshold = 0.001f;

            while (Vector3.Distance(TextToy.Scale, end) > threshold)
            {
                if (TextToy is null) yield break;

                TextToy.Scale = StaticUtils.GetScale(start, end, elapsed, duration);

                elapsed += Time.unscaledDeltaTime; // timeScale に依存しない
                if (elapsed >= duration)
                {
                    // 最終的にピタリに end をセット
                    TextToy.Scale = end;
                    break;
                }

                yield return Timing.WaitForOneFrame;
            }
        }
        _playback = SpeakerApi.PlayLoop("br0.ogg", "broadcaster", MapFlags.BroadcasterPoint, parent: Broadcaster?.transform, isSpatial: true, maxDistance: 15.5f, minDistance: 10f);
        yield return Timing.WaitForSeconds(5f);
        if (Broadcaster is null || _light is null) yield break;
        yield return Timing.WaitUntilDone(Anim(Broadcaster.Position, new Vector3(0f, -2.5f, 0f), 5f));
        yield return Timing.WaitForSeconds(2f);
        elapsed = 0f;
        while (_light?.Color != Color.blue)
        {
            if (_light is null) yield break;
            _light?.Color = StaticUtils.GetGradientColor(Color.black, Color.blue, elapsed);
            elapsed += Time.deltaTime;
            if (elapsed > 1f) elapsed = 1f;
            yield return Timing.WaitForOneFrame;
        }
        yield return Timing.WaitForSeconds(7.25f);
        if (Broadcaster is null || _light is null || _playback is null) yield break;
        var pb = (SpeakerApi.Playback)_playback;
        SpeakerApi.Stop(pb);
        SpeakerApi.Play("br1.ogg", "broadcaster", Broadcaster.Position, isSpatial: true, maxDistance: 15.5f, minDistance: 10f);
    }

    private static void UpdateLightPos()
    {
        if (Broadcaster is null || _light is null) return;
        _light.Position = Broadcaster.Position + Broadcaster.Rotation * new Vector3(0f, 0f, 1.25f);
        _light.Rotation = Broadcaster.Rotation * Quaternion.Euler(90f, 0f, 0f);
    }
    
    private static IEnumerator<float> Anim(Vector3 startpos, Vector3 offset, float duration)
    {
        Vector3 startPos = startpos;
        Vector3 endPos = startPos + offset;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                yield break;
            }

            if (Broadcaster?.gameObject == null)
            {
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            Vector3 targetPos = Vector3.Lerp(startPos, endPos, progress);
            
            Broadcaster?.gameObject.transform.position = targetPos;
            UpdateLightPos();

            yield return 0f;
        }
        
        if (Broadcaster?.gameObject != null)
            Broadcaster.gameObject.transform.position = endPos;
    }
}
