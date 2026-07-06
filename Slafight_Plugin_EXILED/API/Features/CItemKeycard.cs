#nullable enable
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Items.Keycards;
using Exiled.API.Features.Pickups;
using Exiled.API.Interfaces.Keycards;
using UnityEngine;

using ItemHandlers = Exiled.Events.Handlers.Item;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ItemEvents = Exiled.Events.EventArgs.Item;
using PlayerEvents = Exiled.Events.EventArgs.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// CItem の Keycard 特化派生。Exiled.CustomItems.CustomKeycard 互換のカスタマイズ
/// (Label / NameTag / Tint / Permissions / Wear / Serial / Rank) を virtual で受け取り、
/// <see cref="Item.Create(ItemType, Player)"/> + <see cref="CustomKeycardItem"/> 直書きで
/// Spawn / Give 双方の経路にカスタマイズを適用する。
/// </summary>
/// <remarks>
/// Keycard の見た目情報は <c>CustomKeycardItem.DataDict[serial]</c> や
/// <c>CustomPermsDetail.CustomPermissions[serial]</c> といったサーバ側 static マップへ書き込まれ、
/// 該当 Item が網羅する Resync で同期される。Spawn 経路ではまず <see cref="Item.Create"/>
/// で Item を作って serial を確保した後にカスタマイズを焼き、<see cref="Item.CreatePickup"/>
/// で同じ serial を持つ Pickup に変換する。
/// </remarks>
public abstract class CItemKeycard : CItem
{
    /// <summary>
    /// 既定は <see cref="ItemType.KeycardCustomSite02"/>。MetalCase / TaskForce などを
    /// 使う派生は本プロパティを override する。
    /// </summary>
    protected override ItemType BaseItem => ItemType.KeycardCustomSite02;

    // ==== Exiled CustomKeycard 互換カスタマイズプロパティ ====

    /// <summary>カードに記載される所持者名 (NameTag)。</summary>
    protected virtual string KeycardName => string.Empty;

    /// <summary>カードに記載されるラベル文字列。</summary>
    protected virtual string KeycardLabel => string.Empty;

    /// <summary>ラベル文字色。</summary>
    protected virtual Color32? KeycardLabelColor => null;

    /// <summary>カード本体の色 (Tint)。</summary>
    protected virtual Color32? TintColor => null;

    /// <summary>権限。</summary>
    protected virtual KeycardPermissions Permissions => KeycardPermissions.None;

    /// <summary>EXILED の生 KeycardPermissions flags。</summary>
    public KeycardPermissions KeycardPermissionFlags => Permissions;

    /// <summary>ゲーム内表示に合わせた Containment / Armory / Administration レベル。</summary>
    public KeycardAccessLevels AccessLevels => KeycardAccessLevels.FromPermissions(Permissions);

    /// <summary>権限表示部分の色。</summary>
    protected virtual Color32? KeycardPermissionsColor => null;

    /// <summary>摩耗 (Site02 / MetalCase でのみ有効)。byte.MaxValue でデフォルト。</summary>
    protected virtual byte Wear => byte.MaxValue;

    /// <summary>シリアル番号 (MetalCase / TaskForce でのみ表示)。</summary>
    protected virtual string SerialNumber => string.Empty;

    /// <summary>ランク (TaskForce でのみ有効、0-3 で逆順)。byte.MaxValue でデフォルト。</summary>
    protected virtual byte Rank => byte.MaxValue;

    /// <summary>
    /// インベントリに表示される名称。空の場合 <see cref="DisplayName"/> を流用する。
    /// </summary>
    protected virtual string KeycardItemName => DisplayName;

    // ==== CItem hooks ====

    public override void RegisterEvents()
    {
        ItemHandlers.KeycardInteracting += OnAnyKeycardInteracting;
        PlayerHandlers.InteractingDoor += OnAnyInteractingDoor;
        PlayerHandlers.UnlockingGenerator += OnAnyUnlockingGenerator;
        PlayerHandlers.OpeningGenerator += OnAnyOpeningGenerator;
        PlayerHandlers.ClosingGenerator += OnAnyClosingGenerator;
        PlayerHandlers.ActivatingGenerator += OnAnyActivatingGenerator;
        PlayerHandlers.StoppingGenerator += OnAnyStoppingGenerator;

        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        ItemHandlers.KeycardInteracting -= OnAnyKeycardInteracting;
        PlayerHandlers.InteractingDoor -= OnAnyInteractingDoor;
        PlayerHandlers.UnlockingGenerator -= OnAnyUnlockingGenerator;
        PlayerHandlers.OpeningGenerator -= OnAnyOpeningGenerator;
        PlayerHandlers.ClosingGenerator -= OnAnyClosingGenerator;
        PlayerHandlers.ActivatingGenerator -= OnAnyActivatingGenerator;
        PlayerHandlers.StoppingGenerator -= OnAnyStoppingGenerator;

        base.UnregisterEvents();
    }

