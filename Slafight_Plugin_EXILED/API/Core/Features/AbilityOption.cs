using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 1 つの能力が持つ、切り替え可能な選択肢です。選択肢自身が振る舞い (<see cref="Use"/>) を持ちます。
/// </summary>
public abstract class AbilityOption
{
    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 説明です。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// この選択肢だけの使用条件です。使えないなら理由を返します。
    /// </summary>
    public virtual bool CanUse(Player player, out string failureReason)
    {
        failureReason = null;

        return true;
    }

    /// <summary>
    /// この選択肢の効果です。
    /// </summary>
    public abstract void Use(Player player);

    /// <inheritdoc />
    public override string ToString() => Name;
}
