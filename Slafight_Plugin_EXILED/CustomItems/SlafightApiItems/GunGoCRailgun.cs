using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunGoCRailgun : CItemWeapon
{
    public override string DisplayName => "GOC戦略兵装 EMR-X1";
    public override string Description =>
        "<size=22>GoCのホワイトスーツに搭載予定だった主砲を、財団との協定に基づき歩兵運用向けへ再設計した電磁加速兵装。\n" +
        "対異常存在への対処能力を維持しつつ、安全性と携行性を重視した出力制限モデルであり、\n" +
        "制式採用機に比べ抑制された性能で運用される。\n" +
        "高エネルギー電磁加速機構により、小型ながら高い貫通力を発揮する。\n" +
        "<color=red>単発式：装填弾数1発のみ／最大6000ダメージの致死級出力</color></size>";

    protected override string UniqueKey => "GunGoCRailgun";
    protected override ItemType BaseItem => ItemType.ParticleDisruptor;
    protected override Vector3 Scale        => new(1.15f, 1f, 1.15f);
    protected override ushort MaxMagazineAmmo => 1;
    protected override ushort InitialMagazineAmmo => 1;
    protected override byte AmmoDrain => 1;

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Gold.ToUnityColor();

    private bool _isProcessing;

    /// <summary>
    /// 命中: 即時 2000 ダメージの Explosion を再ヒット。
    /// CItem.OnAnyHurting を経由した Hurt 呼び出しで再帰してしまうため処理中フラグでガード。
    /// </summary>
    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (_isProcessing) return;
        if (ev.Attacker is null) return;

        _isProcessing = true;
        try
        {
            ev.Amount = 0f;
            ev.Player?.ExplodeEffect(ProjectileType.FragGrenade);
            ev.Player?.Hurt(ev.Attacker, 2000f, DamageType.Explosion);
            ev.Attacker?.ShowHitMarker();
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
