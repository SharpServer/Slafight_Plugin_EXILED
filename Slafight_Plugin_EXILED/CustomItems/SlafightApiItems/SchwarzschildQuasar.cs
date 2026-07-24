using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using InventorySystem.Items.Jailbird;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Item = Exiled.Events.Handlers.Item;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SchwarzschildQuasar : CItem
{
    public override string DisplayName => "シュバルツシルト・クエィサァー";
    public override string Description => "???";
    protected override string UniqueKey => "SchwarzschildQuasar";
    protected override ItemType BaseItem => ItemType.Jailbird;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.blue;
    private Dictionary<ushort, SchwarzschildQuasarStatusBase> Bases = [];

    public override void RegisterEvents()
    {
        Player.Dying += OnDyingOthers;
        Player.ChangingItem += OnChangingOther;
        Item.JailbirdChangingWearState += OnJailbirdChangingPhase;
        Item.JailbirdChargeComplete += OnJailbirdCharged;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.Dying -= OnDyingOthers;
        Player.ChangingItem -= OnChangingOther;
        Item.JailbirdChangingWearState -= OnJailbirdChangingPhase;
        Item.JailbirdChargeComplete -= OnJailbirdCharged;
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        Bases.Clear();
        base.OnWaitingForPlayers();
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        Bases.TryAdd(ev.Item.Serial, new SchwarzschildQuasarStatusBase {Serial = ev.Item.Serial});
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnChangingItem(ChangingItemEventArgs ev)
    {
        ev.Player?.EnableEffect<Burned>(15);
        base.OnChangingItem(ev);
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.Player?.DisableEffect<Burned>();
        base.OnDropping(ev);
    }

    private void OnChangingOther(ChangingItemEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        ev.Player?.DisableEffect<Burned>();
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (!Bases.TryGetValue(ev.Attacker.CurrentItem.Serial, out var @base)) return;
        var result = @base.Phase switch
        {
            SchwarzschildQuasarPhaseType.WakeUp => 10f,
            SchwarzschildQuasarPhaseType.Normal => 20f,
            SchwarzschildQuasarPhaseType.HighPowered => 30f,
            SchwarzschildQuasarPhaseType.Ender => 777f,
            _ => 0f
        };

        if (@base.IsCharged)
            result *= 2f;
        
        ev.Amount = result;
        if (@base.IsCharged)
        {
            for (int i = 0; i < 7; i++)
            {
                ev.Player?.ExplodeEffect(ProjectileType.FragGrenade);
            }

            if (@base.Phase is SchwarzschildQuasarPhaseType.HighPowered)
            {
                for (int i = 0; i < 2; i++)
                {
                    ev.Player?.ExplodeEffect(ProjectileType.Flashbang);
                }
            }
            else if (@base.Phase is SchwarzschildQuasarPhaseType.Ender)
            {
                for (int i = 0; i < 7; i++)
                {
                    ev.Player?.ExplodeEffect(ProjectileType.Flashbang);
                    ev.Player?.SendWarheadExplosionEffect();
                }
            }
        }
        else
        {
            ev.Player?.ExplodeEffect(ProjectileType.FragGrenade);
        }

        @base.UsedCount++;
        @base.IsCharged = false;
        @base.Jailbird.WearState = @base.UsedCount switch
        {
            // 0-19回: Healthy (初期~中盤)
            <= 19 => JailbirdWearState.Healthy,
        
            // 20-24回: LowWear (低摩耗開始)
            <= 24 => JailbirdWearState.LowWear,
        
            // 25-27回: MediumWear (中摩耗)
            <= 27 => JailbirdWearState.MediumWear,
        
            // 28回: HighWear (高摩耗)
            28 => JailbirdWearState.HighWear,
        
            // 29回: AlmostBroken (最終警告)
            29 => JailbirdWearState.AlmostBroken,
        
            // 30回以上: Broken (破壊)
            _ => JailbirdWearState.Broken
        };
        base.OnHurtingOthers(ev);
    }

    private void OnJailbirdChangingPhase(JailbirdChangingWearStateEventArgs ev)
    {
        if (!Check(ev.Item) || !Bases.TryGetValue(ev.Item.Serial, out var @base)) return;
    
        // 30回使用後に壊れる: LowWear/MediumWear/HighWearすべて使用、29回AlmostBroken
        ev.NewWearState = @base.UsedCount switch
        {
            // 0-19回: Healthy (初期~中盤)
            <= 19 => JailbirdWearState.Healthy,
        
            // 20-24回: LowWear (低摩耗開始)
            <= 24 => JailbirdWearState.LowWear,
        
            // 25-27回: MediumWear (中摩耗)
            <= 27 => JailbirdWearState.MediumWear,
        
            // 28回: HighWear (高摩耗)
            28 => JailbirdWearState.HighWear,
        
            // 29回: AlmostBroken (最終警告)
            29 => JailbirdWearState.AlmostBroken,
        
            // 30回以上: Broken (破壊)
            _ => JailbirdWearState.Broken
        };
    }

    private void OnJailbirdCharged(JailbirdChargeCompleteEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        if (Bases.TryGetValue(ev.Item.Serial, out var @base))
        {
            @base.IsCharged = true;
        }
    }

    private void OnDyingOthers(DyingEventArgs ev)
    {
        if (!CheckHeld(ev.Attacker) || ev.DamageHandler.Type is not DamageType.ParticleDisruptor) return;
        ev.IsAllowed = false;
        ev.Player.Vaporize();
    }
}

public enum SchwarzschildQuasarPhaseType
{
    WakeUp,
    Normal,
    HighPowered,
    Ender
}
public class SchwarzschildQuasarStatusBase
{
    public Jailbird Jailbird => Exiled.API.Features.Items.Item.Get(Serial).As<Jailbird>();
    public ushort Serial { get; init; }
    public int UsedCount { get; set; }
    public SchwarzschildQuasarPhaseType Phase
    {
        get
        {
            switch (UsedCount)
            {
                // Ender: 29回のみ (最後の攻撃だけEnder)
                case 29:
                    return SchwarzschildQuasarPhaseType.Ender;
                // HighPowered: 16-28回 (Ender前)
                case <= 28:
                    return SchwarzschildQuasarPhaseType.HighPowered;
                // Normal: 6-15回
                default:
                {
                    if (UsedCount <= 15)
                    {
                        return SchwarzschildQuasarPhaseType.Normal;
                    }
                    // WakeUp: 0-5回 (初期)

                    return SchwarzschildQuasarPhaseType.WakeUp;
                }
            }
        }
    }
    public bool IsCharged { get; set; }
}