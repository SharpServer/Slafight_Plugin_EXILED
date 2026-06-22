using System;
using Mirror;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomEffects;

public class NaturalHeal : CustomTickingEffectBase, ICustomDisplayName
{
    public bool CanBeDisplayed => true;
    public string DisplayName => "Natural Heal";
    public override EffectClassification Classification => EffectClassification.Positive;
    public override float TickRate => 0.01f;

    public override void OnTick()
    {
        if (!NetworkServer.active)
            return;
        if (Player == null || Player.IsDead) return;
        
        Player.Health = Math.Min(Player.MaxHealth, Player.Health + _intensity);
    }
}