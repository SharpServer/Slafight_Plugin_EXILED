using Exiled.API.Enums;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Firearm = Exiled.API.Features.Items.Firearm;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunGoCRailgunFull : CItemWeapon
{
    public override string DisplayName => "GOC戦略兵装 EMR-01";
    public override string Description =>
        "<size=22>世界オカルト連合（GOC）により正式採用された戦略級電磁加速兵装「EMR-01」。\n" +
        "対大規模異常存在の迅速な無力化を目的として設計されており、\n" +
        "超高出力の電磁加速機構により、単発で圧倒的な貫通力と破壊力を発揮する。\n" +
        "本兵装は複数弾を同時に消費しエネルギーを集約することで最大出力を実現しており、携行兵装としては規格外の性能を有する。\n" +
        "運用には厳格な安全プロトコルと適合装備（ホワイトスーツ）が必須とされる。\n" +
        "<color=red>単発式：発射ごとに弾薬を5発消費／最大出力時、15000ダメージを与える</color></size>";

    protected override string UniqueKey => "GunGoCRailgunFull";
    protected override ItemType BaseItem => ItemType.ParticleDisruptor;
    protected override Vector3 Scale        => new(1.15f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => CustomColor.Gold.ToUnityColor();

    private bool _isProcessing;

    /// <summary>命中: 即時 5000 ダメージの Explosion 再ヒット。再帰防止フラグ付き。</summary>
    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (_isProcessing) return;
        if (ev.Attacker is null) return;

        _isProcessing = true;
        try
        {
            ev.Amount = 0f;
            ev.Player?.ExplodeEffect(ProjectileType.FragGrenade);
            ev.Player?.Hurt(ev.Attacker, 5000f, DamageType.Explosion);
            ev.Attacker?.ShowHitMarker();
        }
        finally
        {
            _isProcessing = false;
        }
    }

    protected override void ApplyFirearmCustomization(Item item)
    {
        if (item is Firearm pickup)
        {
            pickup.MaxMagazineAmmo = 20;
            pickup.MagazineAmmo    = 20;
            pickup.AmmoDrain = 5;
        }
        base.ApplyFirearmCustomization(item);
    }
}
