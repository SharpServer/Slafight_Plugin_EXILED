using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerLv1 : AccessTunerBase
{
    public override string DisplayName => "Access Tuner Level-1";
    public override string Description => "施設のメンテナンスに使用される整備用の診断装置。\n" +
                                          "Lv1のデータセルによって低い権限の扉や施設に対して\n" +
                                          "ハッキングできる。";
    protected override string UniqueKey => "AccessTunerLv1";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.LevelOne;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.white;
}