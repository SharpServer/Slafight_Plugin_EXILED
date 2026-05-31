using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.MicroHID.Modules;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class HIDTurret : CItem
{
    public override string DisplayName => "MicroHID-C（連続運用モデル）";
    public override string Description =>
        "<size=22>財団により運用される低出力型MicroHIDの改修モデル。\n" +
        "出力は小チャージに限定されているが、安定性と継続使用を重視した設計により長時間の照射が可能となっている。\n" +
        "主に制圧、牽制、および対象の行動制限を目的として使用される。\n" +
        "<color=red>小出力連続照射型：エネルギー無制限／持続的にダメージを与える</color></size>";

    protected override string UniqueKey => "HIDTurret";
    protected override ItemType BaseItem => ItemType.MicroHID;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.yellow;

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.ChangingMicroHIDState += OnChangingMicroHIDState;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.ChangingMicroHIDState -= OnChangingMicroHIDState;
        base.UnregisterEvents();
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.Amount = 20f;
        base.OnHurtingOthers(ev);
    }

    /// <summary>
    /// チャージ撃ち (大チャージ) を禁止し、小チャージのみ使用可能にする。
    /// </summary>
    private void OnChangingMicroHIDState(ChangingMicroHIDStateEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        /*Log.Debug($"MicroHID-C Info: \n" +
                  $"Now Phase: {ev.MicroHID.State}\n" +
                  $"New Phase: {ev.NewPhase}\n" +
                  $"IsPrimary: {ev.MicroHID.IsPrimary}" +
                  $"LastMode: {ev.MicroHID.LastFiringMode}");
                  */

        if (ev.NewPhase is MicroHidPhase.WindingUp && !ev.MicroHID.IsPrimary)
        {
            ev.IsAllowed = false;
MeowExtensions.ShowHint(             ev.Player, "<size=24>※この武器の強チャージ照射は無効化されています！</size>");
        }
    }
}

