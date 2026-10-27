using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Core.Interfaces;

/// <summary>
/// 特定のプレイヤーに属するものです。<see cref="Features.EventHandlerBase"/> は持ち主の退出時に自動で破棄します。
/// </summary>
public interface IPlayerOwn
{
    Player Player { get; }
}
