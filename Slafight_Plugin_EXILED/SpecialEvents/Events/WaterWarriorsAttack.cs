using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class WaterWarriorsRaidEvent : SpecialEvent
{
    public override SpecialEventType EventType => SpecialEventType.WaterWarriorsRaid;
    public override int MinPlayersRequired => 5;
    public override string LocalizedName => "Water Warriors Raid";
    public override string TriggerRequirement => "5人以上のプレイヤー";

    private CoroutineHandle _handle;
    private Primitive? _activeFloodInDss;
    private readonly List<Primitive> _activeFloodSurfaces = [];
    private readonly List<WaterParticleState> _activeWaterParticles = [];
    private readonly Dictionary<Room, List<RoomWaterParticleState>> _activeRoomWaterParticles = [];
    private readonly List<WaterTentacle> _activeTentacles = [];
    private readonly Dictionary<Room, Color> _originalFloodRoomColors = [];
    private BossBar? _floodCountdownBar;
    private SpeakerApi.Playback? _floodSongPlayback;
    private float _nextFloodVisualUpdateTime;
    private float _nextRoomParticleUpdateTime;
    private float _nextRoomTintUpdateTime;

    private const string FloodSongFileName = "flood_facility.ogg";
    private const float StabilizationWindowSeconds = 1000f;
    private const float TentacleBreakElapsedSeconds = 158f;
    private const float SurfaceGroundReachElapsedSeconds = TentacleBreakElapsedSeconds + 35f;
    private const float FinalFloodSurgeSeconds = 5f;
    private const float FloodPhaseSeconds = SurfaceGroundReachElapsedSeconds + FinalFloodSurgeSeconds;
    private const float SurfaceGroundTopY = 290f;
    private const float SurfaceFloodTopY = 325f;
    private const float FinalFloodHorizontalScale = 1040f;
    private const int UnderwaterParticleCount = 72;
    private const int RoomWaterParticleCount = 3;
    private const float FloodVisualUpdateInterval = 0.2f;
    private const float RoomParticleUpdateInterval = 0.35f;
    private const float RoomParticleStartDepth = 1.5f;
    private const float RoomParticleStartTintStrength = 0.12f;
    private const float RoomParticleHorizontalSpread = 4.2f;
    private const float RoomParticleMinLocalY = 0.8f;
    private const float RoomParticleMaxLocalY = 4.2f;
    private const float RoomParticleLowerPadding = 0.25f;
    private const float RoomParticleSurfacePadding = 0.25f;
    private const float RoomFloodProbeLocalY = 1.2f;
    private const float RoomTintEllipsoidFeather = 0.32f;
    private const float PlayerFloodProbeHeight = 0.9f;
    private const float RoomTintUpdateInterval = 0.5f;
    private const float RoomTintFullDepth = 35f;
    private static readonly Vector3 InitialFloodScale = new Vector3(6.5f, 8f, 6.5f);
    private static readonly Color FloodBodyColor = new(0.0f, 0.95f, 1f, 0.28f);
    private static readonly Color FloodSurfaceColor = new(0.2f, 0.95f, 1f, 0.48f);
    private static readonly Color FloodParticleColor = new(0.72f, 1f, 1f, 0.55f);
    private static readonly Color RoomBubbleColor = new(0.74f, 1f, 1f, 0.5f);
    private static readonly Color RoomMicrobeColor = new(0.28f, 1f, 0.78f, 0.46f);
    private static readonly Color FloodRoomShallowColor = new(0.15f, 0.92f, 1f, 1f);
    private static readonly Color FloodRoomDeepColor = new(0.0f, 0.22f, 0.48f, 1f);

    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() is SeasonTypeId.Summer;
    }

    protected override void OnExecute(int eventPid)
    {
        if (_handle.IsRunning)
            Timing.KillCoroutines(_handle);

        CleanupActiveState(destroyFlood: true);
        _handle = Timing.RunCoroutine(Coroutine());
    }

    public bool TryPlayFloodScene(out string failureReason)
    {
        if (_handle.IsRunning)
            Timing.KillCoroutines(_handle);

        CleanupActiveState(destroyFlood: true);

        if (!TryCreateDssFlood(out Primitive? floodInDss, out failureReason))
            return false;

        _handle = Timing.RunCoroutine(FloodSceneCoroutine(floodInDss, cancelIfOutdated: false, requireLivingWaterWarrior: false));
        failureReason = string.Empty;
        return true;
    }

    public bool TryCreateDssFlood(out Primitive? floodInDss, out string failureReason)
    {
        floodInDss = null;

        Room? dssRoom = Room.Get(RoomType.HczCrossRoomWater);
        if (dssRoom == null)
        {
            failureReason = "[WaterWarriorsRaid] Failed to find HczCrossRoomWater for DSS flood.";
            Log.Warn(failureReason);
            return false;
        }

        floodInDss = Primitive.Create(PrimitiveType.Sphere, position: dssRoom.WorldPosition(Vector3.up), scale: InitialFloodScale);
        _activeFloodInDss = floodInDss;
        floodInDss.Collidable = false;
        floodInDss.Color = FloodBodyColor;
        floodInDss.MovementSmoothing = 60;

        UpdateFloodVisuals(FloodVolumeState.FromPrimitive(floodInDss), force: true);

        failureReason = string.Empty;
        return true;
    }

    public override void UnregisterEvents()
    {
        if (_handle.IsRunning)
            Timing.KillCoroutines(_handle);

        CleanupActiveState(destroyFlood: true);
    }

    private IEnumerator<float> Coroutine()
    {
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);
        RoundHazardController.SetDeadmanSwitchBlocked(true);

        yield return Timing.WaitForSeconds(2f);
        if (CancelIfOutdated()) yield break;

        foreach (var player in StaticUtils.SelectRandomPlayersByRatio(CTeam.SCPs, 1f / 3f, true))
            player.SetRole(CRoleTypeId.WaterWarrior);

        IntercomApi.Timeout();
        IntercomApi.SetUnavailable("WATER_WARRIORS_ATTACK", "<size=52><color=red>ERROR: SYSTEM IS NOW FLOODING WATER</color></size>");

        Exiled.API.Features.Cassie.MessageTranslated("$PITCH_1 Attention, All personnel. Unknown Anomaly found in Surface Gate A . Please Check $PITCH_.7 .g2 .g2 .g3 .g3 .g3",
            "全職員に通達。不明な物体が地上ゲートAにて確認されました。直ちに<split>[ノイズ音]",true);

        if (!TryCreateDssFlood(out Primitive? floodInDss, out _))
            yield break;

        yield return Timing.WaitUntilDone(AnimationApi.MoveByCoroutine(floodInDss.Transform, new Vector3(0f, 3f, 0f), 1.5f));
        UpdateFloodVisuals(FloodVolumeState.FromPrimitive(floodInDss), force: true);
        if (CancelIfOutdated())
        {
            CleanupActiveState(destroyFlood: true);
            yield break;
        }

        Exiled.API.Features.Cassie.MessageTranslated("$PITCH_1.05 Attention, All Personnel. Were Forces is here. Going to SUNDAY . . .",
            $"全職員に<color={ServerColors.Aqua}>WATER WARRIOR</color>から通達。<split>夏よあれ！！！！！万歳！！！！！");
        yield return Timing.WaitForSeconds(5f);
        if (CancelIfOutdated())
        {
            CleanupActiveState(destroyFlood: true);
            yield break;
        }

        Exiled.API.Features.Cassie.MessageTranslated("Checking Facility _SUFFIX_PLURAL_REGULAR . . . Intercom . . . Break Confirmed . D S S 0 8 Status . . . Cannot Control able . Confirmed .",
            "施設の状態を確認...<split>放送室の状態...<color=red>破損</color>を確認。<split>DSS-08の状態...制御不能を確認。",true);
        Exiled.API.Features.Cassie.MessageTranslated("Attention, All personnel. Were get bad facility status report. Please terminate unknown forces for alive.",
            $"全職員に通達。施設状態は現在<color=red>危険</color>です。<split>設備の制御を取り戻すため、<color={ServerColors.Aqua}>不明な勢力</color>を必ず打倒してください。");
        yield return Timing.WaitUntilDone(WaitWithCancellation(StabilizationWindowSeconds, floodInDss));
        if (CancelIfOutdated())
        {
            CleanupActiveState(destroyFlood: true);
            yield break;
        }

        if (HasLivingWaterWarrior())
        {
            Exiled.API.Features.Cassie.MessageTranslated("$PITCH_.95 Attention, All personnel. Were Failed To Stabilize Facility. Facility get to FRONT POINT . . . PLEASE ESCAPE IMMEDIATELY FROM EXIT GATES.",
                "全職員に通達。施設の安定化に失敗しました。今や制御不能な玄妙除却システムの水が溢れ出してきています。<split>直ちに、脱出口から脱出してください。",true);

            SpawnEscapeTentacles();
            yield return Timing.WaitUntilDone(FloodFacilityCoroutine(floodInDss, cancelIfOutdated: true, requireLivingWaterWarrior: true));
        }
        else
        {
            HandleFacilityStabilized();
        }
    }

    public IEnumerator<float> FloodSceneCoroutine(
        Primitive floodInDss,
        bool cancelIfOutdated = false,
        bool requireLivingWaterWarrior = false)
    {
        yield return Timing.WaitUntilDone(AnimationApi.MoveByCoroutine(floodInDss.Transform, new Vector3(0f, 3f, 0f), 1.5f));
        UpdateFloodVisuals(FloodVolumeState.FromPrimitive(floodInDss), force: true);

        if (ShouldCancelFloodScene(cancelIfOutdated))
        {
            CleanupActiveState(destroyFlood: true);
            yield break;
        }

        SpawnEscapeTentacles();
        yield return Timing.WaitUntilDone(FloodFacilityCoroutine(floodInDss, cancelIfOutdated, requireLivingWaterWarrior));
    }

    public IEnumerator<float> FloodFacilityCoroutine(
        Primitive floodInDss,
        bool cancelIfOutdated = true,
        bool requireLivingWaterWarrior = true)
    {
        Vector3 startScale = floodInDss.Scale;
        Vector3 startPosition = floodInDss.Position;
        float bottomY = startPosition.y - startScale.y * 0.5f;
        float startTopY = startPosition.y + startScale.y * 0.5f;

        _floodSongPlayback = SpeakerApi.Play(FloodSongFileName, "DSS-08", Vector3.zero, true, null, false, 999999999f, 0f, volume: 1.5f);
        _floodCountdownBar = CreateFloodCountdownBar();
        _floodCountdownBar.Show();

        bool tentaclesDestroyed = false;
        float elapsed = 0f;

        while (elapsed < FloodPhaseSeconds)
        {
            if (ShouldCancelFloodScene(cancelIfOutdated))
            {
                CleanupActiveState(destroyFlood: true);
                yield break;
            }

            if (requireLivingWaterWarrior && !HasLivingWaterWarrior())
            {
                HandleFacilityStabilized();
                yield break;
            }

            ApplyFloodProgress(floodInDss, startScale, startPosition, bottomY, startTopY, elapsed);
            FloodVolumeState volume = FloodVolumeState.FromPrimitive(floodInDss);
            UpdateFloodVisuals(volume);
            UpdateFloodRoomColors(volume, force: false);
            UpdateSubmergedRoomParticles(volume);
            ApplyFloodDrowningToSubmergedPlayers(volume);

            float remainingSeconds = Mathf.Max(0f, FloodPhaseSeconds - elapsed);
            UpdateFloodCountdownBar(elapsed, remainingSeconds);

            if (!tentaclesDestroyed && elapsed >= TentacleBreakElapsedSeconds)
            {
                tentaclesDestroyed = true;
                DestroyEscapeTentacles();
                EvacuationRoundEndState.Begin();
                EscapeHandler.AddEscapeOverride(p => new EscapeHandler.EscapeTargetRole { Vanilla = RoleTypeId.Spectator });
                Exiled.API.Features.Cassie.MessageTranslated("Attention, Were Successfully Destroyed Exit Gates It.",
                    "通達。脱出口をふさいでいた触手の破壊に成功しました。<split>時間は一刻一刻と迫ってきています。早急に脱出してください！",false);
            }

            elapsed += Time.deltaTime;
            yield return 0f;
        }

        ApplyFloodProgress(floodInDss, startScale, startPosition, bottomY, startTopY, FloodPhaseSeconds);
        FloodVolumeState finalVolume = FloodVolumeState.FromPrimitive(floodInDss);
        UpdateFloodVisuals(finalVolume, force: true);
        UpdateFloodRoomColors(finalVolume, force: true);
        UpdateSubmergedRoomParticles(finalVolume, force: true);
        UpdateFloodCountdownBar(FloodPhaseSeconds, 0f);
        DestroyEscapeTentacles();
        StopFloodSong();

        _floodCountdownBar?.Hide();
        _floodCountdownBar = null;

        ApplyFloodDrowningToAllPlayers();
    }

    private IEnumerator<float> WaitWithCancellation(float seconds, Primitive? floodVisual = null)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (CancelIfOutdated())
                yield break;

            if (floodVisual != null)
            {
                FloodVolumeState volume = FloodVolumeState.FromPrimitive(floodVisual);
                UpdateFloodVisuals(volume);
                ApplyFloodDrowningToSubmergedPlayers(volume);
            }

            float step = Mathf.Min(floodVisual == null ? 1f : FloodVisualUpdateInterval, seconds - elapsed);
            elapsed += step;
            yield return Timing.WaitForSeconds(step);
        }
    }

    private void SpawnEscapeTentacles()
    {
        DestroyEscapeTentacles();

        foreach (var point in EscapeHandler.Instance.EscapePoints)
        {
            for (int i = 0; i < 2; i++)
            {
                var tentacle = new WaterTentacle();
                tentacle.Create();
                var offset = new Vector3(UnityEngine.Random.Range(0f, 0.15f), 0f, UnityEngine.Random.Range(0f, 0.15f));
                tentacle.Position = point + offset + Vector3.down * 0.45f;
                _activeTentacles.Add(tentacle);
            }
        }
    }

    private void DestroyEscapeTentacles()
    {
        foreach (var tentacle in _activeTentacles.ToList())
        {
            try { tentacle.Destroy(); }
            catch { /* ignore */ }
        }

        _activeTentacles.Clear();
    }

    private BossBar CreateFloodCountdownBar()
    {
        return new BossBar
        {
            Title = "FACILITY FLOODING",
            TitleColor = ServerColors.Aqua,
            Subtitle = $"地表到達まで {Mathf.CeilToInt(SurfaceGroundReachElapsedSeconds)}秒",
            MaxValue = FloodPhaseSeconds,
            Value = FloodPhaseSeconds,
            BarColor = ServerColors.Aqua,
            Segments = 30,
            ShowNumbers = false,
            RefreshInterval = 0.25f,
            BroadcastDuration = 2,
            DisplayOrder = -10,
        };
    }

    private void UpdateFloodCountdownBar(float elapsedSeconds, float remainingSeconds)
    {
        if (_floodCountdownBar == null)
            return;

        _floodCountdownBar.Value = Mathf.Clamp(remainingSeconds, 0f, FloodPhaseSeconds);
        _floodCountdownBar.Subtitle = elapsedSeconds < SurfaceGroundReachElapsedSeconds
            ? $"地表到達まで {Mathf.CeilToInt(SurfaceGroundReachElapsedSeconds - elapsedSeconds)}秒"
            : $"完全水没まで {Mathf.CeilToInt(remainingSeconds)}秒";
        _floodCountdownBar.BarColor = elapsedSeconds >= TentacleBreakElapsedSeconds
            ? "#ff3333"
            : ServerColors.Aqua;
    }

    private static void ApplyFloodProgress(
        Primitive floodInDss,
        Vector3 startScale,
        Vector3 startPosition,
        float bottomY,
        float startTopY,
        float elapsedSeconds)
    {
        float topY;
        if (elapsedSeconds < SurfaceGroundReachElapsedSeconds)
        {
            float progress = Mathf.Clamp01(elapsedSeconds / SurfaceGroundReachElapsedSeconds);
            topY = Mathf.LerpUnclamped(startTopY, SurfaceGroundTopY, AnimationApi.Evaluate(AnimationEase.SmootherStep, progress));
        }
        else
        {
            float progress = Mathf.Clamp01((elapsedSeconds - SurfaceGroundReachElapsedSeconds) / FinalFloodSurgeSeconds);
            topY = Mathf.LerpUnclamped(SurfaceGroundTopY, SurfaceFloodTopY, AnimationApi.Evaluate(AnimationEase.EaseOutSine, progress));
        }

        float horizontalProgress = Mathf.Clamp01(elapsedSeconds / FloodPhaseSeconds);
        float horizontalScale = Mathf.LerpUnclamped(startScale.x, FinalFloodHorizontalScale, AnimationApi.Evaluate(AnimationEase.SmootherStep, horizontalProgress));
        float height = Mathf.Max(startScale.y, topY - bottomY);

        floodInDss.Scale = new Vector3(horizontalScale, height, horizontalScale);
        floodInDss.Position = new Vector3(startPosition.x, bottomY + height * 0.5f, startPosition.z);
    }

    private void EnsureFloodVisuals(FloodVolumeState volume)
    {
        while (_activeFloodSurfaces.Count < 2)
        {
            float rotationX = _activeFloodSurfaces.Count == 0 ? 90f : -90f;
            var surface = Primitive.Create(
                PrimitiveType.Quad,
                volume.SurfacePosition,
                new Vector3(rotationX, 0f, 0f),
                new Vector3(volume.HorizontalScale, volume.HorizontalScale, 1f),
                true,
                FloodSurfaceColor);

            surface.Collidable = false;
            surface.MovementSmoothing = 60;
            _activeFloodSurfaces.Add(surface);
        }

        while (_activeWaterParticles.Count < UnderwaterParticleCount)
            _activeWaterParticles.Add(CreateWaterParticleState(volume));
    }

    private WaterParticleState CreateWaterParticleState(FloodVolumeState volume)
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
        float size = UnityEngine.Random.Range(0.14f, 0.38f);
        var type = _activeWaterParticles.Count % 5 == 0
            ? PrimitiveType.Capsule
            : PrimitiveType.Sphere;
        var color = new Color(
            Mathf.Clamp01(FloodParticleColor.r + UnityEngine.Random.Range(-0.08f, 0.08f)),
            Mathf.Clamp01(FloodParticleColor.g + UnityEngine.Random.Range(-0.04f, 0.0f)),
            1f,
            UnityEngine.Random.Range(0.34f, 0.62f));

        Primitive particle = Primitive.Create(
            type,
            volume.Center,
            Vector3.zero,
            Vector3.one * size,
            true,
            color);

        particle.Collidable = false;
        particle.MovementSmoothing = 60;

        return new WaterParticleState
        {
            Primitive = particle,
            NormalizedHorizontalOffset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius),
            VerticalFactor = UnityEngine.Random.Range(0.1f, 0.9f),
            DriftRadiusFactor = UnityEngine.Random.Range(0.015f, 0.055f),
            OrbitSpeed = UnityEngine.Random.Range(0.12f, 0.55f),
            BobSpeed = UnityEngine.Random.Range(0.6f, 1.6f),
            Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            RotationSpeed = UnityEngine.Random.Range(18f, 80f),
        };
    }

    private void UpdateFloodVisuals(FloodVolumeState volume, bool force = false)
    {
        EnsureFloodVisuals(volume);

        if (force || Time.time >= _nextFloodVisualUpdateTime)
        {
            _nextFloodVisualUpdateTime = Time.time + FloodVisualUpdateInterval;
            UpdateFloodSurfaces(volume);
            UpdateWaterParticles(volume);
        }
    }

    private void UpdateFloodSurfaces(FloodVolumeState volume)
    {
        for (int i = 0; i < _activeFloodSurfaces.Count; i++)
        {
            Primitive surface = _activeFloodSurfaces[i];
            surface.Position = volume.SurfacePosition + Vector3.up * (i == 0 ? 0.03f : -0.03f);
            surface.Scale = new Vector3(volume.HorizontalScale * 1.06f, volume.HorizontalScale * 1.06f, 1f);
        }
    }

    private void UpdateWaterParticles(FloodVolumeState volume)
    {
        float visualSeconds = Time.time;
        float usableHeight = Mathf.Max(1f, volume.Height - 1.6f);

        foreach (WaterParticleState state in _activeWaterParticles)
        {
            Primitive particle = state.Primitive;
            float driftRadius = Mathf.Max(0.25f, volume.HorizontalScale * state.DriftRadiusFactor);
            float orbit = visualSeconds * state.OrbitSpeed + state.Phase;
            float y = volume.BottomY + 0.8f + state.VerticalFactor * usableHeight;
            y += Mathf.Sin(visualSeconds * state.BobSpeed + state.Phase) * Mathf.Min(1.4f, usableHeight * 0.22f);
            y = Mathf.Clamp(y, volume.BottomY + 0.45f, volume.TopY - 0.65f);

            float vertical = Mathf.Clamp((y - volume.Center.y) / Mathf.Max(0.001f, volume.HalfExtents.y), -0.96f, 0.96f);
            float horizontalSlice = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
            float x = state.NormalizedHorizontalOffset.x * volume.HalfExtents.x * horizontalSlice * 0.92f + Mathf.Cos(orbit) * driftRadius;
            float z = state.NormalizedHorizontalOffset.y * volume.HalfExtents.z * horizontalSlice * 0.92f + Mathf.Sin(orbit * 0.73f) * driftRadius;
            Vector3 position = new(volume.Center.x + x, y, volume.Center.z + z);

            if (!volume.ContainsPoint(position))
            {
                position = new Vector3(
                    volume.Center.x + state.NormalizedHorizontalOffset.x * volume.HalfExtents.x * horizontalSlice * 0.88f,
                    y,
                    volume.Center.z + state.NormalizedHorizontalOffset.y * volume.HalfExtents.z * horizontalSlice * 0.88f);
            }

            particle.Position = position;
            particle.Rotation = Quaternion.Euler(
                visualSeconds * state.RotationSpeed,
                visualSeconds * (state.RotationSpeed * 0.7f) + state.Phase * Mathf.Rad2Deg,
                visualSeconds * (state.RotationSpeed * 0.35f));
        }
    }

    private void UpdateFloodRoomColors(FloodVolumeState volume, bool force)
    {
        if (!force && Time.time < _nextRoomTintUpdateTime)
            return;

        _nextRoomTintUpdateTime = Time.time + RoomTintUpdateInterval;

        foreach (Room room in Room.List)
        {
            if (!CanFloodAffectRoom(room))
                continue;

            float tintStrength = GetRoomFloodTintStrength(room, volume);
            if (tintStrength <= 0f)
                continue;

            if (!_originalFloodRoomColors.ContainsKey(room))
                _originalFloodRoomColors[room] = room.Color;

            float depth = GetRoomFloodDepth(room, volume);
            float deepStrength = Mathf.Clamp01(depth / RoomTintFullDepth);
            Color targetWaterColor = Color.Lerp(FloodRoomShallowColor, FloodRoomDeepColor, deepStrength);
            Color originalColor = _originalFloodRoomColors[room];
            Color roomColor = Color.Lerp(originalColor, targetWaterColor, tintStrength);
            roomColor.a = 1f;

            room.AreLightsOff = false;
            room.Color = roomColor;
        }
    }

    private void UpdateSubmergedRoomParticles(FloodVolumeState volume, bool force = false)
    {
        if (!force && Time.time < _nextRoomParticleUpdateTime)
            return;

        _nextRoomParticleUpdateTime = Time.time + RoomParticleUpdateInterval;

        foreach (Room room in Room.List)
        {
            if (!ShouldRenderSubmergedRoomParticles(room, volume))
                continue;

            EnsureSubmergedRoomParticles(room);
        }

        foreach (KeyValuePair<Room, List<RoomWaterParticleState>> entry in _activeRoomWaterParticles.ToList())
        {
            Room room = entry.Key;
            List<RoomWaterParticleState> particles = entry.Value;

            if (!ShouldRenderSubmergedRoomParticles(room, volume))
            {
                DestroyRoomParticles(particles);
                _activeRoomWaterParticles.Remove(room);
                continue;
            }

            UpdateRoomParticles(room, particles, volume);
        }
    }

    private void EnsureSubmergedRoomParticles(Room room)
    {
        if (!_activeRoomWaterParticles.TryGetValue(room, out List<RoomWaterParticleState> particles))
        {
            particles = [];
            _activeRoomWaterParticles[room] = particles;
        }

        while (particles.Count < RoomWaterParticleCount)
            particles.Add(CreateRoomWaterParticleState(room, particles.Count));
    }

    private RoomWaterParticleState CreateRoomWaterParticleState(Room room, int index)
    {
        bool isMicrobe = index % 3 == 0;
        float size = UnityEngine.Random.Range(0.1f, 0.24f);
        Vector3 scale = isMicrobe
            ? new Vector3(size * 0.45f, size * UnityEngine.Random.Range(1.55f, 2.25f), size * 0.45f)
            : Vector3.one * size;

        Color color = isMicrobe ? RoomMicrobeColor : RoomBubbleColor;
        color.a = UnityEngine.Random.Range(0.34f, 0.62f);

        Vector3 localAnchor = new(
            UnityEngine.Random.Range(-RoomParticleHorizontalSpread, RoomParticleHorizontalSpread),
            RoomParticleMinLocalY,
            UnityEngine.Random.Range(-RoomParticleHorizontalSpread, RoomParticleHorizontalSpread));

        Primitive particle = Primitive.Create(
            isMicrobe ? PrimitiveType.Capsule : PrimitiveType.Sphere,
            RoomLocalToWorld(room, localAnchor),
            UnityEngine.Random.rotation.eulerAngles,
            scale,
            true,
            color);

        particle.Collidable = false;
        particle.MovementSmoothing = 60;

        return new RoomWaterParticleState
        {
            Primitive = particle,
            LocalAnchor = localAnchor,
            LocalTopY = UnityEngine.Random.Range(2.4f, RoomParticleMaxLocalY),
            DriftRadius = UnityEngine.Random.Range(0.12f, 0.55f),
            RiseSpeed = UnityEngine.Random.Range(0.04f, 0.12f),
            OrbitSpeed = UnityEngine.Random.Range(0.25f, 0.85f),
            BobSpeed = UnityEngine.Random.Range(0.9f, 1.8f),
            Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
            RotationSpeed = UnityEngine.Random.Range(22f, 115f),
        };
    }

    private static void UpdateRoomParticles(Room room, List<RoomWaterParticleState> particles, FloodVolumeState volume)
    {
        float visualSeconds = Time.time;
        float waterLocalTop = Mathf.Clamp(
            RoomFloodProbeLocalY + GetRoomFloodDepth(room, volume) - 0.35f,
            RoomParticleMinLocalY + 0.15f,
            RoomParticleMaxLocalY);

        foreach (RoomWaterParticleState state in particles)
        {
            float localTopY = Mathf.Clamp(waterLocalTop, RoomParticleMinLocalY + 0.15f, state.LocalTopY);
            float rise = Mathf.Repeat(visualSeconds * state.RiseSpeed + state.Phase, 1f);
            float orbit = visualSeconds * state.OrbitSpeed + state.Phase;

            Vector3 local = state.LocalAnchor;
            local.x += Mathf.Cos(orbit) * state.DriftRadius;
            local.y = Mathf.Lerp(RoomParticleMinLocalY, localTopY, rise)
                + Mathf.Sin(visualSeconds * state.BobSpeed + state.Phase) * 0.08f;
            local.z += Mathf.Sin(orbit * 0.77f) * state.DriftRadius;

            Vector3 worldPosition = RoomLocalToWorld(room, local);
            if (!volume.TryGetVerticalBoundsAtPoint(worldPosition, out float lowerY, out float upperY) ||
                upperY - lowerY <= RoomParticleLowerPadding + RoomParticleSurfacePadding)
            {
                state.Primitive.Visible = false;
                continue;
            }

            worldPosition.y = Mathf.Clamp(
                worldPosition.y,
                lowerY + RoomParticleLowerPadding,
                upperY - RoomParticleSurfacePadding);

            bool isInsideFlood = volume.ContainsPoint(worldPosition);
            state.Primitive.Visible = isInsideFlood;
            if (!isInsideFlood)
                continue;

            state.Primitive.Position = worldPosition;
            state.Primitive.Rotation = Quaternion.Euler(
                visualSeconds * state.RotationSpeed + state.Phase * Mathf.Rad2Deg,
                visualSeconds * state.RotationSpeed * 0.65f,
                visualSeconds * state.RotationSpeed * 0.35f);
        }
    }

    private static bool ShouldRenderSubmergedRoomParticles(Room room, FloodVolumeState volume)
    {
        return CanFloodAffectRoom(room) &&
               GetRoomFloodDepth(room, volume) >= RoomParticleStartDepth &&
               GetRoomFloodTintStrength(room, volume) >= RoomParticleStartTintStrength;
    }

    private static bool CanFloodAffectRoom(Room? room)
    {
        return room != null && room.Zone != ZoneType.Unspecified && room.Zone != ZoneType.Pocket;
    }

    private static float GetRoomFloodDepth(Room room, FloodVolumeState volume)
    {
        return Mathf.Max(0f, volume.GetVerticalDepthAtPoint(GetRoomFloodProbePosition(room)));
    }

    private static float GetRoomFloodTintStrength(Room room, FloodVolumeState volume)
    {
        return volume.GetContainmentStrength(GetRoomFloodProbePosition(room), RoomTintEllipsoidFeather);
    }

    private static Vector3 GetRoomFloodProbePosition(Room room)
    {
        return RoomLocalToWorld(room, new Vector3(0f, RoomFloodProbeLocalY, 0f));
    }

    private static Vector3 RoomLocalToWorld(Room room, Vector3 localPosition)
    {
        return room.Position + room.Rotation * localPosition;
    }

    private static void ApplyFloodDrowningToSubmergedPlayers(FloodVolumeState volume)
    {
        foreach (Player player in Player.List)
        {
            if (!CanFloodDrown(player))
                continue;

            if (!volume.ContainsPoint(GetPlayerFloodProbePosition(player)))
                continue;

            ApplyFloodDrowning(player);
        }
    }

    private static void ApplyFloodDrowningToAllPlayers()
    {
        foreach (Player player in Player.List)
        {
            if (CanFloodDrown(player))
                ApplyFloodDrowning(player);
        }
    }

    private static bool CanFloodDrown(Player? player)
    {
        return player != null && player.IsSafePlayer() && player.IsAlive && !IsWaterWarrior(player);
    }

    private static void ApplyFloodDrowning(Player player)
    {
        if (player.IsEffectActive<FloodDrowning>())
            return;

        player.EnableEffect<FloodDrowning>(FloodDrowning.DefaultIntensity);
    }

    private static Vector3 GetPlayerFloodProbePosition(Player player)
    {
        return player.Position + Vector3.up * PlayerFloodProbeHeight;
    }

    private void HandleFacilityStabilized()
    {
        CleanupActiveState(destroyFlood: true);
        Exiled.API.Features.Cassie.MessageTranslated("Attention, All personnel. Were Stabilized Facility Successfully. Return Normal Decision.",
            "全職員に通達。施設の安定化に成功しました。<split>通常通りのプロトコルに復帰してください");
    }

    private bool ShouldCancelFloodScene(bool cancelIfOutdated)
    {
        return (cancelIfOutdated && CancelIfOutdated()) || Round.IsLobby || Round.IsEnded;
    }

    private void CleanupActiveState(bool destroyFlood)
    {
        _floodCountdownBar?.Hide();
        _floodCountdownBar = null;
        StopFloodSong();
        DestroyEscapeTentacles();
        DestroyFloodVisuals();
        RestoreFloodRoomColors();

        if (!destroyFlood || _activeFloodInDss == null)
            return;

        try { _activeFloodInDss.Destroy(); }
        catch { /* ignore */ }
        _activeFloodInDss = null;
    }

    private void DestroyFloodVisuals()
    {
        foreach (Primitive surface in _activeFloodSurfaces.ToList())
            SafeDestroyPrimitive(surface);

        _activeFloodSurfaces.Clear();

        foreach (WaterParticleState particleState in _activeWaterParticles.ToList())
            SafeDestroyPrimitive(particleState.Primitive);

        _activeWaterParticles.Clear();

        foreach (List<RoomWaterParticleState> particles in _activeRoomWaterParticles.Values.ToList())
            DestroyRoomParticles(particles);

        _activeRoomWaterParticles.Clear();
        _nextRoomParticleUpdateTime = 0f;
    }

    private static void DestroyRoomParticles(List<RoomWaterParticleState> particles)
    {
        foreach (RoomWaterParticleState particleState in particles.ToList())
            SafeDestroyPrimitive(particleState.Primitive);

        particles.Clear();
    }

    private void RestoreFloodRoomColors()
    {
        foreach (KeyValuePair<Room, Color> entry in _originalFloodRoomColors.ToList())
        {
            Room room = entry.Key;
            if (room == null)
                continue;

            try { room.Color = entry.Value; }
            catch { /* ignore */ }
        }

        _originalFloodRoomColors.Clear();
    }

    private static void SafeDestroyPrimitive(Primitive? primitive)
    {
        if (primitive == null)
            return;

        try { primitive.Destroy(); }
        catch { /* ignore */ }
    }

    private void StopFloodSong()
    {
        if (_floodSongPlayback is { } playback)
        {
            try { playback.Stop(); }
            catch { /* ignore */ }
        }

        _floodSongPlayback = null;
    }

    private static bool HasLivingWaterWarrior()
    {
        return Player.List.Any(player => player != null && player.IsAlive && IsWaterWarrior(player));
    }

    private static bool IsWaterWarrior(Player player)
    {
        return player.GetCustomRole() is CRoleTypeId.WaterWarrior;
    }

    private readonly struct FloodVolumeState
    {
        public FloodVolumeState(Vector3 center, Vector3 scale)
        {
            Center = center;
            HalfExtents = scale * 0.5f;
            Height = scale.y;
            BottomY = center.y - Height * 0.5f;
            TopY = center.y + Height * 0.5f;
            HorizontalScale = Mathf.Max(scale.x, scale.z);
            SurfacePosition = new Vector3(center.x, TopY, center.z);
        }

        public Vector3 Center { get; }
        public Vector3 HalfExtents { get; }
        public float Height { get; }
        public float BottomY { get; }
        public float TopY { get; }
        public float HorizontalScale { get; }
        public Vector3 SurfacePosition { get; }

        public bool ContainsPoint(Vector3 point)
        {
            return GetNormalizedDistanceSqr(point) <= 1f;
        }

        public float GetContainmentStrength(Vector3 point, float edgeFeather)
        {
            float distanceSqr = GetNormalizedDistanceSqr(point);
            if (distanceSqr >= 1f)
                return 0f;

            return Mathf.Clamp01((1f - distanceSqr) / Mathf.Max(0.001f, edgeFeather));
        }

        public float GetVerticalDepthAtPoint(Vector3 point)
        {
            return TryGetVerticalBoundsAtPoint(point, out _, out float upperY)
                ? upperY - point.y
                : -1f;
        }

        public bool TryGetVerticalBoundsAtPoint(Vector3 point, out float lowerY, out float upperY)
        {
            float horizontalDistanceSqr = GetHorizontalNormalizedDistanceSqr(point);
            if (horizontalDistanceSqr > 1f)
            {
                lowerY = 0f;
                upperY = 0f;
                return false;
            }

            float verticalOffset = HalfExtents.y * Mathf.Sqrt(Mathf.Max(0f, 1f - horizontalDistanceSqr));
            lowerY = Center.y - verticalOffset;
            upperY = Center.y + verticalOffset;
            return true;
        }

        private float GetNormalizedDistanceSqr(Vector3 point)
        {
            Vector3 offset = point - Center;
            float x = offset.x / Mathf.Max(0.001f, HalfExtents.x);
            float y = offset.y / Mathf.Max(0.001f, HalfExtents.y);
            float z = offset.z / Mathf.Max(0.001f, HalfExtents.z);
            return x * x + y * y + z * z;
        }

        private float GetHorizontalNormalizedDistanceSqr(Vector3 point)
        {
            Vector3 offset = point - Center;
            float x = offset.x / Mathf.Max(0.001f, HalfExtents.x);
            float z = offset.z / Mathf.Max(0.001f, HalfExtents.z);
            return x * x + z * z;
        }

        public static FloodVolumeState FromPrimitive(Primitive primitive)
        {
            return new FloodVolumeState(primitive.Position, primitive.Scale);
        }
    }

    private sealed class WaterParticleState
    {
        public Primitive Primitive { get; set; } = null!;
        public Vector2 NormalizedHorizontalOffset { get; set; }
        public float VerticalFactor { get; set; }
        public float DriftRadiusFactor { get; set; }
        public float OrbitSpeed { get; set; }
        public float BobSpeed { get; set; }
        public float Phase { get; set; }
        public float RotationSpeed { get; set; }
    }

    private sealed class RoomWaterParticleState
    {
        public Primitive Primitive { get; set; } = null!;
        public Vector3 LocalAnchor { get; set; }
        public float LocalTopY { get; set; }
        public float DriftRadius { get; set; }
        public float RiseSpeed { get; set; }
        public float OrbitSpeed { get; set; }
        public float BobSpeed { get; set; }
        public float Phase { get; set; }
        public float RotationSpeed { get; set; }
    }
}
