using Exiled.API.Enums;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Scp914;
using MEC;
using PlayerRoles;
using Scp914;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.Scp914;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.Changes;

/// <summary>
/// SCP-914 のアップグレード挙動を**一元管理**するファイル。
/// vanilla ItemType / Exiled CustomItem / CItem 派生すべての変換ルールを
/// <see cref="RegisterRules"/> にまとめて宣言し、<see cref="Scp914Registry"/> へ流す。
/// 実行時は本ファイルの <see cref="OnUpgradingPickup"/> /
/// <see cref="OnUpgradingInventoryItem"/> が Registry を引いて
/// <see cref="Scp914Dispatcher"/> に委譲する。
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

    // =====================================================================
    // 全ルール定義 — Vanilla / CustomItem / CItem を横断してここだけ見れば
    // 各アイテムがどう変換されるか分かる。
    // =====================================================================

    private static void RegisterRules()
    {
        RegisterWildcard();
        RegisterVanillaRules();
        RegisterCustomItemRules();
        RegisterCItemRules();
    }

    /// <summary>全アイテム共通の 1/6 ロール。Scp513 または CapybaraMissile に置き換え。</summary>
    private static void RegisterWildcard()
    {
        // 1/12 で当選後、さらに 1/10 で Scp513、残り 9/10 で CapybaraMissile
        Scp914Registry.WildcardRule = Scp914Rule.Weighted(
            (1f / 10f, Scp914Rule.ToCItem<Scp513Item>()),
            (9f / 10f, Scp914Rule.ToCItem<CapybaraMissile>())
        ).WithChance(1f / 12f);
    }

    /// <summary>vanilla ItemType → Custom/CItem の変換ルール。</summary>
    private static void RegisterVanillaRules()
    {
        Scp914Registry.RegisterVanilla(ItemType.Adrenaline, new()
        {
            VeryFine = Scp914Rule.ToCItem<SerumD>(),
        });

        Scp914Registry.RegisterVanilla(ItemType.SCP500, new()
        {
            Fine = Scp914Rule.ToCItem<ClassXMemoryForcePil>(),
            VeryFine = Scp914Rule.ToCItem<ClassZMemoryForcePil>().WithChance(1f / 4f)
        });

        Scp914Registry.RegisterVanilla(ItemType.KeycardJanitor, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 6f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardScientist, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 5f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardResearchCoordinator, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardZoneManager, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardGuard, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardContainmentEngineer, new()
        {
            OneToOne = Scp914Rule.ToCItem<Toolbox>(),
            Rough    = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
            Coarse   = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
            Fine     = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
            VeryFine = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFPrivate, new()
        {
            Coarse   = Scp914Rule.Destroy,
            Rough    = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            OneToOne = Scp914Rule.ToCItem<KeycardSecurityChief>(),
            Fine     = Scp914Rule.Weighted((1f / 2f, Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain)), (1f / 2f, Scp914Rule.ToCItem<MasterCard>())),
            VeryFine = Scp914Rule.ToCItem<MasterCard>()
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFOperative, new()
        {
            Coarse   = Scp914Rule.ToCItem<KeycardConscripts>().WithChance(1f / 6f),
            Rough    = Scp914Rule.ToVanilla(ItemType.KeycardMTFPrivate),
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
            VeryFine = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 2f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardMTFCaptain, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardFacilityManager, new()
        {
            All = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 4f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardChaosInsurgency, new()
        {
            Coarse   = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 6f),
            Rough    = Scp914Rule.ToCItem<KeycardConscripts>(),
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 3f),
            VeryFine = Scp914Rule.ToCItem<MasterCard>().WithChance(1f / 2f),
        });
        Scp914Registry.RegisterVanilla(ItemType.KeycardO5, new()
        {
            VeryFine = Scp914Rule.ToCItem<OmegaWarheadAccess>().WithChance(1f / 2f),
        });

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
            Fine     = Scp914Rule.ToCItem<ThrowableScp244>(),
            VeryFine = Scp914Rule.ToCItem<ThrowableScp244>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP244b, new()
        {
            Fine     = Scp914Rule.ToCItem<ThrowableScp244>(),
            VeryFine = Scp914Rule.ToCItem<ThrowableScp244>(),
        });
        Scp914Registry.RegisterVanilla(ItemType.SCP1344, new()
        {
            Coarse = Scp914Rule.ToCItem<NvgBlue>(),
        });
    }

    /// <summary>Exiled CustomItem 固有の変換ルール (将来の CustomItem 専用ルール用に確保)。</summary>
    private static void RegisterCustomItemRules() { }

    /// <summary>CItem → Vanilla/CItem の変換ルール。</summary>
    private static void RegisterCItemRules()
    {
        Scp914Registry.RegisterCItem<KeycardFifthist>(new()
        {
            All = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<Scp1425>(),
            Fine     = Scp914Rule.ToCItem<KeycardFifthistPriest>(),
            VeryFine = Scp914Rule.ToCItem<MagicMissile>().WithChance(1f / 3f),
        });

        Scp914Registry.RegisterCItem<KeycardFifthistPriest>(new()
        {
            All = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<KeycardFifthist>(),
            Fine     = Scp914Rule.ToCItem<MagicMissile>(),
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

        Scp914Registry.RegisterCItem<NvgNormal>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<NvgNormal>(),
            Fine     = Scp914Rule.ToCItem<NvgRed>(),
            VeryFine = Scp914Rule.ToCItem<NvgBlue>(),
        });

        Scp914Registry.RegisterCItem<HIDTurret>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<HIDTurret>(),
            Fine     = Scp914Rule.ToVanilla(ItemType.MicroHID),
            VeryFine = Scp914Rule.ToCItem<GunGoCTurret>(),
        });

        Scp914Registry.RegisterCItem<GunGoCRailgun>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToVanilla(ItemType.ParticleDisruptor),
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.Keep,
            VeryFine = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
        });

        Scp914Registry.RegisterCItem<SerumD>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToVanilla(ItemType.Adrenaline),
            OneToOne = Scp914Rule.Passthrough,
            Fine     = Scp914Rule.ToCItem<SerumC>(),
            VeryFine = Scp914Rule.ToCItem<SerumC>(),
        });

        Scp914Registry.RegisterCItem<SerumC>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<SerumD>(),
            OneToOne = Scp914Rule.ToCItem<SerumC>(),
            Fine     = Scp914Rule.ToCItem<SerumC>(),
            VeryFine = Scp914Rule.ToCItem<SerumC>(),
        });

        Scp914Registry.RegisterCItem<ClassXMemoryForcePil>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToVanilla(ItemType.SCP500),
            OneToOne = Scp914Rule.Passthrough,
            Fine     = Scp914Rule.ToCItem<ClassZMemoryForcePil>(),
            VeryFine = Scp914Rule.ToCItem<ClassZMemoryForcePil>(),
        });

        Scp914Registry.RegisterCItem<SNAV300>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine     = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        Scp914Registry.RegisterCItem<SNAV310>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine     = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        Scp914Registry.RegisterCItem<SNAVUltimate>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.ToCItem<SNAV300>(),
            Fine     = Scp914Rule.ToCItem<SNAV310>(),
            VeryFine = Scp914Rule.ToCItem<SNAVUltimate>(),
        });

        Scp914Registry.RegisterCItem<MasterCard>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<Quarter>().Times(8),
            OneToOne = Scp914Rule.ToCItem<PlayingCard>(),
            Fine     = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
        });

        Scp914Registry.RegisterCItem<PlayingCard>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<Quarter>().Times(8),
            OneToOne = Scp914Rule.ToCItem<MasterCard>(),
            Fine     = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardResearchCoordinator),
        });

        Scp914Registry.RegisterCItem<Quarter>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.Destroy,
            Fine     = Scp914Rule.ToVanilla(ItemType.Coin),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardScientist),
        });

        Scp914Registry.RegisterCItem<KeycardConscripts>(new()
        {
            Rough    = Scp914Rule.Destroy, 
            Coarse   = Scp914Rule.ToCItem<KeycardSecurityChief>(), 
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.ToVanilla(ItemType.KeycardChaosInsurgency),
            // 1/5 で KeycardO5、残り 4/5 は Destroy（入力消すだけ）
            VeryFine = Scp914Rule.Weighted(
                (1f / 5f, Scp914Rule.ToVanilla(ItemType.KeycardO5)),
                (4f / 5f, Scp914Rule.Destroy)
            ),
        });

        Scp914Registry.RegisterCItem<KeycardSecurityChief>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToVanilla(ItemType.KeycardGuard),
            OneToOne = Scp914Rule.ToCItem<KeycardSecurityChief>(),
            Fine     = Scp914Rule.ToVanilla(ItemType.KeycardMTFOperative),
            VeryFine = Scp914Rule.ToVanilla(ItemType.KeycardMTFCaptain),
        });

        Scp914Registry.RegisterCItem<CloakGenerator>(new()
        {
            All = Scp914Rule.Destroy,
            Rough  = Scp914Rule.ToVanilla(ItemType.SCP268),
            Coarse = Scp914Rule.ToVanilla(ItemType.SCP268),
            OneToOne = Scp914Rule.Keep
        });

        Scp914Registry.RegisterCItem<CaneOfTheStars>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToCItem<KeycardFifthistPriest>(),
            OneToOne = Scp914Rule.ToVanilla(ItemType.SCP1509),
            Fine     = Scp914Rule.Keep,
            // 1/77 で SchwarzschildQuasar、残りは Destroy
            VeryFine = Scp914Rule.Weighted(
                (1f / 77f, Scp914Rule.ToCItem<SchwarzschildQuasar>()),
                (76f / 77f, Scp914Rule.Destroy)
            ),
        });

        Scp914Registry.RegisterCItem<ThrowableScp244>(new()
        {
            Rough    = Scp914Rule.Destroy,
            // 50% SCP244a、50% SCP244b
            Coarse   = Scp914Rule.Weighted(
                (0.5f, Scp914Rule.ToVanilla(ItemType.SCP244a)),
                (0.5f, Scp914Rule.ToVanilla(ItemType.SCP244b))
            ),
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.Destroy,
            VeryFine = Scp914Rule.Destroy,
        });

        Scp914Registry.RegisterCItem<SchwarzschildQuasar>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.ToVanilla(ItemType.Jailbird),
            OneToOne = Scp914Rule.ToCItem<CaneOfTheStars>(),
            Fine     = Scp914Rule.Keep,
            VeryFine = Scp914Rule.ToCItem<SchwarzschildRailbreaker>()
        });
        
        Scp914Registry.RegisterCItem<SchwarzschildRailbreaker>(new()
        {
            Rough    = Scp914Rule.ToCItem<SchwarzschildQuasar>(),
            Coarse   = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.Keep,
            VeryFine = Scp914Rule.Custom(ctx =>
            {
                FullyRemoveItem(ctx);
                for (int i = 0; i < 5; i++)
                {
                    if (Pickup.CreateAndSpawn(ItemType.GrenadeHE, ctx.OutputPosition) is GrenadePickup grenade)
                    {
                        grenade.Explode();
                    }
                }
            }),
        });

        Scp914Registry.RegisterCItem<GunScp7381>(new()
        {
            Rough    = Scp914Rule.Destroy,
            Coarse   = Scp914Rule.Destroy,
            OneToOne = Scp914Rule.Keep,
            Fine     = Scp914Rule.ToCItem<GunGoCRailgunFull>(),
            VeryFine = Scp914Rule.Custom(ctx =>
            {
                FullyRemoveItem(ctx);
                for (int i = 0; i < 5; i++)
                {
                    if (Pickup.CreateAndSpawn(ItemType.GrenadeHE, ctx.OutputPosition) is GrenadePickup grenade)
                    {
                        grenade.Explode();
                    }
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

        if (Scp914Registry.WildcardRule is { } wildcard
            && Scp914Dispatcher.ApplyPickup(wildcard, ev))
        {
            return;
        }

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
    /// インベントリアップグレード版。Wildcard は床専用につき無し。
    /// 出力は <see cref="Scp914Dispatcher.ApplyInventory"/> が AddOrDrop で捌く。
    /// </summary>
    private static void OnUpgradingInventoryItem(UpgradingInventoryItemEventArgs ev)
    {
        if (ev?.Item == null || ev.Player == null) return;

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
