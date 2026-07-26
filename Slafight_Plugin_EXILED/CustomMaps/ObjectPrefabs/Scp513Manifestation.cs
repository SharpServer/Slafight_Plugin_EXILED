using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features.Entities;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// SCP-513のストーキング演出1体分。<see cref="Scp513"/> がターゲット管理と
/// スポーンサイクルを担い、このクラスは Position/Create/Destroy のライフサイクルを
/// ObjectPrefab に寄せ、対象プレイヤーへの追従と可視状態設定のみ行う。
/// </summary>
public class Scp513Manifestation : ObjectPrefab
{
    protected override string SchematicName => "SCP513";
    protected override float SetupDelay => 0f;

    public Player? TargetPlayer { get; set; }

    protected override void OnSetup()
    {
        if (Schematic == null || TargetPlayer?.ReferenceHub == null)
        {
            Destroy();
            return;
        }

        Schematic.transform.SetParent(TargetPlayer.Transform, true);
        Schematic.NetworkIdentities.InitShowState(new NetworkShowState
        {
            OwnerId = TargetPlayer.Id,
            ShowToOwner = true,
            SpectatorVisibility = SpectatorVisibility.Show,
        });
    }

    protected override void OnDestroy()
    {
        Schematic?.NetworkIdentities.RemoveShowState();
    }
}
