using System;
using System.Collections.Generic;
using Exiled.API.Extensions;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpyKitRecords
{
    public struct SpyData
    {
        public int PlayerId;
        public RoleTypeId PlayerBaseRole;
        public CRoleTypeId PlayerBaseCRole;
        public string PlayerBaseCustomInfo;
        public SpyMorphData NowMorphData;
    }

    public struct SpyMorphData
    {
        public RoleTypeId MorphRoleTypeId;
        public CRoleTypeId MorphCRoleTypeId;
        public string? MorphCustomName;
        public string? MorphCustomInfo;
        public ProjectMER.Features.Objects.SchematicObject? MorphSchematicObject;
        public Action<Player>? Setup;
        public Action<Player>? Cleanup;
            
        public string MorphId;
    }

    private static readonly Dictionary<int, SpyData> PlayerSpyDatas = new Dictionary<int, SpyData>();
        
    public static void Reset() => PlayerSpyDatas.Clear();
    public static Dictionary<int, SpyData> GetSpyData() => PlayerSpyDatas;
    public static SpyData GetSpyData(int playerId)
    {
        PlayerSpyDatas.TryGetValue(playerId, out var result);
        return result;
    }

    public static void Morph(this Player player, SpyMorphData morphData)
    {
        PlayerSpyDatas[player.Id] = new SpyData()
        {
            PlayerId = player.Id,
            PlayerBaseRole = player.Role.Type,
            PlayerBaseCRole = player.GetRoleInfo().Custom,
            PlayerBaseCustomInfo = CustomInfoDisplay.GetAssignedCustomInfo(player),
            NowMorphData = morphData
        };

        var data = morphData;
        if (!string.IsNullOrEmpty(data.MorphCustomName))
        {
            player.TryAddFlag(SpecificFlagType.RPNameDisabled);
            RPNameSetter.SetForcedCustomName(player, data.MorphCustomName);
        }
        if (!string.IsNullOrEmpty(data.MorphCustomInfo))
            player.SetCustomInfo(data.MorphCustomInfo!);
        if (data.MorphSchematicObject != null)
            player.Wear(data.MorphSchematicObject!);
        data.Setup?.Invoke(player);
    }

    public static void EndMorph(this Player player)
    {
        if (PlayerSpyDatas.TryGetValue(player.Id, out var playerSpyData))
        {
            var data = playerSpyData.NowMorphData;
            if (!string.IsNullOrEmpty(data.MorphCustomName))
            {
                player.TryRemoveFlag(SpecificFlagType.RPNameDisabled);
                RPNameSetter.ApplyStoredInputName(player);
            }
            if (!string.IsNullOrEmpty(data.MorphCustomInfo))
                player.SetCustomInfo(playerSpyData.PlayerBaseCustomInfo);
                
            WearsHandler.ForceRemoveWear(player);
            player.ChangeAppearance(playerSpyData.PlayerBaseRole);
            data.Cleanup?.Invoke(player);
        }
        PlayerSpyDatas.Remove(player.Id);
    }

    // ========== Collections連携API ==========
    public static SpyMorphData? GetMorph(string name) => 
        SpyMorphDataCollections.GetByName(name);

    public static List<string> GetAllMorphNames() => 
        SpyMorphDataCollections.GetAllNames();

    /// <summary>
    /// 名前でMorph（コレクションから自動取得）
    /// </summary>
    public static void MorphTo(this Player player, string morphName)
    {
        var data = SpyKitRecords.GetMorph(morphName);
        if (data.HasValue)
            player.Morph(data.Value);
        else
MeowExtensions.ShowHint(            player, $"<color=red>{morphName} が見つかりません</color>", 3f);
    }

    /// <summary>
    /// ランダムMorph（コレクションから自動選択）
    /// </summary>
    public static void MorphRandom(this Player player)
    {
        var names = SpyKitRecords.GetAllMorphNames();
        if (names.Count > 0)
            player.MorphTo(names[UnityEngine.Random.Range(0, names.Count)]);
    }
}
