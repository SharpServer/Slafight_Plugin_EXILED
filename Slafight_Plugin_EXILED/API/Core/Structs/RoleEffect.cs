using System;
using CustomPlayerEffects;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// 役職スポーン時に付与する効果の宣言です。効果は型で指定します。
/// </summary>
public readonly struct RoleEffect
{
    private readonly Action<Player> apply;

    private RoleEffect(Action<Player> apply) => this.apply = apply;

    /// <summary>
    /// 効果を型で指定します。duration が 0 なら期限なしです。
    /// </summary>
    public static RoleEffect Of<T>(byte intensity = 1, float duration = 0f)
        where T : StatusEffectBase
    {
        return new RoleEffect(player => player.EnableEffect<T>(intensity, duration));
    }

    /// <summary>
    /// 対象プレイヤーに適用します。
    /// </summary>
    public void ApplyTo(Player player) => apply?.Invoke(player);
}
