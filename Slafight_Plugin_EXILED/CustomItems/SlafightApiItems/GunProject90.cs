using System.Collections.Generic;
using System.Collections.ObjectModel;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Structs;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunProject90 : CItemWeapon
{
    public override string DisplayName => "Project-90";
    public override string Description => "昔ながらの、安定した撃ちどけ";
    protected override string UniqueKey => "GunProject90";
    protected override ItemType BaseItem => ItemType.GunCrossvec;
    protected override float Damage => 38f;
    protected override byte MagazineSize => 25;
    protected override Vector3 Scale => new(1.135f, 1.3555f, 1.08f);
    protected override IReadOnlyDictionary<GunSoundSelector, GunSoundOverride> OverrideSounds => new Dictionary<GunSoundSelector, GunSoundOverride>
    {
        [GunSoundKind.Gunshot] = "GunP90Fire.ogg",
        [GunSoundKind.DryFire] = "GunP90NoAmmo.ogg",
        [GunSoundKind.Reload] = "GunP90Reloading.ogg",
        [GunSoundClips.Crossvec.Silenced] = "GunP90Silence.ogg"
    };
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.cyan;
}
