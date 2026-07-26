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

    public int LocalLevel { get; set; } = 0;
    public DoorOpeningSideFlag DoorOpeningSideFlag { get; set; } = DoorOpeningSideFlag.SideA;

    /// <summary>扉が閉まり、移動を開始した直後に鳴らす音(モーター音など)。空なら再生しない。</summary>
    public string MovingAudio { get; set; } = string.Empty;

    /// <summary>到着階に着いた直後、扉が開く前に鳴らす音(到着チャイムなど)。空なら再生しない。</summary>
    public string ArrivalAudio { get; set; } = string.Empty;

    /// <summary>移動音の再生開始から到着音を鳴らすまでの最短待機秒数。実際のクリップ長がこれより長ければそちらを優先する。</summary>
    public float RideDuration { get; set; } = 4f;

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
    } = 0;

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

        PlayAnimation(DoorOpeningSideFlag.Both, true);
        if (IsHostOfSession())
        {
            PlayAnimation(DoorOpeningSideFlag, false);
        }
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

        // 1. 出発階のドアを閉め、移動音を鳴らす。
        PlayAnimation(DoorOpeningSideFlag, true);
        PlayAudio(MovingAudio, _waypoint.Position, "moving");

        // 2. 移動音の実クリップ長と RideDuration の長い方だけ待ってから到着処理に入る。
        float rideDuration = string.IsNullOrWhiteSpace(MovingAudio)
            ? RideDuration
            : Math.Max(RideDuration, SpeakerApi.GetClipDuration(MovingAudio.Trim()));

        ScheduleDelayed(rideDuration, () => CompleteTransition(item));
    }

    /// <summary>
    /// 到着音を鳴らし、乗客をテレポートしてから到着階のドアを開ける。
    /// ドアが開き終わるまで IsTransitioning を維持し、以後のボタン操作をブロックする。
    /// </summary>
    private void CompleteTransition(EzShelterElevator destination)
    {
        if (destination.Schematic is null || destination._waypoint is null)
        {
            IsTransitioning = false;
            return;
        }

        destination.PlayAudio(destination.ArrivalAudio, destination._waypoint.Position, "arrival");
        TeleportOccupants(destination._waypoint);
        destination.PlayAnimation(destination.DoorOpeningSideFlag, false);

        Animator? openingAnimator = destination.GetSideAnimator(destination.DoorOpeningSideFlag);
        destination.ScheduleAfterAnimatorState(openingAnimator, "opening", TransitionFallbackDuration, () => IsTransitioning = false);
    }

    private void PlayAudio(string? audio, Vector3 position, string suffix)
    {
        if (string.IsNullOrWhiteSpace(audio))
            return;

        try
        {
            SpeakerApi.Play(
                audio.Trim(),
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
            Log.Warn($"[EzShelterElevator] Failed to play audio '{audio}': {e.Message}");
        }
    }

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
                break;
            }
            case DoorOpeningSideFlag.Both:
            {
                SchematicBlock? blockA = GetSideBlock("SideA");
                SchematicBlock? blockB = GetSideBlock("SideB");
                if (string.IsNullOrEmpty(blockA?.AnimatorName) || string.IsNullOrEmpty(blockB?.AnimatorName)) return;
                Schematic.AnimationController.Play(isClose ? "closing" : "opening", animatorName: blockA.BlockName);
                Schematic.AnimationController.Play(isClose ? "closing" : "opening", animatorName: blockB.BlockName);
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
