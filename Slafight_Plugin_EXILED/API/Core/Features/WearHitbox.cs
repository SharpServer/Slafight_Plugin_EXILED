using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp1507;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.PlayableScps.Scp939;
using PlayerStatsSystem;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 着せた Schematic の CustomHitbox を装着者の体として働かせます。
/// </summary>
/// <remarks>
/// バニラは <see cref="NetworkId"/> からプレイヤーを引くので、自傷防止・爆発の重複防止・SCP 補正はそのまま効きます。
/// </remarks>
internal sealed class WearHitbox : IDestructible
{
    private static readonly int HitboxLayer = LayerMask.NameToLayer("Hitbox");

    private static (uint Attacker, uint Victim, int Frame) lastScpMelee;

    private readonly PlayerWear wear;
    private readonly WearHitboxOptions options;

    /// <summary>
    /// Unity 側で作ったグループなら、ブロックの選択はそちらに任せます。
    /// </summary>
    private readonly bool authored;

    private bool bound;

    internal WearHitbox(PlayerWear wear, WearHitboxOptions options)
    {
        this.wear = wear;
        this.options = options;

        Hitbox = SchematicCustomHitbox.Get(wear.Schematic, options.Group) ??
                 SchematicCustomHitbox.Create(wear.Schematic, options.Group, []);

        authored = Hitbox.Blocks.Count > 0;

        Hitbox.SetDamageMultiplier(options.DamageMultiplier);
        Hitbox.SetEnabled(true);
        Hitbox.RedirectTarget = this;
    }

    /// <inheritdoc />
    public uint NetworkId => wear.OwnerNetId;

    /// <inheritdoc />
    public Vector3 CenterOfMass => wear.GameObject != null ? wear.GameObject.transform.position : Vector3.zero;

    internal SchematicCustomHitbox Hitbox { get; }

    /// <inheritdoc />
    public bool Damage(float damage, DamageHandlerBase handler, Vector3 exactHitPos)
    {
        Player owner = wear.Owner;

        if (!wear.IsWorn || !owner.IsSafePlayer() || owner.GetNetId() != wear.OwnerNetId || !owner.IsAlive)
            return false;

        if (IsBlockedScpMelee(owner, handler))
            return false;

        if (handler is StandardDamageHandler standard)
        {
            standard.Hitbox = options.HitboxType;
            standard.Damage = damage;
        }

        return owner.ReferenceHub.playerStats.DealDamage(handler);
    }

    /// <summary>
    /// 未登録のブロックを拾い、当たり判定を有効な状態に保ちます。
    /// </summary>
    internal void Sync()
    {
        if (!bound)
            Bind();

        foreach (Collider collider in Hitbox.Colliders)
        {
            if (collider == null) continue;

            // Glass のままだと視線判定と弾痕がモデルに反応する。
            collider.gameObject.layer = HitboxLayer;

            // Primitive のフラグが変わるたびに Collidable へ戻されるので、見張り続ける。
            collider.enabled = true;
        }
    }

    /// <summary>
    /// サーバー側の Collider だけを切り替えます。
    /// </summary>
    internal void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider collider in Hitbox.Colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    /// <summary>
    /// 体への転送をやめます。オブジェクトが残る着せ直しでも当たらないようにします。
    /// </summary>
    internal void Dispose()
    {
        if (Hitbox == null) return;

        Hitbox.RedirectTarget = null;
        Hitbox.SetEnabled(false);
        SetCollidersEnabled(false);
    }

    private void Bind()
    {
        IEnumerable<SchematicBlock> blocks = authored
            ? Hitbox.Blocks
            : wear.Schematic.Blocks.Where(options.Matches);

        bound = true;

        foreach (SchematicBlock block in blocks.ToList())
        {
            if (block.TryGetComponent(out PrimitiveObjectToy primitive))
            {
                // Primitive の Collider は Start で作られるので、生成直後はまだ無い。
                if (!primitive.TryGetComponent(out Collider _))
                {
                    bound = false;
                    continue;
                }

                // クライアント側でぶつかると装着者が動けなくなる。判定はサーバーの Collider だけが持つ。
                primitive.NetworkPrimitiveFlags &= ~PrimitiveFlags.Collidable;
            }

            Hitbox.AddBlock(block);
        }
    }

    /// <summary>
    /// SCP の近接は HitboxIdentity 以外を味方でも、当たった Collider の数だけ殴るので、ここで体 1 つ分に揃えます。
    /// </summary>
    private bool IsBlockedScpMelee(Player owner, DamageHandlerBase handler)
    {
        if (handler is not (ScpDamageHandler or Scp939DamageHandler or Scp3114DamageHandler or Scp1507DamageHandler))
            return false;

        AttackerDamageHandler melee = (AttackerDamageHandler)handler;

        // 素体の当たり判定が残っていれば、そちらが受ける。
        if (!wear.ReplacesBody || !HitboxIdentity.IsEnemy(melee.Attacker.Role, owner.Role.Type))
            return true;

        (uint Attacker, uint Victim, int Frame) current = (melee.Attacker.NetId, wear.OwnerNetId, Time.frameCount);

        if (lastScpMelee == current) return true;

        lastScpMelee = current;

        return false;
    }
}
