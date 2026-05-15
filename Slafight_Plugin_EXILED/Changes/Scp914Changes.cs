using Exiled.API.Enums;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Scp914;
using MEC;
using PlayerRoles;
using Scp914;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.Scp914;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Changes;

/// <summary>
/// SCP-914 のアップグレード挙動を一元管理するファイル。
/// </summary>
public static class Scp914Changes
{
    private static readonly Random Random = new();

    public static void Register()
    {
        RegisterRules();

        Exiled.Events.Handlers.Scp914.UpgradingPickup += OnUpgradingPickup;
        Exiled.Events.Handlers.Scp914.UpgradingInventoryItem += OnUpgradingInventoryItem;
        Exiled.Events.Handlers.Scp914.UpgradingPlayer += Human;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Scp914.UpgradingPickup -= OnUpgradingPickup;
        Exiled.Events.Handlers.Scp914.UpgradingInventoryItem -= OnUpgradingInventoryItem;
        Exiled.Events.Handlers.Scp914.UpgradingPlayer -= Human;

        Scp914Registry.Clear();
    }

    private static void RegisterRules()
    {
        RegisterWildcard();
        RegisterVanillaRules();
        RegisterCustomItemRules();
        RegisterCItemRules();
    }

    /// <summary>
    /// 全アイテム共通の O5 超低確率ロール + Scp513/CapybaraMissile ワイルドカード。
    /// </summary>
    private static void RegisterWildcard()
    {
        // どのアイテムでも 0.2% で O5 に化ける共通ロール
        Scp914Registry.O5WildcardRule = Scp914Rule
            .ToVanilla(ItemType.KeycardO5)
            .WithChance(0.002f); // 0.2% = 1/500

        // 既存の Scp513 / CapybaraMissile ロール
        Scp914Registry.WildcardRule = Scp914Rule.Weighted(
            (1f / 10f, Scp914Rule.ToCItem<Scp513Item>()),
            (9f / 10f, Scp914Rule.ToCItem<CapybaraMissile>())
        ).WithChance(1f / 12f);
    }

