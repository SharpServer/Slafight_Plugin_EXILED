using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Toys;
using LabApi.Events.Arguments.PlayerEvents;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class EzShelterElevator : ObjectPrefab
{
    protected override string? SchematicName => "ALN_Lift";

    private const float TransitionFallbackDuration = 3f;

    // Audio系プロパティが NullOrEmpty の場合のフォールバック先。PreloadHandler はこの定数を参照してプリロードする。
    internal const string DefaultElevatorJamAudio = "./ObjectPrefabs/EzShelterEV/ElevatorJam.ogg";
    internal const string DefaultDoorCloseAudio = "./ObjectPrefabs/EzShelterEV/ElevatorDoorClose.ogg";
    internal const string DefaultMovingAudio = "./ObjectPrefabs/EzShelterEV/ElevatorMoving.ogg";
    internal const string DefaultDoorOpenAudio = "./ObjectPrefabs/EzShelterEV/ElevatorDoorOpen.ogg";

    public int LocalLevel { get; set; } = 0;
    public DoorOpeningSideFlag DoorOpeningSideFlag { get; set; } = DoorOpeningSideFlag.SideA;

    /// <summary>
    /// 常に流れているエレベーターのアンビエントジャム音(ループ、常に AudioVolume で再生)。
    /// 扉が閉じている間は Waypoint(かご)の Bounds 内にいるプレイヤーにだけ聞こえ、扉が開くとリスナー制限を解除する。
    /// 空なら再生しない。
    /// </summary>
    public string ElevatorJamAudio { get; set; } = string.Empty;

    /// <summary>出発階で扉が閉まる際に鳴らす音。空なら再生しない。</summary>
    public string DoorCloseAudio { get; set; } = string.Empty;

    /// <summary>
    /// 扉が閉まる音の終了後、移動を開始した直後に鳴らす音(モーター音など)。出発 waypoint 位置で再生を開始し、
    /// テレポートと同時に到着 waypoint 位置へ切り替わる(waypoint追従)。空なら再生しない。
    /// </summary>
    public string MovingAudio { get; set; } = string.Empty;

    /// <summary>到着階で扉が開く際に鳴らす音。空なら再生しない。</summary>
    public string DoorOpenAudio { get; set; } = string.Empty;

    /// <summary>移動音の再生開始から到着処理(扉が開く)を始めるまでの最短待機秒数。実際のクリップ長がこれより長ければそちらを優先する。</summary>
    public float RideDuration { get; set; } = 4f;

    /// <summary>移動音の再生開始からプレイヤー・アイテムをテレポートするまでの秒数。</summary>
    public float TeleportDelay { get; set; } = 2.5f;

    public bool AudioSpatial { get; set; } = true;
    public float AudioVolume { get; set; } = 1f;
    public float AudioMaxDistance { get; set; } = 15f;
    public float AudioMinDistance { get; set; } = 1f;

    public static int GlobalLevel
    {
        get;
        private set
        {
            PreviousLevel = field;
            field = value;
        }
    } = 1; // TEMPORARY DISABLED FOR NORMAL PEOPLES. PLEASE SET TO 0 FOR UPDATE RELEASE.

    public static int PreviousLevel { get; private set; } = GlobalLevel;
    public static bool IsTransitioning { get; private set; } = false;

    public static int[] ExistLevels()
    {
        List<int> levels = [];
        foreach (var elevator in ObjectPrefabInstances.GetAll<EzShelterElevator>())
        {
            if (levels.Contains(elevator.LocalLevel)) continue;
            levels.Add(elevator.LocalLevel);
        }

        // GetLoopAt(idx +/- 1) が「番号順で隣の階」を指すよう並び順を保証する。
        levels.Sort();
        return [.. levels];
    }

    // Key(ObjectPrefabSchematicInfo)がこの接頭辞で始まるものを、いくつでも自動でボタンとして採用する。
    // 例: "InnerButton", "InnerButton1", "InnerButton_L" は全て内側ボタン扱い。
    private const string InnerButtonKeyPrefix = "InnerButton";
    private const string OuterButtonKeyPrefix = "OuterButton";

    private Waypoint? _waypoint;
    private readonly List<InteractableHandle> _innerButtons = [];
    private readonly List<InteractableHandle> _outerButtons = [];
    private SpeakerApi.Playback _jamPlayback;

    protected override void OnSetup()
    {
        WaypointToy w = Schematic?.FindBlockComponents<WaypointToy>().FirstOrDefault();
        _waypoint = AdminToy.Get<Waypoint>(w);

        foreach (InteractableHandle handle in Interactables)
        {
            if (handle.Key.StartsWith(InnerButtonKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _innerButtons.Add(handle);
                handle.Interacted += OnInteracted;
            }
            else if (handle.Key.StartsWith(OuterButtonKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _outerButtons.Add(handle);
                handle.Interacted += OnInteracted;
            }
        }

        StartElevatorJam();

        // Both を閉じることで、非ホスト側インスタンスのジャム音は SetJamDoorState(true) により
        // Waypoint内リスナー限定の待機状態になる。
        PlayAnimation(DoorOpeningSideFlag.Both, true);
        if (IsHostOfSession())
        {
            PlayAnimation(DoorOpeningSideFlag, false);
        }
    }

    /// <summary>アンビエントジャム音をループ再生開始する(常に AudioVolume)。</summary>
    private void StartElevatorJam()
    {
        string audio = ResolveAudio(ElevatorJamAudio, DefaultElevatorJamAudio);
        if (string.IsNullOrWhiteSpace(audio) || _waypoint is null)
            return;

        try
        {
            _jamPlayback = SpeakerApi.PlayLoop(
                audio.Trim(),
                $"ezElevator_{ObjectInstanceID}_jam",
                _waypoint.Position,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[EzShelterElevator] Failed to start elevator jam audio '{audio}': {e.Message}");
        }
    }

    /// <summary>
    /// 扉が閉じている間はジャム音のリスナーを Waypoint(かご)の Bounds 内にいるプレイヤーへ限定し、
    /// 扉が開くとリスナー制限を解除して通常の空間音響に戻す。
    /// </summary>
    private void SetJamDoorState(bool isClose)
    {
        if (!_jamPlayback.IsValid || _waypoint is null)
            return;

        SpeakerApi.SetListeners(_jamPlayback, isClose ? IsInsideWaypoint : null);
    }

    private bool IsInsideWaypoint(Player player)
        => _waypoint != null && _waypoint.Bounds.Contains(player.Position);

    protected override void OnDestroy()
    {
        if (_jamPlayback.IsValid)
            SpeakerApi.Stop(_jamPlayback);
    }

    private void OnInteracted(Player player, PlayerSearchedToyEventArgs ev)
    {
        // IsTransitioning は成功パスの末尾でのみ true になり、アニメーション完了後に自動で false へ戻る。
        // ここで弾く分岐（ホストでない/ターゲット未準備）は IsTransitioning に一切触れないため、
        // ここで return しても以後のボタン操作が永久にロックされることはない。
        if (Schematic is null || _waypoint is null || IsTransitioning || !IsHostOfSession()) return;

        InteractableHandle? pressed = _innerButtons.Concat(_outerButtons).FirstOrDefault(button => button.Toy == ev.Interactable);
        if (pressed is null) return;

        var snapshot = ExistLevels();
        if (snapshot.Length < 2) return;

        bool isBack = IsBackButton(pressed);
        int idx = snapshot.IndexOf(GlobalLevel);
        int nextLevel = isBack ? snapshot.GetLoopAt(idx - 1) : snapshot.GetLoopAt(idx + 1);

        var item = ObjectPrefabInstances.GetAll<EzShelterElevator>().FirstOrDefault(x => x.LocalLevel == nextLevel);
        if (item?.Schematic is null || item._waypoint is null) return;

        IsTransitioning = true;
        GlobalLevel = nextLevel;

        // 1. 出発階のドアを閉め、扉が閉まる音を鳴らす。
        PlayAnimation(DoorOpeningSideFlag, true);
        PlayAudio(DoorCloseAudio, DefaultDoorCloseAudio, _waypoint.Position, "doorclose");

        // 2. 扉が閉まる音の実クリップ長だけ待ってから移動を開始する(クリップが無ければ即座)。
        string doorCloseAudio = ResolveAudio(DoorCloseAudio, DefaultDoorCloseAudio);
        float closeDuration = string.IsNullOrWhiteSpace(doorCloseAudio)
            ? 0f
            : SpeakerApi.GetClipDuration(doorCloseAudio.Trim());

        ScheduleDelayed(closeDuration, () => BeginMoving(item));
    }

    /// <summary>
    /// 扉が閉まる音の終了後に呼ばれる。移動音の再生を出発 waypoint 位置で開始し、
    /// 再生開始から <see cref="TeleportDelay"/> 秒後、乗客のテレポートと同時に移動音の位置も
    /// 到着 waypoint へ切り替える(waypoint追従)。
    /// 移動音の実クリップ長と <see cref="RideDuration"/> の長い方だけ待ってから到着処理(<see cref="CompleteTransition"/>)に入る。
    /// </summary>
    private void BeginMoving(EzShelterElevator destination)
    {
        if (destination.Schematic is null || destination._waypoint is null)
        {
            IsTransitioning = false;
            return;
        }

        Vector3 start = _waypoint!.Position;
        Waypoint destinationWaypoint = destination._waypoint;

        SpeakerApi.Playback moving = PlayAudio(MovingAudio, DefaultMovingAudio, start, "moving");

        string movingAudio = ResolveAudio(MovingAudio, DefaultMovingAudio);
        float movingDuration = string.IsNullOrWhiteSpace(movingAudio)
            ? RideDuration
            : Math.Max(RideDuration, SpeakerApi.GetClipDuration(movingAudio.Trim()));

        ScheduleDelayed(TeleportDelay, () =>
        {
            TeleportOccupants(destinationWaypoint);
            if (moving.IsValid)
                SpeakerApi.SetTransform(moving, destinationWaypoint.Position);
        });

        ScheduleDelayed(movingDuration, () => CompleteTransition(destination));
    }

    /// <summary>
    /// 移動音の再生終了後に呼ばれる。乗客のテレポートは <see cref="BeginMoving"/> 側で既に完了している前提で、
    /// 到着階のドアを開けて扉が開く音を鳴らす。ドアが開き終わるまで IsTransitioning を維持し、
    /// 以後のボタン操作をブロックする。
    /// </summary>
    private void CompleteTransition(EzShelterElevator destination)
    {
        if (destination.Schematic is null || destination._waypoint is null)
        {
            IsTransitioning = false;
            return;
        }

        destination.PlayAnimation(destination.DoorOpeningSideFlag, false);
        destination.PlayAudio(destination.DoorOpenAudio, DefaultDoorOpenAudio, destination._waypoint.Position, "dooropen");

        Animator? openingAnimator = destination.GetSideAnimator(destination.DoorOpeningSideFlag);
        destination.ScheduleAfterAnimatorState(openingAnimator, "opening", TransitionFallbackDuration, () => IsTransitioning = false);
    }

    private SpeakerApi.Playback PlayAudio(string? audio, string fallback, Vector3 position, string suffix)
    {
        string resolved = ResolveAudio(audio, fallback);
        if (string.IsNullOrWhiteSpace(resolved))
            return default;

        try
        {
            return SpeakerApi.Play(
                resolved.Trim(),
                $"ezElevator_{ObjectInstanceID}_{suffix}",
                position,
                destroyOnEnd: true,
                isSpatial: AudioSpatial,
                maxDistance: AudioMaxDistance,
                minDistance: AudioMinDistance,
                volume: AudioVolume);
        }
        catch (Exception e)
        {
            Log.Warn($"[EzShelterElevator] Failed to play audio '{resolved}': {e.Message}");
            return default;
        }
    }

    /// <summary>設定値が NullOrEmpty なら PreloadHandler がプリロードするデフォルトパスにフォールバックする。</summary>
    private static string ResolveAudio(string? configured, string fallback)
        => string.IsNullOrEmpty(configured) ? fallback : configured;

    /// <summary>
    /// かご（このインスタンスの Waypoint の Bounds 内）にいるプレイヤー・アイテムを、
    /// 互いの相対位置を保ったまま移動先の Waypoint 位置へテレポートする。
    /// </summary>
    private void TeleportOccupants(Waypoint destination)
    {
        Bounds bounds = _waypoint!.Bounds;
        Vector3 origin = _waypoint.Position;
        Vector3 target = destination.Position;

        foreach (Player rider in Player.List)
        {
            if (!bounds.Contains(rider.Position)) continue;
            rider.Position = target + (rider.Position - origin);
        }

        foreach (Pickup pickup in Pickup.List)
        {
            if (!bounds.Contains(pickup.Position)) continue;
            pickup.Position = target + (pickup.Position - origin);
        }
    }

    private bool IsHostOfSession()
    {
        return GlobalLevel == LocalLevel;
    }

    private const string ButtonTagProperty = "ObjectPrefabTag";
    private const string BackTagValue = "Back";

    /// <summary>
    /// ボタンの方向は Unity 側(ObjectPrefabSchematicInfo.Tag)で焼き込んだ "Next"/"Back" タグを優先する。
    /// Inner/Outer はあくまで設置場所の分類であり、方向とは独立に何個でも Next/Back を割り当てられる。
    /// タグが未設定の場合のみ、後方互換として Outer=戻る/Inner=進む にフォールバックする。
    /// </summary>
    private bool IsBackButton(InteractableHandle handle)
    {
        SchematicBlock? block = GetBlock(handle.Key);
        if (block?.Data?.Properties != null &&
            block.Data.Properties.TryGetValue(ButtonTagProperty, out object tag) &&
            tag is string tagText && !string.IsNullOrWhiteSpace(tagText))
        {
            return string.Equals(tagText.Trim(), BackTagValue, StringComparison.OrdinalIgnoreCase);
        }

        return _outerButtons.Contains(handle);
    }

    /// <summary>
    /// キー(ObjectPrefabSchematicInfo)採用済みブロックを優先し、無ければブロック名(GameObject名)の
    /// 完全一致で探す。"SideA"/"SideB" に Info が付いていてもキー未設定のことがあるため、
    /// <see cref="ProjectMER.Features.Objects.SchematicObject.FindBlockByKey"/> 単体には頼らない。
    /// </summary>
    private SchematicBlock? GetSideBlock(string name)
        => GetBlock(name) ?? Schematic?.FindBlock(name, allowPartial: false);

    private Animator? GetSideAnimator(DoorOpeningSideFlag sideFlag)
    {
        string name = sideFlag == DoorOpeningSideFlag.SideB ? "SideB" : "SideA";
        SchematicBlock? block = GetSideBlock(name);
        if (string.IsNullOrEmpty(block?.AnimatorName)) return null;

        return Schematic?.AnimationController.Animators.FirstOrDefault(a => a.name == block.BlockName);
    }

    private void PlayAnimation(DoorOpeningSideFlag sideFlag, bool isClose)
    {
        if (Schematic is null) return;
        switch (sideFlag)
        {
            case DoorOpeningSideFlag.SideA:
            case DoorOpeningSideFlag.SideB:
            {
                SchematicBlock? block = GetSideBlock(sideFlag.ToString());
                if (string.IsNullOrEmpty(block?.AnimatorName)) return;
                Schematic.AnimationController.Play(isClose ? "closing" : "opening", animatorName: block.BlockName);
                SetJamDoorState(isClose);
                break;
            }
            case DoorOpeningSideFlag.Both:
            {
                SchematicBlock? blockA = GetSideBlock("SideA");
                SchematicBlock? blockB = GetSideBlock("SideB");
                if (string.IsNullOrEmpty(blockA?.AnimatorName) || string.IsNullOrEmpty(blockB?.AnimatorName)) return;
                Schematic.AnimationController.Play(isClose ? "closing" : "opening", animatorName: blockA.BlockName);
                Schematic.AnimationController.Play(isClose ? "closing" : "opening", animatorName: blockB.BlockName);
                SetJamDoorState(isClose);
                break;
            }
            case DoorOpeningSideFlag.Custom:
                CustomAnimation();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sideFlag), sideFlag, null);
        }
    }

    private void CustomAnimation() {}
}