    /// <summary>
    /// Spawn 経路: <see cref="Item.Create"/> で先に Item を作り、Keycard カスタマイズを
    /// 焼き込んでから同 Serial の Pickup に変換する。これによりカスタマイズが
    /// <c>DataDict[serial]</c> に反映済みの状態で Pickup が生成される。
    /// </summary>
    protected override Pickup? CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(BaseItem);
        if (item == null) return null;

        ApplyKeycardCustomization(item);
        return item.CreatePickup(position);
    }

    /// <summary>Give 経路で AddItem 直後に呼ばれるカスタマイズ。</summary>
    protected override void CustomizeItem(Item item)
    {
        ApplyKeycardCustomization(item);
        base.CustomizeItem(item);
    }

    // ==== カスタマイズ適用本体 ====

    /// <summary>
    /// Exiled <c>CustomKeycard.SetupKeycard</c> 互換のカスタマイズ適用。
    /// 派生は通常 override 不要だが、追加カスタマイズが必要な場合は override 可能。
    /// </summary>
    protected virtual void ApplyKeycardCustomization(Item item)
    {
        if (item is not Keycard keycard) return;
        if (keycard is not CustomKeycardItem ck) return;

        ck.Permissions = Permissions;

        if (KeycardPermissionsColor.HasValue)
            ck.PermissionsColor = KeycardPermissionsColor.Value;

        if (TintColor.HasValue)
            ck.Color = TintColor.Value;

        if (!string.IsNullOrEmpty(KeycardItemName))
            ck.ItemName = KeycardItemName;

        if (!string.IsNullOrEmpty(KeycardName) && ck is INameTagKeycard nameTag)
            nameTag.NameTag = KeycardName;

        if (ck is ILabelKeycard label)
        {
            if (!string.IsNullOrEmpty(KeycardLabel))
                label.Label = KeycardLabel;

            if (KeycardLabelColor.HasValue)
                label.LabelColor = KeycardLabelColor.Value;
        }

        if (ck is IWearKeycard wear)
            wear.Wear = Wear;

        if (ck is ISerialNumberKeycard sn)
            sn.SerialNumber = SerialNumber;

        if (ck is IRankKeycard rank)
            rank.Rank = Rank;
    }

    // ==== Keycard interaction hooks ====

    /// <summary>
    /// 床や投げられた Keycard が Door に接触したとき。
    /// </summary>
    protected virtual void OnKeycardInteracting(ItemEvents.KeycardInteractingEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Door を操作したとき。
    /// </summary>
    protected virtual void OnInteractingDoor(PlayerEvents.InteractingDoorEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Generator のロックを解除しようとしたとき。
    /// </summary>
    protected virtual void OnUnlockingGenerator(PlayerEvents.UnlockingGeneratorEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Generator の扉を開けようとしたとき。
    /// </summary>
    protected virtual void OnOpeningGenerator(PlayerEvents.OpeningGeneratorEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Generator の扉を閉じようとしたとき。
    /// </summary>
    protected virtual void OnClosingGenerator(PlayerEvents.ClosingGeneratorEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Generator を起動しようとしたとき。
    /// </summary>
    protected virtual void OnActivatingGenerator(PlayerEvents.ActivatingGeneratorEventArgs ev) { }

    /// <summary>
    /// この Keycard を手に持って Generator を停止しようとしたとき。
    /// </summary>
    protected virtual void OnStoppingGenerator(PlayerEvents.StoppingGeneratorEventArgs ev) { }

    private void OnAnyKeycardInteracting(ItemEvents.KeycardInteractingEventArgs ev)
    {
        if (!Check(ev.KeycardPickup)) return;
        OnKeycardInteracting(ev);
    }

    private void OnAnyInteractingDoor(PlayerEvents.InteractingDoorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnInteractingDoor(ev);
    }

    private void OnAnyUnlockingGenerator(PlayerEvents.UnlockingGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnUnlockingGenerator(ev);
    }

    private void OnAnyOpeningGenerator(PlayerEvents.OpeningGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnOpeningGenerator(ev);
    }

    private void OnAnyClosingGenerator(PlayerEvents.ClosingGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnClosingGenerator(ev);
    }

    private void OnAnyActivatingGenerator(PlayerEvents.ActivatingGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnActivatingGenerator(ev);
    }

    private void OnAnyStoppingGenerator(PlayerEvents.StoppingGeneratorEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        OnStoppingGenerator(ev);
    }
}
