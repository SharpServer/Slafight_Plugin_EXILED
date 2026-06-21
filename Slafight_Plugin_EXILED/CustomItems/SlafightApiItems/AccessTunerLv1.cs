using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerLv1 : AccessTunerBase
{
    public override string DisplayName => "Access Tuner Level-1";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "AccessTunerLv1";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.LevelOne;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.white;
}