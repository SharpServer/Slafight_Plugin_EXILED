using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerBroken : AccessTunerBase
{
    public override string DisplayName => "Broken Access Tuner";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "AccessTunerBroken";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.Broken;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;
}