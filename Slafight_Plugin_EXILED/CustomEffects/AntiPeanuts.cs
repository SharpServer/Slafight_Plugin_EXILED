using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;
using ExiledEventHandlers = Exiled.Events.Handlers;

namespace Slafight_Plugin_EXILED.CustomEffects;

public class AntiPeanuts : CustomEffectBase, ICustomDisplayName
{
    public bool CanBeDisplayed => true;
    public string DisplayName => "Anti Peanuts";
    public override EffectClassification Classification => EffectClassification.Positive;

    protected override void SubscribeEvents()
    {
        RegisterEvent(
            () => ExiledEventHandlers.Player.Hurting += OnHurting,
            () => ExiledEventHandlers.Player.Hurting -= OnHurting);
        base.SubscribeEvents();
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Player != Player || ev.Attacker?.Role?.Type != RoleTypeId.Scp173)
            return;

        ev.IsAllowed = false;
        ev.Player.Health = 1f;
        ev.Attacker.PlayShieldBreakSound();
        DisableEffect();
    }
}
