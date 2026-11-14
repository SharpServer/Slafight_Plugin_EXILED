using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.SpawnSets.Fifthists;

/// <summary>第五教会の波が共有する出撃放送です。</summary>
internal static class FifthistsAnnouncement
{
    public static void Build(SpawnAnnouncement a)
    {
        a.Cassie($"Attention All personnel . Detected {a.SpawnCount} $pitch_1.05 5 5 5 $pitch_1 Forces in Gate B . Please Terminate Them")
            .Subtitle($"全職員に通達。Gate Bに{a.SpawnCount}人の{SpawnAnnouncement.Colored("第五主義者", ServerColors.Magenta)}が検出されました。")
            .Line("見つけ次第終了してください。");
    }
}
