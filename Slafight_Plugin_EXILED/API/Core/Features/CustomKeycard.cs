using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.API.Features.Items.Keycards;
using Exiled.API.Interfaces.Keycards;
using System;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Keycards;
using InventorySystem.Items.Pickups;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using UnityEngine;

using ExiledItem = Exiled.API.Features.Items.Item;
using LabItem = LabApi.Features.Wrappers.Item;
using LabPickup = LabApi.Features.Wrappers.Pickup;
using LabPlayer = LabApi.Features.Wrappers.Player;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomItem"/> のキーカード向け基底です。<see cref="KeycardType"/> の外見と権限を適用します。
/// </summary>
/// <remarks><see cref="BaseType"/> をバニラのカードにした場合は権限だけを適用します。</remarks>
public abstract class CustomKeycard : CustomItem
{
    private static NametagDetail vanillaNametag;

    // 外見はシリアル単位でサーバーに残るので、拾い直しでは焼き直さない。
    private ushort designedSerial;

    public override ItemType BaseType => KeycardType switch
    {
        CustomKeycardType.Management => ItemType.KeycardCustomManagement,
        CustomKeycardType.MetalCase => ItemType.KeycardCustomMetalCase,
        CustomKeycardType.TaskForce => ItemType.KeycardCustomTaskForce,
        _ => ItemType.KeycardCustomSite02,
    };

    /// <summary>カードの外見の種類。</summary>
    protected virtual CustomKeycardType KeycardType => CustomKeycardType.Site02;

    /// <summary>このカードが持つアクセス権限。</summary>
    protected abstract KeycardPermissions Permissions { get; }

    /// <summary>インベントリ上の名称。</summary>
    protected virtual string ItemName => Name;

    /// <summary>カード本体の色。</summary>
    protected virtual Color32? TintColor => null;

    /// <summary>権限表示の色。</summary>
    protected virtual Color32? PermissionsColor => null;

    /// <summary>ラベル。Site02 / Management / MetalCase のみ。</summary>
    protected virtual string Label => null;

    /// <summary>ラベルの文字色。Site02 / Management / MetalCase のみ。</summary>
    protected virtual Color32? LabelColor => null;

    /// <summary>
    /// 所持者名。Site02 / MetalCase / TaskForce のみ。
    /// 未指定なら所持者の表示名、所持者がいなければ <see cref="HolderNamePool"/> から抽選して接頭辞を付けます。
    /// </summary>
    protected virtual string HolderName => null;

    /// <summary>所持者がいない時の所持者名の候補。null ならバニラの偽名、空なら設定しない。</summary>
    protected virtual string[] HolderNamePool => null;

    /// <summary>摩耗度。Site02 / MetalCase のみ。</summary>
    protected virtual byte? Wear => null;

    /// <summary>シリアル番号。MetalCase / TaskForce のみ。</summary>
    protected virtual string SerialNumber => null;

    /// <summary>ランク (0-3)。TaskForce のみ。</summary>
    protected virtual byte? Rank => null;

    /// <summary>
    /// 自動で決めた所持者名の前に付ける接頭辞。null なら付けない。
    /// 既定はバニラの役職別の接頭辞で、所持者がいない時は <see cref="RoleTypeId.None"/> です。
    /// </summary>
    protected virtual string GetHolderPrefix(RoleTypeId role)
    {
        if (VanillaNametag is not { } nametag)
            return null;

        KeycardWordsCollection prefixes = nametag._fallbackPrefix;
        foreach (NametagDetail.RolePrefixOverride roleOverride in nametag._roleBasedPrefixes ?? [])
        {
            if (Array.IndexOf(roleOverride.Roles, role) < 0)
                continue;

            prefixes = roleOverride.Prefix;
            break;
        }

        return prefixes != null && prefixes.Words is { Length: > 0 } words
            ? words[Random.Range(0, words.Length)]
            : null;
    }

