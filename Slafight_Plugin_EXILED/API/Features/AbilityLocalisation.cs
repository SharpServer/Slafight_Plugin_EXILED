using System.Collections.Generic;
using Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;

namespace Slafight_Plugin_EXILED.API.Features;

public static class AbilityLocalization
{
    private static readonly Dictionary<string, string> JaNames = new()
    {
        ["CreateSinkholeAbility"] = "シンクホール",
        ["MagicMissileAbility"] = "マジックミサイル",
        ["AllowEscapeAbility"] = "腐蝕からの解放",
        ["SoundOfFifthAbility"] = "第五からの音",
        ["TeleportRandomAbility"] = "メインヴィラン",
        ["PlaceTantrumAbility"] = "汚物作戦",
        ["Scp035TentacleAbility"] = "触手",
        ["Scp966SleepWaveAbility"] = "██████波",
        ["Scp966SpeedAbility"] = "這いよる混沌",
        ["DropBiggerShitAbility"] = "爺街道",
        ["MemeWaveAbility"] = "ミーム波動",
        ["SenseOfGreatDoctor "] = "名医の感",
    };
    private static readonly Dictionary<string, string> JaNamesSergey = new()
    {
        ["CreateSinkholeAbility"] = "怨みの沼",
        ["MagicMissileAbility"] = "呪詛",
        ["AllowEscapeAbility"] = "呪縛からの解放",
        ["SoundOfFifthAbility"] = "管理官の祟り",
        ["TeleportRandomAbility"] = "背後からの一突き",
        ["PlaceTantrumAbility"] = "精神破壊幻覚の顕現",
        ["Scp035TentacleAbility"] = "冥界からの呼び声",
        ["Scp966SleepWaveAbility"] = "不眠の呪詛",
        ["Scp966SpeedAbility"] = "背後からの一突き",
        ["DropBiggerShitAbility"] = "怨念大肛爆発",
        ["MemeWaveAbility"] = "ミーム波動",
    };

    public static string GetDisplayName(string key, Exiled.API.Features.Player player)
    {
        if (player.IsSergeyMarkov())
        {
            return JaNamesSergey.TryGetValue(key, out var nameS) ? nameS :
                // フォールバック
                key.Replace("Ability", string.Empty);
        }
        return JaNames.TryGetValue(key, out var name) ? name :
            // フォールバック
            key.Replace("Ability", string.Empty);
    }
}
