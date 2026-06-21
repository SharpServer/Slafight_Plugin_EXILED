using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerLv3 : AccessTunerBase
{
    public override string DisplayName => "Access Tuner Level-3";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "AccessTunerLv3";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.LevelThree;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
}