using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using CustomPlayerEffects;
using Exiled.API.Extensions;
using Exiled.API.Features.Items;
using InventorySystem.Items.Keycards;
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
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;
using Item = Exiled.API.Features.Items.Item;
using KeycardPickup = Exiled.API.Features.Pickups.KeycardPickup;
using Pickup = Exiled.API.Features.Pickups.Pickup;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Trashbox : ObjectPrefab
{
    public override float ToySearchRadius { get; set; } = 1.2f;

    private SchematicObject? _schematicObject;
    private InteractableToy? _interactableToy;
    private static readonly Vector3 InteractableLocalOffset = Vector3.zero;
    private static readonly Vector3 InteractableBaseScale = Vector3.one + Vector3.up * 2f;
    public static int TriggeredEventCount => TriggeredEvents.Count;
    public static byte TriggeredSecretCount;
    public static List<TrashboxEventType> TriggeredEvents { get; private set; }

    private static readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;
    
    protected override void OnCreate()
    {
         _schematicObject = SpawnManagedSchematic("trashbox");

         Timing.CallDelayed(0.5f, CreateInteractableToy);
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
        var pos = _schematicObject?.Position ?? Position;
        if (TriggeredEventCount >= 3)
        {
            player.ShowHint("<size=26>あなたはゴミ箱を漁った・・・\n" +
                            "しかし、何も見つからなかった。\n" +
                            "<color=yellow>もうここには何もないようだ</color></size>",5);
        }

        var values = Enum.GetValues(typeof(TrashboxEventType))
            .Cast<TrashboxEventType>()
            .ToArray();

        var elected = values[UnityEngine.Random.Range(0, values.Length)];
        TriggeredEvents.Add(elected);

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
                break;
            case TrashboxEventType.Secret:
                var text = TriggeredSecretCount switch
                {
                    0 => "何だか安心感を与える曲が流れてきた。",
                    1 => "頭が活性化するような曲が流れてきた。",
                    2 => "不安を与える曲が流れてきた。",
                    3 => "虚無になる曲が流れてきた。",
                    4 => $"<b><color={CTeam.Fifthists.GetTeamColor()}>星のシグナル</color></b>を感じる曲が流れてきた・・・！"
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
                    4 => "5egg_4.ogg"
                };
                SpeakerApi.CreateOrGetSpeaker(songName, Position, null, "Trashbox___PLS_HL_55555");
                if (TriggeredSecretCount >= 4)
                {
                    TriggeredSecretCount = 0;
                    player.EnableEffect<Flashed>(255, 5);
                    Timing.CallDelayed(5, () => player?.SetRole(CRoleTypeId.FifthistConvert, RoleSpawnFlags.AssignInventory));
                }
                break;
        }
    }
}
