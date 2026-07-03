using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerLv2 : AccessTunerBase
{
    public override string DisplayName => "Access Tuner Level-2";
    public override string Description => "施設のメンテナンスに使用される整備用の診断装置。\n" +
                                          "Lv2のデータセルによってある程度の権限の扉や施設に対して\n" +
                                          "ハッキングできる。";
    protected override string UniqueKey => "AccessTunerLv2";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.LevelTwo;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => ServerColors.Orange.GetColorFromString();
}