    /// <summary>
    /// vanilla ItemType → Custom/CItem の変換ルール。
    /// （キーカード周りを全面改修）
    /// </summary>
    private static void RegisterVanillaRules()
    {
        // ===== 非キー系（元のまま） =====

        Scp914Registry.RegisterVanilla(ItemType.Adrenaline, new()
        {
            VeryFine = Scp914Rule.ToCItem<SerumD>(),
        });

        Scp914Registry.RegisterVanilla(ItemType.SCP500, new()
        {
            Fine = Scp914Rule.ToCItem<ClassXMemoryForcePil>(),
            VeryFine = Scp914Rule.ToCItem<ClassZMemoryForcePil>().WithChance(1f / 4f)
        });

        // ===== キーカード系 =====
        // 施設系: Janitor < Scientist < ZoneManager < ResearchCoordinator < ContainmentEngineer < FacilityManager < SiteDirector < O5
        // 軍系: Armory1 < Guard = ChaosIntruder < Armory2 < SecurityChief < MTFPrivate < MTFOperative < MTFCaptain = ChaosInsurgency < SiteDirector < O5

        // --- Janitor ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardJanitor, new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<Quarter>().Times(2),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.Weighted(
                (0.7f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.3f, Scp914Rule.ToCItem<KeycardArmoryLevel1>())),
            VeryFine = Scp914Rule.Weighted(
                (0.05f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (0.95f, Scp914Rule.Destroy)),
        });

        // --- Scientist ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardScientist, new()
        {
            Rough = Scp914Rule.ToCItem<Quarter>().Times(2),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardJanitor),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardZoneManager),
            VeryFine = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardZoneManager)),
                (0.5f, Scp914Rule.ToCItem<KeycardSiteNavigator>())),
        });

        // --- Zone Manager ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardZoneManager, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardJanitor)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
            VeryFine = Scp914Rule.Weighted(
                (0.4f, Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator)),
                (0.4f, Scp914Rule.ToCItem<KeycardSurveillance>()),
                (0.2f, Scp914Rule.ToCItem<KeycardChaosIntruder>())),
        });

        // --- Research Coordinator ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardResearchCoordinator, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardJanitor))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardContainmentEngineer),
            VeryFine = Scp914Rule.Weighted(
                (0.25f, Scp914Rule.ToVanilla(ItemType.KeycardContainmentEngineer)),
                (0.75f, Scp914Rule.Destroy)),
        });

        // --- Containment Engineer ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardContainmentEngineer, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardFacilityManager),
            VeryFine = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (0.5f, Scp914Rule.Destroy)),
        });

        // --- Facility Manager ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardFacilityManager, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardContainmentEngineer),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToCItem<KeycardSiteDirector>(),
            VeryFine = Scp914Rule.Weighted(
                (0.25f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (0.75f, Scp914Rule.Destroy)),
        });

        // --- Guard (軍系 W1) ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardGuard, new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<Quarter>().Times(2),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel2>(),
            VeryFine = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToCItem<KeycardSecurityChief>()),
                (0.5f, Scp914Rule.ToCItem<KeycardChaosIntruder>())),
        });

        // --- MTF Private (W4) ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFPrivate, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardContainmentEngineer),
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
            VeryFine = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative)),
                (0.5f, Scp914Rule.ToCItem<KeycardArmoryLevel3>())),
        });

        // --- MTF Operative (W5) ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFOperative, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardGuard)),
                (0.5f, Scp914Rule.Destroy)),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel3>(),
            VeryFine = Scp914Rule.Weighted(
                (1f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative)),
                (1f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain)),
                (1f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardO5))),
        });

        // --- MTF Captain (W6, ChaosInsurgency と同格) ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFCaptain, new()
        {
            Rough = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardChaosInsurgency),
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardO5),
            VeryFine = Scp914Rule.Weighted(
                (0.25f, Scp914Rule.Destroy),
                (0.75f, Scp914Rule.ToVanilla(ItemType.KeycardO5))),
        });

        // --- Chaos Insurgency (W6 同格) ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardChaosInsurgency, new()
        {
            Rough = Scp914Rule.ToCItem<KeycardConscripts>(),
            Coarse = Scp914Rule.ToCItem<KeycardChaosIntruder>(),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain),
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel3>(),
            VeryFine = Scp914Rule.Weighted(
                (2f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (1f / 3f, Scp914Rule.Destroy)),
        });

        // --- O5 ---
        Scp914Registry.RegisterVanilla(ItemType.KeycardO5, new()
        {
            Rough = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardZoneManager))),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardFacilityManager),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToCItem<MasterCard>(),
            VeryFine = Scp914Rule.ToCItem<MasterCard>().WithChance(0.5f),
        });

        // ===== 非キー系 Vanilla の残り（元のまま） =====

        Scp914Registry.RegisterVanilla(ItemType.Radio, new()
        {
            VeryFine = Scp914Rule.ToCItem<SNAV300>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.MicroHID, new()
        {
            Coarse = Scp914Rule.ToCItem<HIDTurret>().WithChance(1f / 2f),
        });
        Scp914Registry.RegisterVanilla(ItemType.GrenadeFlash, new()
        {
            Fine = Scp914Rule.ToCItem<FlashBangE>().WithChance(1f / 3f),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP268, new()
        {
            VeryFine = Scp914Rule.ToCItem<CloakGenerator>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.Coin, new()
        {
            Coarse = Scp914Rule.ToCItem<Quarter>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.GunRevolver, new()
        {
            Fine = Scp914Rule.ToCItem<GunTacticalRevolver>().WithChance(1f / 2f),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP244a, new()
        {
            Fine = Scp914Rule.ToCItem<ThrowableScp244>(),
            VeryFine = Scp914Rule.ToCItem<ThrowableScp244>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP244b, new()
        {
            Fine = Scp914Rule.ToCItem<ThrowableScp244>(),
            VeryFine = Scp914Rule.ToCItem<ThrowableScp244>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP1344, new()
        {
            Coarse = Scp914Rule.ToCItem<NvgBlue>(),
        });
    }

    /// <summary>Exiled CustomItem 固有の変換ルール (将来の CustomItem 専用ルール用に確保)。</summary>
    private static void RegisterCustomItemRules()
    {
    }

    /// <summary>CItem → Vanilla/CItem の変換ルール。</summary>
    /// <summary>CItem → Vanilla/CItem の変換ルール。</summary>
    private static void RegisterCItemRules()
    {
        // ===== Fifthist / その他 SCP 系 =====

        Scp914Registry.RegisterCItem<KeycardFifthist>(new()
        {
            All = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<Scp1425>(),
            Fine = Scp914Rule.ToCItem<KeycardFifthistPriest>(),
            VeryFine = Scp914Rule.ToCItem<MagicMissile>().WithChance(1f / 3f),
        });

        Scp914Registry.RegisterCItem<KeycardFifthistPriest>(new()
        {
            All = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<KeycardFifthist>(),
            Fine = Scp914Rule.ToCItem<MagicMissile>(),
            VeryFine = Scp914Rule.ToCItem<CaneOfTheStars>(),
        });

        Scp914Registry.RegisterCItem<Scp1425>(new()
        {
            All = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<GoCRecruitPaper>(),
        });

        Scp914Registry.RegisterCItem<GoCRecruitPaper>(new()
        {
            All = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<Scp1425>(),
        });

        // ===== NVG / HID / Railgun / Serum / SNAV など（元のまま） =====

        Scp914Registry.RegisterCItem<NvgNormal>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<NvgNormal>(),
            Fine = Scp914Rule.ToCItem<NvgRed>(),
            VeryFine = Scp914Rule.ToCItem<NvgBlue>(),
        });

        Scp914Registry.RegisterCItem<HIDTurret>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<HIDTurret>(),
            Fine = Scp914Rule.ToVanilla(ItemType.MicroHID),
            VeryFine = Scp914Rule.ToCItem<GunGoCTurret>(),
        });

        Scp914Registry.RegisterCItem<GunGoCRailgun>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.ParticleDisruptor),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.Keep,
            VeryFine = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
        });

        Scp914Registry.RegisterCItem<SerumD>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.Adrenaline),
            OneToOne = Scp914Rule.Passthrough,
            Fine = Scp914Rule.ToCItem<SerumC>(),
            VeryFine = Scp914Rule.ToCItem<SerumC>(),
        });

        Scp914Registry.RegisterCItem<SerumC>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<SerumD>(),
            OneToOne = Scp914Rule.ToCItem<SerumC>(),
            Fine = Scp914Rule.ToCItem<SerumC>(),
            VeryFine = Scp914Rule.ToCItem<SerumC>(),
        });

        Scp914Registry.RegisterCItem<ClassXMemoryForcePil>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.SCP500),
            OneToOne = Scp914Rule.Passthrough,
            Fine = Scp914Rule.ToCItem<ClassZMemoryForcePil>(),
            VeryFine = Scp914Rule.ToCItem<ClassZMemoryForcePil>(),
        });

        Scp914Registry.RegisterCItem<SNAV300>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        Scp914Registry.RegisterCItem<SNAV310>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        Scp914Registry.RegisterCItem<SNAVUltimate>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        // ===== ここからキーカード CItem 系 =====
        // 施設系: Janitor < Scientist < ZoneManager < ResearchCoordinator < ContainmentEngineer < FacilityManager < SiteDirector < O5
        // 軍系: Armory1 < Guard = ChaosIntruder < Armory2 < SecurityChief < MTFPrivate < MTFOperative < MTFCaptain = ChaosInsurgency < SiteDirector < O5

        // --- Site Navigator（施設系中級） ---
        Scp914Registry.RegisterCItem<KeycardSiteNavigator>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
            Fine = Scp914Rule.ToCItem<KeycardChaosIntruder>(),
            VeryFine = Scp914Rule.Weighted(
                (2f / 3f, Scp914Rule.ToCItem<KeycardArmoryLevel2>()),
                (1f / 3f, Scp914Rule.ToCItem<MasterCard>())),
        });

        // --- Surveillance（軍寄り中級） ---
        Scp914Registry.RegisterCItem<KeycardSurveillance>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardJanitor),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel2>(),
            VeryFine = Scp914Rule.ToCItem<KeycardSecurityChief>(),
        });

        // --- Site Director（最上位クラス。VF で O5 高確率） ---
        Scp914Registry.RegisterCItem<KeycardSiteDirector>(new()
        {
            Rough = Scp914Rule.ToVanilla(ItemType.KeycardFacilityManager),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardContainmentEngineer),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardO5),
            VeryFine = Scp914Rule.Weighted(
                (0.66f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (0.34f, Scp914Rule.ToVanilla(ItemType.KeycardJanitor))),
        });

        // --- Armory Level 1（軍系 W0） ---
        Scp914Registry.RegisterCItem<KeycardArmoryLevel1>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            Fine = Scp914Rule.ToCItem<KeycardChaosIntruder>(),
            VeryFine = Scp914Rule.ToCItem<KeycardConscripts>(),
        });

        // --- Armory Level 2（軍系 W2） ---
        Scp914Registry.RegisterCItem<KeycardArmoryLevel2>(new()
        {
            Rough = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            Coarse = Scp914Rule.ToCItem<KeycardArmoryLevel1>(),
            OneToOne = Scp914Rule.ToCItem<KeycardConscripts>(),
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel3>(),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
        });

        // --- Armory Level 3（軍系上位武装） ---
        Scp914Registry.RegisterCItem<KeycardArmoryLevel3>(new()
        {
            Rough = Scp914Rule.ToCItem<KeycardArmoryLevel2>(),
            Coarse = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
            OneToOne = Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain),
            Fine = Scp914Rule.Weighted(
                (2f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (1f / 3f, Scp914Rule.ToVanilla(ItemType.KeycardChaosInsurgency))),
            VeryFine = Scp914Rule.Weighted(
                (3f / 4f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (1f / 4f, Scp914Rule.ToCItem<MasterCard>())),
        });

        // --- Chaos Intruder（軍系 W1。同格は Guard） ---
        Scp914Registry.RegisterCItem<KeycardChaosIntruder>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<KeycardConscripts>(),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardChaosInsurgency),
            VeryFine = Scp914Rule.ToCItem<KeycardArmoryLevel3>(),
        });

        // --- Conscripts（Chaos 下位） ---
        Scp914Registry.RegisterCItem<KeycardConscripts>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<KeycardChaosIntruder>(),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToVanilla(ItemType.KeycardChaosInsurgency),
            // 1/5 で O5, 4/5 Destroy
            VeryFine = Scp914Rule.Weighted(
                (1f / 5f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (4f / 5f, Scp914Rule.Destroy)),
        });

        // --- Security Chief（軍系 W3） ---
        Scp914Registry.RegisterCItem<KeycardSecurityChief>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<KeycardArmoryLevel1>(),
            OneToOne = Scp914Rule.ToCItem<KeycardSecurityChief>(),
            Fine = Scp914Rule.ToCItem<KeycardArmoryLevel2>(),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain),
        });

        // --- MasterCard（ギャンブルキー） ---
        Scp914Registry.RegisterCItem<MasterCard>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<Quarter>().Times(8),
            OneToOne = Scp914Rule.ToCItem<PlayingCard>(),
            Fine = Scp914Rule.Weighted(
                (0.4f, Scp914Rule.ToVanilla(ItemType.KeycardScientist)),
                (0.3f, Scp914Rule.ToCItem<KeycardSiteNavigator>()),
                (0.3f, Scp914Rule.ToCItem<KeycardArmoryLevel2>())),
            VeryFine = Scp914Rule.Weighted(
                (0.05f, Scp914Rule.ToVanilla(ItemType.KeycardO5)), // 5% O5
                (0.25f, Scp914Rule.ToCItem<KeycardSiteDirector>()), // 25% SiteDirector
                (0.20f, Scp914Rule.ToVanilla(ItemType.KeycardJanitor)), // 20% Janitor 戻り
                (0.50f, Scp914Rule.Destroy)), // 50% 失敗
        });

        // --- PlayingCard（MasterCard と相互変換＋中級ルート） ---
        Scp914Registry.RegisterCItem<PlayingCard>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<Quarter>().Times(8),
            OneToOne = Scp914Rule.ToCItem<MasterCard>(),
            Fine = Scp914Rule.ToCItem<KeycardSiteNavigator>(),
            VeryFine = Scp914Rule.ToCItem<KeycardSurveillance>(),
        });

        // --- Quarter（コイン素材） ---
        Scp914Registry.RegisterCItem<Quarter>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.Destroy,
            Fine = Scp914Rule.ToVanilla(ItemType.Coin),
            VeryFine = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardJanitor)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.KeycardScientist))),
        });

        // ===== 残りの SCP 武器系 CItem （Schwarzschild, 7381 など）は元のまま =====

        Scp914Registry.RegisterCItem<CloakGenerator>(new()
        {
            All = Scp914Rule.Destroy,
            Rough = Scp914Rule.ToVanilla(ItemType.SCP268),
            Coarse = Scp914Rule.ToVanilla(ItemType.SCP268),
            OneToOne = Scp914Rule.Keep
        });

        Scp914Registry.RegisterCItem<CaneOfTheStars>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToCItem<KeycardFifthistPriest>(),
            OneToOne = Scp914Rule.ToVanilla(ItemType.SCP1509),
            Fine = Scp914Rule.Keep,
            VeryFine = Scp914Rule.Weighted(
                (1f / 77f, Scp914Rule.ToCItem<SchwarzschildQuasar>()),
                (76f / 77f, Scp914Rule.Destroy)
            ),
        });

        Scp914Registry.RegisterCItem<ThrowableScp244>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.SCP244a)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.SCP244b))
            ),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.Destroy,
            VeryFine = Scp914Rule.Destroy,
        });

        Scp914Registry.RegisterCItem<SchwarzschildQuasar>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.ToVanilla(ItemType.Jailbird),
            OneToOne = Scp914Rule.ToCItem<CaneOfTheStars>(),
            Fine = Scp914Rule.Keep,
            VeryFine = Scp914Rule.ToCItem<SchwarzschildRailbreaker>()
        });

        Scp914Registry.RegisterCItem<SchwarzschildRailbreaker>(new()
        {
            Rough = Scp914Rule.ToCItem<SchwarzschildQuasar>(),
            Coarse = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.Keep,
            VeryFine = Scp914Rule.Custom(ctx =>
            {
                FullyRemoveItem(ctx);
                for (int i = 0; i < 5; i++)
                {
                    if (Pickup.CreateAndSpawn(ItemType.GrenadeHE, ctx.OutputPosition) is GrenadePickup grenade)
                        grenade.Explode();
                }
            }),
        });

        Scp914Registry.RegisterCItem<GunScp7381>(new()
        {
            Rough = Scp914Rule.Destroy,
            Coarse = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.Keep,
            Fine = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
            VeryFine = Scp914Rule.Custom(ctx =>
            {
                FullyRemoveItem(ctx);
                for (int i = 0; i < 5; i++)
                {
                    if (Pickup.CreateAndSpawn(ItemType.GrenadeHE, ctx.OutputPosition) is GrenadePickup grenade)
                        grenade.Explode();
                }
            }),
        });
    }

    // =====================================================================
    // ディスパッチ
    // =====================================================================

    /// <summary>
    /// 優先順位:
    /// 1. Wildcard (1/6) — 当選で Scp513 / CapybaraMissile に置き換え、他の処理打ち切り
    /// 2. CustomItem (Registry ヒット) — Registry 経由で変換
    /// 3. CItem (Registry ヒット) — Registry 経由で変換
    /// 4. Vanilla (Registry ヒット) — Registry 経由で変換
    /// どれにも当たらない場合は何もしない (vanilla 914 / CItem デフォルト挙動に任せる)
    /// </summary>
    private static void OnUpgradingPickup(UpgradingPickupEventArgs ev)
    {
        if (ev?.Pickup == null) return;

        // ① O5 共通超低確率ロール
        if (Scp914Registry.O5WildcardRule is { } o5wild
            && Scp914Dispatcher.ApplyPickup(o5wild, ev))
        {
            return;
        }

        // ② 既存の Wildcard (Scp513 / CapybaraMissile)
        if (Scp914Registry.WildcardRule is { } wildcard
            && Scp914Dispatcher.ApplyPickup(wildcard, ev))
        {
            return;
        }

        // ③ CustomItem → CItem → Vanilla は元通り
        if (ev.Pickup.TryGetCustomItem(out var customItem) && customItem != null)
        {
            if (Scp914Registry.TryGetForCustomItem(customItem, out var customRules)
                && customRules != null
                && customRules.Get(ev.KnobSetting) is { } customRule)
            {
                Scp914Dispatcher.ApplyPickup(customRule, ev);
            }

            return;
        }

        if (CItem.TryGet(ev.Pickup, out var cItem) && cItem != null)
        {
            if (Scp914Registry.TryGetForCItem(cItem, out var cItemRules)
                && cItemRules != null
                && cItemRules.Get(ev.KnobSetting) is { } cItemRule)
            {
                Scp914Dispatcher.ApplyPickup(cItemRule, ev);
            }

            return;
        }

        if (Scp914Registry.TryGetVanilla(ev.Pickup.Type, out var vanillaRules)
            && vanillaRules != null
            && vanillaRules.Get(ev.KnobSetting) is { } vanillaRule)
        {
            Scp914Dispatcher.ApplyPickup(vanillaRule, ev);
        }
    }

    /// <summary>
    /// インベントリアップグレード版。O5 ワイルドカードも適用する。
    /// </summary>
    private static void OnUpgradingInventoryItem(UpgradingInventoryItemEventArgs ev)
    {
        if (ev?.Item == null || ev.Player == null) return;

        // ① O5 共通超低確率ロール
        if (Scp914Registry.O5WildcardRule is { } o5wild
            && Scp914Dispatcher.ApplyInventory(o5wild, ev))
        {
            return;
        }

        // ② CustomItem → CItem → Vanilla は元通り
        if (ev.Item.TryGetCustomItem(out var customItem) && customItem != null)
        {
            if (Scp914Registry.TryGetForCustomItem(customItem, out var customRules)
                && customRules != null
                && customRules.Get(ev.KnobSetting) is { } customRule)
            {
                Scp914Dispatcher.ApplyInventory(customRule, ev);
            }

            return;
        }

        if (CItem.TryGet(ev.Item, out var cItem) && cItem != null)
        {
            if (Scp914Registry.TryGetForCItem(cItem, out var cItemRules)
                && cItemRules != null
                && cItemRules.Get(ev.KnobSetting) is { } cItemRule)
            {
                Scp914Dispatcher.ApplyInventory(cItemRule, ev);
            }

            return;
        }

        if (Scp914Registry.TryGetVanilla(ev.Item.Type, out var vanillaRules)
            && vanillaRules != null
            && vanillaRules.Get(ev.KnobSetting) is { } vanillaRule)
        {
            Scp914Dispatcher.ApplyInventory(vanillaRule, ev);
        }
    }

    private static void FullyRemoveItem(Scp914Context ctx)
    {
        if (ctx.IsInventory)
            ctx.Owner!.RemoveItem(ctx.Item!, true);
        else
            ctx.Pickup?.Destroy();
    }

    // ==== プレイヤー本体 (VeryFine で稀にゾンビ化) ====

    private static void Human(UpgradingPlayerEventArgs ev)
    {
        if (ev.KnobSetting != Scp914KnobSetting.VeryFine) return;
        if (Random.Next(0, 4) != 0) return;

        ev.Player?.Role.Set(RoleTypeId.Scp0492, RoleSpawnFlags.None);
        Timing.CallDelayed(1f, () =>
        {
            ev.Player?.EnableEffect(EffectType.Scp207, 4);
            if (ev.Player != null)
            {
                ev.Player.UniqueRole = "Zombified";
                ev.Player.SetCustomInfo("<color=#C50000>Zombified Subject</color>");
                ev.Player.SetScale(new Vector3(
                    UnityEngine.Random.Range(0.01f, 1.08f),
                    UnityEngine.Random.Range(0.01f, 1.08f),
                    UnityEngine.Random.Range(0.01f, 1.08f)));
                if (!Handler.CanUsePlayers.Contains(ev.Player))
                    Handler.CanUsePlayers.Add(ev.Player);
                if (!Handler.ActivatedPlayers.Contains(ev.Player))
                    Handler.ActivatedPlayers.Add(ev.Player);
                ev.Player.ShowHint("<size=24>体が魔改造されていく・・・！</size>");
            }
        });
    }
}