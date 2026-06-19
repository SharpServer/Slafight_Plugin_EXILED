using System;
using CustomPlayerEffects;
using Exiled.API.Features;
using Mirror;

namespace Slafight_Plugin_EXILED.API.Features;

public class CustomTickingEffectBase : TickingEffectBase
{
    public virtual float TickRate
    {
        get;
        set
        {
            field = value;
            ModifyTickRates(value);
        }
    } = 1f;
    public Player Player { get; private set; }
    
    public override void Enabled()
    {
        base.Enabled();
        TimeBetweenTicks = TickRate;
        _timeTillTick = TickRate;
        Player = Player.Get(Hub.PlayerId);
    }

    public override void OnTick() {}

    private void ModifyTickRates(float rate)
    {
        TimeBetweenTicks = rate;
        _timeTillTick = rate;
    }
}