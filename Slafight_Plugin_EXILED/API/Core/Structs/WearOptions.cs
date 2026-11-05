using System;
using System.Collections.Generic;
using Exiled.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// <see cref="Extensions.WearExtensions"/> で着せるときの指定です。
/// </summary>
public sealed class WearOptions
{
    /// <summary>
    /// 省略時のスロット名です。
    /// </summary>
    public const string DefaultSlot = "default";

    /// <summary>
    /// すべて既定値の指定です。
    /// </summary>
    public static readonly WearOptions Default = new WearOptions();

    /// <summary>
    /// 装着先の枠です。同じ枠に着せると前のものを差し替えます。
    /// </summary>
    public string Slot { get; init; } = DefaultSlot;

    /// <summary>
    /// 追従先です。null なら装着者の体です。
    /// </summary>
    public Transform Target { get; init; }

    /// <summary>
    /// 追従先のローカル座標系でのずれです。
    /// </summary>
    public Vector3 Offset { get; init; }

    /// <summary>
    /// 追従先に対する回転のずれです。
    /// </summary>
    public Quaternion? RotationOffset { get; init; }

    /// <summary>
    /// オブジェクトの大きさです。null なら元のままです。
    /// </summary>
    public Vector3? Scale { get; init; }

    /// <summary>
    /// 装着者本人に見せるかどうか。
    /// </summary>
    public bool ShowToWearer { get; init; } = true;

    /// <summary>
    /// 見せる相手の条件です。null なら全員です。
    /// </summary>
    public Predicate<Player> Visibility { get; init; }

    /// <summary>
    /// true なら装着者が透明化しても隠しません。
    /// </summary>
    public bool IgnoreInvisibility { get; init; }

    /// <summary>
    /// true ならこのモデルを体として扱います。着せている間、素体を生存者から隠して当たり判定を切り、
    /// 名札とバッジは本人以外に見える透明な Internal NPC が代わりに出します。
    /// </summary>
    /// <remarks>
    /// 素体の当たり判定を前提にするバニラの SCP 能力 (SCP-096 の突進など) は届かなくなります。
    /// </remarks>
    public bool ReplaceBody { get; init; }

    /// <summary>
    /// モデルに持たせる当たり判定です。Schematic のときだけ効き、受けた攻撃は装着者へ流します。
    /// </summary>
    public IReadOnlyList<WearHitboxOptions> Hitboxes { get; init; } = [];
}
