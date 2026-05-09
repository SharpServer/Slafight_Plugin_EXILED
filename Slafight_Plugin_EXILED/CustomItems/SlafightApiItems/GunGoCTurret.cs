using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunGoCTurret : CItemWeapon
{
    public override string DisplayName => "GOC高出力指向性電磁照射兵装《HID-Ω》";
    public override string Description => "<size=22>財団製MicroHIDをベースに、世界オカルト連合（GOC）が独自に大幅改修を施した高出力モデル。\n" +
                                          "出力制御系および冷却機構は戦闘効率を最優先に再設計されており、原型機に存在した安全制限の大半は意図的に解除されている。\n" +
                                          "これにより照射出力と持続時間は飛躍的に向上したが、使用者および周辺環境への負荷も著しく増大している。\n" +
                                          "対異常存在の強制無力化を主目的とした、極めて攻撃的な運用思想のもと開発された兵装である。\n" +
                                          "<color=red>高出力連続照射型：使用中はエネルギーを急速消費／過熱時、強制停止および使用者へダメージ</color></size>";
    protected override string UniqueKey => "GunGoCTurret";
    protected override ItemType BaseItem => ItemType.MicroHID;
    protected override Vector3 Scale => new(1.15f, 1f, 1.15f);
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.Gold.ToUnityColor();

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.Amount = 100f;
        ev.Player?.ExplodeEffect(ProjectileType.FragGrenade);
        base.OnHurtingOthers(ev);
    }
}
