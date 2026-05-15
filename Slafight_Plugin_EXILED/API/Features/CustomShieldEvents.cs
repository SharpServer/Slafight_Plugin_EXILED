using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.API.Features;

public class CustomShieldChangingEventArgs(Player player, CustomShieldState state, float oldValue, float newValue, CustomShieldChangeReason reason)
{
    public Player Player { get; } = player;
    public CustomShieldState State { get; } = state;
    public float OldValue { get; } = oldValue;
    public float NewValue { get; set; } = newValue;
    public CustomShieldChangeReason Reason { get; } = reason;
    public bool IsAllowed { get; set; } = true;
}

public class CustomShieldChangedEventArgs(Player player, CustomShieldState state, float oldValue, float newValue, CustomShieldChangeReason reason)
{
    public Player Player { get; } = player;
    public CustomShieldState State { get; } = state;
    public float OldValue { get; } = oldValue;
    public float NewValue { get; } = newValue;
    public CustomShieldChangeReason Reason { get; } = reason;
    public float Delta => NewValue - OldValue;
}

public class CustomShieldAbsorbingDamageEventArgs(
    HurtingEventArgs hurtingEvent,
    CustomShieldState state,
    float originalAmount,
    float shieldDamage,
    float healthDamage)
{
    public HurtingEventArgs HurtingEvent { get; } = hurtingEvent;
    public Player Player => HurtingEvent.Player;
    public Player Attacker => HurtingEvent.Attacker;
    public CustomShieldState State { get; } = state;
    public float OriginalAmount { get; } = originalAmount;
    public float ShieldDamage { get; set; } = shieldDamage;
    public float HealthDamage { get; set; } = healthDamage;
    public bool IsAllowed { get; set; } = true;
}

public enum CustomShieldChangeReason
{
    Set,
    Damage,
    Heal,
    Smooth,
    Decay,
    Reset,
    AbsorbDamage
}
