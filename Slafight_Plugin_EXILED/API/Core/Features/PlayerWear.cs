using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Mirror;
using PlayerRoles;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// プレイヤーに着せたオブジェクト 1 つです。役職変更・退出・ラウンド再開で自動的に外れます。
/// </summary>
public sealed class PlayerWear
{
    private readonly Action destroy;
    private readonly IReadOnlyList<WearHitboxOptions> hitboxOptions;
    private readonly List<WearHitbox> hitboxes = [];

    private WearFollower follower;
    private Transform target;
    private Vector3 offset;
    private Quaternion? rotationOffset;
    private Predicate<Player> visibility;
    private bool showToWearer;
    private bool ignoreInvisibility;
    private bool replacesBody;
    private NetworkShowState showState;

    internal PlayerWear(Player owner, string slot, GameObject gameObject, Action destroy, WearOptions options)
    {
        Owner = owner;
        OwnerNetId = owner.GetNetId();
        Role = owner.Role.Type;
        Slot = slot;
        GameObject = gameObject;
        Schematic = gameObject.GetComponent<SchematicObject>();
        this.destroy = destroy;

        target = options.Target != null ? options.Target : owner.Transform;
        offset = options.Offset;
        rotationOffset = options.RotationOffset;
        visibility = options.Visibility;
        showToWearer = options.ShowToWearer;
        ignoreInvisibility = options.IgnoreInvisibility || options.ReplaceBody;
        replacesBody = options.ReplaceBody;
        hitboxOptions = options.Hitboxes ?? [];

        if (options.Scale is { } scale)
            gameObject.transform.localScale = scale;
    }

    /// <summary>
    /// 装着者です。
    /// </summary>
    public Player Owner { get; }

    /// <summary>
    /// 装着先の枠です。
    /// </summary>
    public string Slot { get; }

    /// <summary>
    /// 着せているオブジェクトです。
    /// </summary>
    public GameObject GameObject { get; }

    /// <summary>
    /// Schematic を着せているならその実体です。それ以外は null。
    /// </summary>
    public SchematicObject Schematic { get; }

    /// <summary>
    /// まだ着せたままかどうか。
    /// </summary>
    public bool IsWorn => !IsRemoved && GameObject != null;

    /// <summary>
    /// 見せる相手の条件です。変更すると全員へ反映します。
    /// </summary>
    public Predicate<Player> Visibility
    {
        get => visibility;
        set
        {
            visibility = value;
            RefreshVisibility();
        }
    }

    /// <summary>
    /// 装着者本人に見せるかどうか。変更すると全員へ反映します。
    /// </summary>
    public bool ShowToWearer
    {
        get => showToWearer;
        set
        {
            showToWearer = value;
            RefreshVisibility();
        }
    }

    /// <summary>
    /// true なら装着者が透明化しても隠しません。
    /// </summary>
    public bool IgnoreInvisibility
    {
        get => ignoreInvisibility;
        set
        {
            ignoreInvisibility = value;
            WearManager.SyncOwner(this);
        }
    }

    /// <summary>
    /// モデルに持たせた当たり判定です。Damaging などのイベントはここから購読できます。
    /// </summary>
    public IReadOnlyList<SchematicCustomHitbox> Hitboxes => hitboxes.Select(hitbox => hitbox.Hitbox).ToList();

    /// <summary>
    /// このモデルが素体の代わりを務めているかどうか。変更するとすぐ反映します。
    /// </summary>
    public bool ReplacesBody
    {
        get => replacesBody;
        set
        {
            replacesBody = value;

            if (value)
                ignoreInvisibility = true;

            WearManager.SyncBodyReplacement(this);
        }
    }

    /// <summary>
    /// 追従先のローカル座標系でのずれです。
    /// </summary>
    public Vector3 Offset => offset;

    /// <summary>
    /// 追従先に対する回転のずれです。
    /// </summary>
    public Quaternion RotationOffset => rotationOffset ?? Quaternion.identity;

    /// <summary>
    /// 追従先です。
    /// </summary>
    public Transform Target => target;

    /// <summary>
    /// オブジェクトの大きさです。
    /// </summary>
    public Vector3 Scale
    {
        get => GameObject != null ? GameObject.transform.localScale : Vector3.one;
        set
        {
            if (GameObject != null)
                GameObject.transform.localScale = value;
        }
    }

