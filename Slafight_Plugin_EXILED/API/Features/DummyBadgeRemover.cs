using NetworkManagerUtils.Dummies;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// すべての Dummy / NPC から「Dummy」バッジを消します。
/// </summary>
/// <remarks>バッジの色が <c>none</c> の Group はバッジを設定しません。<c>IsDummy</c> の判定は変わりません。</remarks>
public sealed class DummyBadgeRemover : EventHandlerBase
{
    private const string NoBadgeColor = "none";

    private string originalColor;

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        originalColor ??= DummyUtils.DummyGroup.BadgeColor;
        DummyUtils.DummyGroup.BadgeColor = NoBadgeColor;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        if (originalColor is not null)
            DummyUtils.DummyGroup.BadgeColor = originalColor;
    }
}
