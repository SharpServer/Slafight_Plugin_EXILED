using CustomPlayerEffects;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public class CustomEffectBase : StatusEffectBase
{
    public Player Player { get; private set; }
    
    public override void Enabled()
    {
        base.Enabled();
        Player = Player.Get(Hub.PlayerId);
    }
}