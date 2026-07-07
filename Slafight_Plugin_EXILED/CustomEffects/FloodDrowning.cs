using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Mirror;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomEffects;

public class FloodDrowning : CustomTickingEffectBase, ICustomDisplayName
{
    public const byte DefaultIntensity = 255;
    public const float DefaultDuration = 6f;

    private const float DamagePerTick = 28f;
    private const float FinishingDamage = 5000f;
    private const float VisualRefreshDuration = 1.1f;
    private const string DeathText = "溺死した";

    public bool CanBeDisplayed => true;
    public string DisplayName => "Flood Drowning";
    public override EffectClassification Classification => EffectClassification.Negative;
    public override float TickRate => 0.35f;

    public override void Enabled()
    {
        base.Enabled();
        ApplyDrowningVisuals();
    }

    public override void OnEffectUpdate()
    {
        base.OnEffectUpdate();
        ApplyDrowningVisuals();
    }

    public override void IntensityChanged(byte prevState, byte newState)
    {
        base.IntensityChanged(prevState, newState);

        if (newState > 0)
            ApplyDrowningVisuals();
    }

    public override void OnTick()
    {
        if (!NetworkServer.active || Player == null || Player.IsDead || Intensity == 0)
            return;

        ApplyDrowningVisuals();

        float damage = Player.Health <= DamagePerTick
            ? FinishingDamage
            : DamagePerTick;

        Player.Hurt(damage, DeathText);
    }

    private void ApplyDrowningVisuals()
    {
        if (!NetworkServer.active || Player == null || Player.IsDead || Intensity == 0)
            return;

        Player.EnableEffect(EffectType.SinkHole, 255, VisualRefreshDuration);
        Player.EnableEffect<Slowness>(90, VisualRefreshDuration);
        Player.EnableEffect<Blindness>(80, VisualRefreshDuration);
        Player.EnableEffect<Blurred>(255, VisualRefreshDuration);
        Player.EnableEffect<Concussed>(255, VisualRefreshDuration);
        Player.EnableEffect<Deafened>(255, VisualRefreshDuration);
        Player.EnableEffect<Hemorrhage>(120, VisualRefreshDuration);
        Player.EnableEffect<VisualTraumatized>(180, VisualRefreshDuration);
    }
}