    // バニラのカスタムカードは所持者名を書かないので、通常カードの名簿を借りる。
    private static NametagDetail VanillaNametag
    {
        get
        {
            if (vanillaNametag != null)
                return vanillaNametag;

            foreach (ItemBase template in InventoryItemLoader.AvailableItems.Values)
            {
                if (template is not KeycardItem keycard)
                    continue;

                foreach (DetailBase detail in keycard.Details)
                {
                    if (detail is NametagDetail nametag && nametag._fakeNamesPeople != null)
                        return vanillaNametag = nametag;
                }
            }

            return null;
        }
    }

    protected override void Customize(LabItem item)
    {
        if (ExiledItem.Get(item.Base) is Keycard keycard)
            Apply(keycard, item.CurrentOwner);

        base.Customize(item);
    }

    protected override LabPickup CreatePickup(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (BaseType is not (ItemType.KeycardCustomSite02 or ItemType.KeycardCustomManagement
                or ItemType.KeycardCustomMetalCase or ItemType.KeycardCustomTaskForce)
            || ExiledItem.Create(BaseType) is not CustomKeycardItem card)
            return base.CreatePickup(position, rotation, scale);

        // Pickup は外見を持たないので、外見を焼いた実アイテムと同じシリアルで作る。

        Apply(card, owner: null);
        ItemPickupBase pickup = InventoryExtensions.ServerCreatePickup(
            card.Base,
            new PickupSyncInfo(card.Type, card.Weight, card.Serial),
            position,
            rotation,
            spawn: false);
        card.Base.OnRemoved(pickup);
        pickup.transform.localScale = scale;

        return LabPickup.Get(pickup);
    }

    private void Apply(Keycard keycard, LabPlayer owner)
    {
        if (keycard is not CustomKeycardItem card)
        {
            // バニラカードの権限は実体ごとに持つので、拾うたびに戻す。
            keycard.Permissions = Permissions;
            return;
        }

        if (designedSerial == card.Serial)
            return;

        designedSerial = card.Serial;

        // setter ごとに全員へ再同期が飛ぶので、未指定の値には触れない。
        card.Permissions = Permissions;
        card.ItemName = string.IsNullOrEmpty(ItemName) ? Name : ItemName;
        if (TintColor is { } tint)
            card.Color = tint;
        if (PermissionsColor is { } permissionsColor)
            card.PermissionsColor = permissionsColor;

        if (card is ILabelKeycard labelCard)
        {
            if (!string.IsNullOrEmpty(Label))
                labelCard.Label = Label;
            if (LabelColor is { } labelColor)
                labelCard.LabelColor = labelColor;
        }

        if (card is INameTagKeycard nameTagCard && ResolveHolderName(owner) is { Length: > 0 } holderName)
            nameTagCard.NameTag = holderName;
        if (card is IWearKeycard wearCard && Wear is { } wear)
            wearCard.Wear = wear;
        if (card is ISerialNumberKeycard serialCard && !string.IsNullOrEmpty(SerialNumber))
            serialCard.SerialNumber = SerialNumber;
        if (card is IRankKeycard rankCard && Rank is { } rank)
            rankCard.Rank = rank;

        // Item.Create 直後のカードは未登録で setter が同期しない。Pickup の Start() は
        // 次フレームに共有の static 値を読むので、同フレームに生成した別カードの外見が混ざる前に登録する。
        KeycardDetailSynchronizer.ServerProcessItem(card.Base);
    }

    private string ResolveHolderName(LabPlayer owner)
    {
        if (!string.IsNullOrEmpty(HolderName))
            return HolderName;

        string name;
        RoleTypeId role;
        if (owner != null)
        {
            name = owner.DisplayName;
            role = owner.Role;
        }
        else
        {
            name = HolderNamePool is { } pool
                ? pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : null
                : VanillaNametag?._fakeNamesPeople.NextRandomWord();
            role = RoleTypeId.None;
        }

        if (string.IsNullOrEmpty(name))
            return null;

        return GetHolderPrefix(role) is { Length: > 0 } prefix ? $"{prefix} {name}" : name;
    }
}
