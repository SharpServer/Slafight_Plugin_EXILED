using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// カスタムステータスエフェクトをプレイヤーのプレハブへ差し込みます。
/// </summary>
public sealed class CustomEffectsBootstrap : EventHandlerBase
{
    /// <inheritdoc />
    protected override void OnEnabled() => CustomStatusEffectsRegistry.AllRegister();

    /// <inheritdoc />
    protected override void OnDisposed() => CustomStatusEffectsRegistry.Unhook();
}
