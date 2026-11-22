using Exiled.Events.EventArgs.Player;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomEffects;

public class DamageBoost : CustomEffectBase, ICustomDisplayName
{
    public bool CanBeDisplayed => true;
    public string DisplayName => "Damage Boost";
    public override EffectClassification Classification => EffectClassification.Positive;

    protected override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;

        base.RegisterEvents();
    }

    protected override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;

        base.UnregisterEvents();
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker is null || ev.Attacker != Player) return;
        ev.Amount += Intensity;
    }
}
