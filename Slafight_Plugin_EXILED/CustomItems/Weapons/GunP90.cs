using System.Collections.Generic;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Structs;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

public class GunP90 : CustomFirearm
{
    public override string Name => "Project 90";
    public override string Description => "かつて機動部隊で使用されていた型落ちの短機関銃である。\n" +
                                          "最近はめったに見ないが、サイト-02のどこかには未だ大量に保管されているという噂も。";
    public override ItemType BaseType => ItemType.GunCrossvec;
    public override Rarity Rarity => Rarity.Rare;
    protected override Vector3 PickupScale => new(1.135f, 1.3555f, 1.08f);
    protected override float? Damage => 20f;
    protected override float? Penetration => 0.22f;
    protected override int? MagazineCapacity => 50;
    protected override IReadOnlyDictionary<GunSoundSelector, GunSoundOverride> GunSounds => new Dictionary<GunSoundSelector, GunSoundOverride>
    {
        [GunSoundKind.Gunshot] = "GunP90Fire.ogg",
        [GunSoundKind.DryFire] = "GunP90NoAmmo.ogg",
        [GunSoundKind.Reload] = "GunP90Reloading.ogg",
        [GunSoundClips.Crossvec.Silenced] = "GunP90Silence.ogg"
    };
}
