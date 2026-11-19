using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomEffects;

namespace Slafight_Plugin_EXILED.CustomItems.Utils;

public class PeanutsKillers : CustomUsableItem
{
    public override string Name => "ピーナッツキラー-X1";
    public override string Description => "<b>S</b>uper <b>C</b>reemy <b>P</b>eanuts社の新製品！\n" +
                                          "「どれだけ恐ろしいピーナッツと相対しても大丈夫！」\n" +
                                          "あなたの首は一度ぐらいは耐えられるように\n" +
                                          $"<color={ServerColors.Magenta}>アンチ・ピーナッツ力</color>で保護されることでしょう！";
    public override ItemType BaseType => ItemType.Painkillers;

    protected override void OnUseCompleted(PlayerUsedItemEventArgs ev)
    {
        Owner.EnableEffect<AntiPeanuts>();
        base.OnUseCompleted(ev);
    }
}
