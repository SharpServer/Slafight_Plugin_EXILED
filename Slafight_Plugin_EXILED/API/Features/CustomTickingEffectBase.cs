using CustomPlayerEffects;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class CustomTickingEffectBase : TickingEffectBase
{
    private const float MinimumTickRate = 0.01f;

    public virtual float TickRate
    {
        get;
        set
        {
            field = NormalizeTickRate(value);
            ModifyTickRates(field);
        }
    } = 1f;

    public Player Player { get; private set; }
    
    public override void Enabled()
    {
        base.Enabled();
        TimeBetweenTicks = TickRate;
        _timeTillTick = TickRate;
        Player = Player.Get(Hub);
    }

    public override void Disabled()
    {
        base.Disabled();
        Player = null;
    }

    public virtual void OnDestroy()
    {
        Player = null;
    }

    public override void OnTick() {}

    private void ModifyTickRates(float rate)
    {
        TimeBetweenTicks = rate;
        _timeTillTick = rate;
    }

    private static float NormalizeTickRate(float rate)
    {
        if (float.IsNaN(rate) || float.IsInfinity(rate) || rate < MinimumTickRate)
            return MinimumTickRate;

        return rate;
    }
}