    internal uint OwnerNetId { get; }

    internal RoleTypeId Role { get; }

    /// <summary>
    /// 透明化の連動で隠している netId です。
    /// </summary>
    internal HashSet<uint> HiddenNetIds { get; } = [];

    private bool IsRemoved { get; set; }

    /// <summary>
    /// 追従先やずれを変更します。省略した項目は今の値のままです。
    /// </summary>
    public void Follow(Transform newTarget = null, Vector3? newOffset = null, Quaternion? newRotationOffset = null)
    {
        if (newTarget != null) target = newTarget;
        if (newOffset is { } value) offset = value;
        if (newRotationOffset is not null) rotationOffset = newRotationOffset;

        if (!IsWorn) return;

        if (follower == null)
            follower = GameObject.AddComponent<WearFollower>();

        follower.Initialize(target, offset, rotationOffset);
    }

    /// <summary>
    /// <see cref="Visibility"/> の判定元が変わったときに、全員へ判定し直します。
    /// </summary>
    public void RefreshVisibility()
    {
        if (!IsWorn) return;

        // 条件が一度も無ければ全員に見えるままでよく、送信も要らない。
        if (showState is null && visibility is null && showToWearer) return;

        showState ??= new NetworkShowState { VisibilityPredicate = CanSee };

        foreach (NetworkIdentity identity in ResolveIdentities())
        {
            identity.UpdateShowState(showState);
        }
    }

    /// <summary>
    /// 外して破棄します。
    /// </summary>
    public void Remove() => WearManager.Remove(this);

    /// <summary>
    /// 当たり判定を足します。Schematic 以外には付けられず null を返します。
    /// </summary>
    public SchematicCustomHitbox AddHitbox(WearHitboxOptions options)
    {
        if (!IsWorn || options is null) return null;

        if (Schematic == null)
        {
            Log.Warn($"[Slafight] Schematic 以外には当たり判定を持たせられません: {Slot}");

            return null;
        }

        WearHitbox hitbox = new WearHitbox(this, options);

        hitboxes.Add(hitbox);
        WearManager.QueueHitboxSync(this);

        return hitbox.Hitbox;
    }

    /// <summary>
    /// 当たり判定をすべて外します。
    /// </summary>
    public void ClearHitboxes()
    {
        foreach (WearHitbox hitbox in hitboxes)
        {
            hitbox.Dispose();
        }

        hitboxes.Clear();
    }

    /// <summary>
    /// 指定された当たり判定を作ります。
    /// </summary>
    internal void BindHitboxes()
    {
        foreach (WearHitboxOptions options in hitboxOptions)
        {
            AddHitbox(options);
        }
    }

    internal void SyncHitboxes()
    {
        foreach (WearHitbox hitbox in hitboxes)
        {
            hitbox.Sync();
        }
    }

    internal void SetHitboxCollidersEnabled(bool enabled)
    {
        foreach (WearHitbox hitbox in hitboxes)
        {
            hitbox.SetCollidersEnabled(enabled);
        }
    }

    /// <summary>
    /// Schematic は子が増減するので毎回引き直します。
    /// </summary>
    internal List<NetworkIdentity> ResolveIdentities()
    {
        List<NetworkIdentity> identities = [];

        if (GameObject == null) return identities;

        foreach (NetworkIdentity identity in GameObject.GetComponentsInChildren<NetworkIdentity>(true))
        {
            if (identity != null && identity.netId != 0)
                identities.Add(identity);
        }

        return identities;
    }

    /// <summary>
    /// 記録だけ消して、オブジェクトは残します。同じ物を着せ直すときに使います。
    /// </summary>
    internal void Release()
    {
        IsRemoved = true;

        ClearHitboxes();

        if (follower != null)
            UnityEngine.Object.Destroy(follower);

        if (showState is not null)
            ResolveIdentities().RemoveShowState();
    }

    /// <summary>
    /// 記録を消してオブジェクトも破棄します。
    /// </summary>
    internal void Destroy()
    {
        Release();
        destroy();
    }

    private bool CanSee(Player viewer) =>
        (showToWearer || viewer.Id != Owner.Id) && (visibility?.Invoke(viewer) ?? true);
}
