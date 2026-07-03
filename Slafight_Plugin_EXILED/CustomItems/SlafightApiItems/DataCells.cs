using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public abstract class DataCellBase : CItem
{
    private const float ResultHintDuration = 4f;

    protected override ItemType BaseItem => ItemType.Ammo44cal;
    protected override bool ShowPickedUpHint => false;
    protected override bool ShowSelectedHint => false;
    protected override bool PickupLightEnabled => true;
    protected abstract AccessTunerBase.AccessTunerLevel DataCellLevel { get; }

    protected override void OnPickingUp(PickingUpItemEventArgs ev)
    {
        ev.IsAllowed = false;

        if (ev.Player == null || ev.Pickup == null)
            return;

        if (TryUse(ev.Player))
            ConsumePickup(ev);
    }

    /// <summary>
    /// この Data Cell をプレイヤーの Access Tuner に適用し、結果ヒントを表示する。
    /// 消費された（強化 / ポイント充填に成功した）場合は true。
    /// </summary>
    public bool TryUse(Player player)
    {
        if (!TryGetTargetAccessTuner(player, out var accessTuner, out var accessTunerItem) ||
            accessTuner == null ||
            accessTunerItem == null)
        {
            player.ShowHint("<color=#ff7777>Access Tuner が必要です。</color>", ResultHintDuration);
            return false;
        }

        AccessTunerBase.DataCellApplyResult result =
            accessTuner.ApplyDataCell(player, accessTunerItem, DataCellLevel);

        switch (result)
        {
            case AccessTunerBase.DataCellApplyResult.Upgraded:
                player.ShowHint(
                    $"<color=#66ddff>Access Tuner が Lv.{(int)DataCellLevel} に強化されました。</color>",
                    ResultHintDuration);
                return true;

            case AccessTunerBase.DataCellApplyResult.FilledPoints:
                player.ShowHint(
                    $"<color=#88ff88>Access Tuner Lv.{(int)DataCellLevel} のポイントが最大になりました。</color>",
                    ResultHintDuration);
                return true;

            case AccessTunerBase.DataCellApplyResult.LowerThanTuner:
                player.ShowHint(
                    "<color=#ff7777>Access Tuner より低い Lv の Data Cell は使用できません。</color>",
                    ResultHintDuration);
                return false;

            default:
                player.ShowHint("<color=#ff7777>この Data Cell は使用できません。</color>", ResultHintDuration);
                return false;
        }
    }

    private void ConsumePickup(PickingUpItemEventArgs ev)
    {
        ushort serial = ev.Pickup.Serial;
        var pickup = ev.Pickup;

        ev.Pickup.Destroy();
        MEC.Timing.CallDelayed(0.1f, () =>
        {
            RemovePickupLight(pickup);
            CItem.SerialTracker.ForceUnregister(serial);
        });
    }

    private static bool TryGetTargetAccessTuner(
        Player player,
        out AccessTunerBase? accessTuner,
        out Item? accessTunerItem)
    {
        accessTuner = null;
        accessTunerItem = null;

        if (TryResolveAccessTuner(player.CurrentItem, out accessTuner, out accessTunerItem))
            return true;

        foreach (var item in player.Items.ToList())
        {
            if (!TryResolveAccessTuner(item, out var candidate, out var candidateItem) ||
                candidate == null ||
                candidateItem == null)
            {
                continue;
            }

            if (accessTuner == null ||
                candidate.GetCurrentLevelNumber(candidateItem.Serial) >
                accessTuner.GetCurrentLevelNumber(accessTunerItem!.Serial))
            {
                accessTuner = candidate;
                accessTunerItem = candidateItem;
            }
        }

        return accessTuner != null && accessTunerItem != null;
    }

    private static bool TryResolveAccessTuner(
        Item? item,
        out AccessTunerBase? accessTuner,
        out Item? accessTunerItem)
    {
        accessTuner = null;
        accessTunerItem = null;

        if (item == null || !CItem.TryGet(item, out var cItem) || cItem is not AccessTunerBase tuner)
            return false;

        accessTuner = tuner;
        accessTunerItem = item;
        return true;
    }
}

public sealed class DataCellLv1 : DataCellBase
{
    public override string DisplayName => "Data Cell Level-1";
    public override string Description => "Access Tuner Lv.1 と同期するデータセル。";
    protected override string UniqueKey => "DataCellLv1";
    protected override string? PickupSchematicName => "Alienisolation_Datacell_lv1";
    protected override Color PickupLightColor => Color.white;
    protected override AccessTunerBase.AccessTunerLevel DataCellLevel => AccessTunerBase.AccessTunerLevel.LevelOne;
}

public sealed class DataCellLv2 : DataCellBase
{
    public override string DisplayName => "Data Cell Level-2";
    public override string Description => "Access Tuner Lv.2 と同期するデータセル。";
    protected override string UniqueKey => "DataCellLv2";
    protected override string? PickupSchematicName => "Alienisolation_Datacell_lv1";
    protected override Color PickupLightColor => new(1f, 0.45f, 0f);
    protected override AccessTunerBase.AccessTunerLevel DataCellLevel => AccessTunerBase.AccessTunerLevel.LevelTwo;
}

public sealed class DataCellLv3 : DataCellBase
{
    public override string DisplayName => "Data Cell Level-3";
    public override string Description => "Access Tuner Lv.3 と同期するデータセル。";
    protected override string UniqueKey => "DataCellLv3";
    protected override string? PickupSchematicName => "Alienisolation_Datacell_lv1";
    protected override Color PickupLightColor => Color.red;
    protected override AccessTunerBase.AccessTunerLevel DataCellLevel => AccessTunerBase.AccessTunerLevel.LevelThree;
}
