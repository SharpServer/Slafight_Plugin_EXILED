using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Trashbox : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.2f;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;
    private static readonly Vector3 InteractableLocalOffset = Vector3.zero;
    private static readonly Vector3 InteractableBaseScale = Vector3.one + Vector3.up * 2f;
    private readonly Dictionary<int, List<TrashboxEventType>> _triggeredEventsByPlayer = [];
    private static readonly Dictionary<int, byte> TriggeredSecretCountsByPlayer = [];

    public int TriggeredEventCount => _triggeredEventsByPlayer.Values.Sum(events => events.Count);
    public IReadOnlyDictionary<int, List<TrashboxEventType>> TriggeredEventsByPlayer => _triggeredEventsByPlayer;
    public static IReadOnlyDictionary<int, byte> TriggeredSecretCounts => TriggeredSecretCountsByPlayer;
    public static bool HimselfTriggered { get; private set; }
    protected override void OnCreate()
    {
         _schematicObject = SpawnManagedSchematic("trashbox");
         _triggeredEventsByPlayer.Clear();

         ScheduleDelayed(0.5f, CreateInteractableToy);
         base.OnCreate();
    }

    private void CreateInteractableToy()
    {
        _interactableToy = CreateManagedInteractable(
            interactionDuration: 5f,
            shape: InvisibleInteractableToy.ColliderShape.Box,
            localOffset: InteractableLocalOffset,
            baseScale: InteractableBaseScale);
    }

    protected override void OnDestroy()
    {
        _schematicObject = null;
        _interactableToy = null;
        base.OnDestroy();
    }
    
    public enum TrashboxEventType
    {
        Nothing,
        Painkillers,
        NeutralizeGrenade,
        MagicMissile,
        MasterCard,
        Quarter,
        Zombie,
        Secret
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null)
            return;

        var pos = _schematicObject?.Position ?? Position;
        var triggeredEvents = GetTriggeredEvents(player.Id);

        if (triggeredEvents.Count >= 3)
        {
            player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                            "しかし、何も見つからなかった。\n" +
                            "<color=yellow>もうここには何もないようだ</color></size>",5);
            return;
        }

        var values = Enum.GetValues(typeof(TrashboxEventType))
            .Cast<TrashboxEventType>()
            .ToArray();

        var elected = values[UnityEngine.Random.Range(0, values.Length)];
        if (HimselfTriggered && elected is TrashboxEventType.Zombie)
        {
            elected = TrashboxEventType.Secret;
        }
        triggeredEvents.Add(elected);

        switch (elected)
        {
            case TrashboxEventType.Nothing:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "しかし、何も見つからなかった。</size>",5);
                break;
            case TrashboxEventType.Painkillers:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=yellow>鎮痛剤を手に入れた！</color></size>",5);
                player.GiveOrDrop(ItemType.Painkillers);
                break;
            case TrashboxEventType.NeutralizeGrenade:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=yellow>無力化グレネードを手に入れた！</color></size>",5);
                player.GiveOrDrop<NeutralizeGrenade>();
                break;
            case TrashboxEventType.MagicMissile:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=yellow>マジックミサイルを手に入れた！</color></size>",5);
                player.GiveOrDrop<MagicMissile>();
                break;
            case TrashboxEventType.MasterCard:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=yellow>マスターカードを手に入れた！</color></size>",5);
                player.GiveOrDrop<MasterCard>();
                break;
            case TrashboxEventType.Quarter:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=yellow>硬貨を手に入れた！</color></size>",5);
                player.GiveOrDrop<Quarter>();
                break;
            case TrashboxEventType.Zombie:
                player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                                "<color=red>ワオ！なぞの死体が出てきた。</color></size>",5);
                var ragdoll = Ragdoll.SpawnRagdoll(RoleTypeId.Scp0492, player.Position, Quaternion.identity, new CustomReasonDamageHandler("???"), "Dr. Redheart");
                CItem.Get<KeycardHimself>()?.Spawn(ragdoll?.Position ?? player.Position + Vector3.up * 0.25f);
                HimselfTriggered = true;
                break;
            case TrashboxEventType.Secret:
                var triggeredSecretCount = GetTriggeredSecretCount(player.Id);
                var text = triggeredSecretCount switch
                {
                    0 => "何だか安心感を与える曲が流れてきた。",
                    1 => "頭が活性化するような曲が流れてきた。",
                    2 => "不安を与える曲が流れてきた。",
                    3 => "虚無になる曲が流れてきた。",
                    4 => $"<b><color={CTeam.Fifthists.GetTeamColor()}>星のシグナル</color></b>を感じる曲が流れてきた・・・！",
                    _ => string.Empty
                };
                var combinedText = "<size=26>あなたはゴミ箱を漁った・・・\n" +
                                   $"<color=yellow>{text}</color></size>";
                player.ShowHint(combinedText, 10);
                var random = new Random();
                var songName = random.Next(0, 5) switch
                {
                    0 => "5egg_0.ogg",
                    1 => "5egg_1.ogg",
                    2 => "5egg_2.ogg",
                    3 => "5egg_3.ogg",
                    4 => "5egg_4.ogg",
                    _ => string.Empty
                };
                SpeakerApi.Play(songName, "Trashbox___PLS_HL_55555", pos, true, null, false, 5f, 0f);
                triggeredSecretCount++;
                if (triggeredSecretCount >= 4)
                {
                    TriggeredSecretCountsByPlayer[player.Id] = 0;
                    player.EnableEffect<Flashed>(255, 5);
                    Timing.CallDelayed(5, () => player?.SetRole(CRoleTypeId.FifthistConvert, RoleSpawnFlags.AssignInventory));
                }
                else
                {
                    TriggeredSecretCountsByPlayer[player.Id] = triggeredSecretCount;
                }
                break;
        }
    }

    protected override void OnRoundRestarting()
    {
        ResetSharedRoundState();
        base.OnRoundRestarting();
    }

    private List<TrashboxEventType> GetTriggeredEvents(int playerId)
    {
        if (_triggeredEventsByPlayer.TryGetValue(playerId, out var triggeredEvents))
            return triggeredEvents;

        triggeredEvents = [];
        _triggeredEventsByPlayer[playerId] = triggeredEvents;
        return triggeredEvents;
    }

    private static byte GetTriggeredSecretCount(int playerId)
        => TriggeredSecretCountsByPlayer.TryGetValue(playerId, out var count) ? count : (byte)0;

    public static void ResetSharedRoundState()
    {
        TriggeredSecretCountsByPlayer.Clear();
        HimselfTriggered = false;
    }
}
