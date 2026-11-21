using Exiled.Events.EventArgs.Player;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

public class GunGocRailgun : CustomFirearm
{
    public override string Name => "GOC戦略兵装 EMR-X1";
    public override string Description =>
        "<size=22>GoCのホワイトスーツに搭載予定だった主砲を、財団との協定に基づき歩兵運用向けへ再設計した電磁加速兵装。\n" +
        "対異常存在への対処能力を維持しつつ、安全性と携行性を重視した出力制限モデルであり、\n" +
        "制式採用機に比べ抑制された性能で運用される。\n" +
        "高エネルギー電磁加速機構により、小型ながら高い貫通力を発揮する。\n" +
        "<color=red>単発式：装填弾数1発のみ／最大6000ダメージの致死級出力</color></size>";

    public override ItemType BaseType => ItemType.ParticleDisruptor;
    public override Rarity Rarity => Rarity.Legendary;
    protected override Vector3 PickupScale => new(2f, 1f, 1f);
    protected override float? Damage => 2000f;
    protected override int? MagazineCapacity => 1;
}
