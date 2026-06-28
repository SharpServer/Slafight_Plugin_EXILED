using CustomPlayerEffects;
using Mirror;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// Marker effect for uses that need Traumatized visuals without SCP-106's Traumatized kill branch.
/// </summary>
public class VisualTraumatized : CustomEffectBase, ICustomDisplayName
{
    private bool _blockedByExistingTraumatized;
    private bool _ownsTraumatizedState;

    public bool CanBeDisplayed => false;
    public string DisplayName => "Visual Traumatized";
    public override EffectClassification Classification => EffectClassification.Technical;

    public override void Enabled()
    {
        base.Enabled();
        _blockedByExistingTraumatized = IsRealTraumatizedEnabled();
        ApplyTraumatizedVisual();
    }

    public override void Disabled()
    {
        DisableTraumatizedVisual();
        base.Disabled();
    }

    public override void OnEffectUpdate()
    {
        base.OnEffectUpdate();
        ApplyTraumatizedVisual();
    }

    public override void IntensityChanged(byte prevState, byte newState)
    {
        base.IntensityChanged(prevState, newState);

        if (newState > 0)
            ApplyTraumatizedVisual();
    }

    public override void OnDestroy()
    {
        DisableTraumatizedVisual();
        base.OnDestroy();
    }

    public static bool ShouldSuppressScp106Kill(ReferenceHub hub)
    {
        return hub?.playerEffectsController != null &&
               hub.playerEffectsController.TryGetEffect(out VisualTraumatized effect) &&
               effect.IsEnabled &&
               effect._ownsTraumatizedState;
    }

    private bool IsRealTraumatizedEnabled()
    {
        return Hub?.playerEffectsController?.GetEffect<Traumatized>()?.IsEnabled == true;
    }

    private void ApplyTraumatizedVisual()
    {
        if (!NetworkServer.active || Intensity == 0 || Hub?.playerEffectsController == null)
            return;

        Traumatized traumatized = Hub.playerEffectsController.GetEffect<Traumatized>();
        if (traumatized == null)
            return;

        if (_blockedByExistingTraumatized)
            return;

        if (!_ownsTraumatizedState)
        {
            if (traumatized.IsEnabled)
            {
                _blockedByExistingTraumatized = true;
                return;
            }

            _ownsTraumatizedState = true;
        }

        if (traumatized.IsEnabled && traumatized.Intensity == Intensity)
            return;

        traumatized.ServerSetState(Intensity);
    }

    private void DisableTraumatizedVisual()
    {
        if (!NetworkServer.active || Hub?.playerEffectsController == null)
            return;

        Traumatized traumatized = Hub.playerEffectsController.GetEffect<Traumatized>();
        if (_ownsTraumatizedState && traumatized?.IsEnabled == true && traumatized.Duration == 0f)
            traumatized.ServerSetState(0);

        _ownsTraumatizedState = false;
        _blockedByExistingTraumatized = false;
    }
}
