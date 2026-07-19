using CustomPlayerEffects;
using Mirror;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// Provides Sinkhole visuals while suppressing its movement and stamina effects.
/// </summary>
public class VisualSinkhole : CustomEffectBase, ICustomDisplayName
{
    private bool _blockedByExistingSinkhole;
    private bool _ownsSinkholeState;

    public bool CanBeDisplayed => false;
    public string DisplayName => "Visual Sinkhole";
    public override EffectClassification Classification => EffectClassification.Technical;

    /// <summary>
    /// Whether the vanilla Sinkhole footstep override should run while this effect owns the
    /// Sinkhole state. This can be changed through PlayerEffectsController.TryGetEffect.
    /// </summary>
    public bool FootstepOverridesEnabled { get; set; } = true;

    public override void Enabled()
    {
        base.Enabled();
        _blockedByExistingSinkhole = IsRealSinkholeEnabled();
        ApplySinkholeVisual();
    }

    public override void Disabled()
    {
        DisableSinkholeVisual();
        base.Disabled();
    }

    public override void OnEffectUpdate()
    {
        base.OnEffectUpdate();
        ApplySinkholeVisual();
    }

    public override void IntensityChanged(byte prevState, byte newState)
    {
        base.IntensityChanged(prevState, newState);

        if (newState > 0)
            ApplySinkholeVisual();
    }

    public override void OnDestroy()
    {
        DisableSinkholeVisual();
        base.OnDestroy();
    }

    public static bool TryGetOwner(ReferenceHub hub, out VisualSinkhole effect)
    {
        effect = null;
        return hub?.playerEffectsController != null &&
               hub.playerEffectsController.TryGetEffect(out effect) &&
               effect.IsEnabled &&
               effect._ownsSinkholeState;
    }

    private bool IsRealSinkholeEnabled()
    {
        return Hub?.playerEffectsController?.GetEffect<Sinkhole>()?.IsEnabled == true;
    }

    private void ApplySinkholeVisual()
    {
        if (!NetworkServer.active || Intensity == 0 || Hub?.playerEffectsController == null)
            return;

        Sinkhole sinkhole = Hub.playerEffectsController.GetEffect<Sinkhole>();
        if (sinkhole == null || _blockedByExistingSinkhole)
            return;

        if (!_ownsSinkholeState)
        {
            if (sinkhole.IsEnabled)
            {
                _blockedByExistingSinkhole = true;
                return;
            }

            _ownsSinkholeState = true;
        }

        if (!sinkhole.IsEnabled || sinkhole.Intensity != Intensity)
            sinkhole.ServerSetState(Intensity);
    }

    private void DisableSinkholeVisual()
    {
        if (NetworkServer.active && Hub?.playerEffectsController != null)
        {
            Sinkhole sinkhole = Hub.playerEffectsController.GetEffect<Sinkhole>();
            if (_ownsSinkholeState && sinkhole?.IsEnabled == true && sinkhole.Duration == 0f)
                sinkhole.ServerSetState(0);
        }

        _ownsSinkholeState = false;
        _blockedByExistingSinkhole = false;
    }
}
