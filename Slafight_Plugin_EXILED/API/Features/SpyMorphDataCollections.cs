using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using PlayerRoles;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpyMorphDataCollections
{
    private static readonly Dictionary<string, SpyKitRecords.SpyMorphData> Collections = new()
    {
        // ========== 基本変装 ==========
        ["ClassD"] = new SpyKitRecords.SpyMorphData
        {
            MorphRoleTypeId = RoleTypeId.ClassD,
            MorphId = "ClassD",
            Setup = p => { WearsHandler.ForceRemoveWear(p); p.ChangeAppearance(RoleTypeId.ClassD); }
        },
        ["Scientist"] = new SpyKitRecords.SpyMorphData
        {
            MorphRoleTypeId = RoleTypeId.Scientist,
            MorphId = "Scientist",
            Setup = p => { WearsHandler.ForceRemoveWear(p); p.ChangeAppearance(RoleTypeId.Scientist); }
        },
        ["FacilityGuard"] = new SpyKitRecords.SpyMorphData
        {
            MorphRoleTypeId = RoleTypeId.FacilityGuard,
            Setup = p => { WearsHandler.ForceRemoveWear(p); p.ChangeAppearance(RoleTypeId.FacilityGuard); }
        },
        ["NtfPrivate"] = new SpyKitRecords.SpyMorphData
        {
            MorphRoleTypeId = RoleTypeId.NtfPrivate,
            Setup = p => { WearsHandler.ForceRemoveWear(p); p.ChangeAppearance(RoleTypeId.NtfPrivate); }
        },
        ["ChaosRifleman"] = new SpyKitRecords.SpyMorphData
        {
            MorphRoleTypeId = RoleTypeId.ChaosRifleman,
            Setup = p => { WearsHandler.ForceRemoveWear(p); p.ChangeAppearance(RoleTypeId.ChaosRifleman); }
        },
    };

    public static SpyKitRecords.SpyMorphData? GetByName(string name) => 
        Collections.TryGetValue(name, out var data) ? data : null;

    public static SpyKitRecords.SpyMorphData? GetByRole(RoleTypeId roleType) => 
        Collections.Values.FirstOrDefault(d => d.MorphRoleTypeId == roleType);

    public static List<SpyKitRecords.SpyMorphData> GetAll() => 
        Collections.Values.ToList();

    public static List<string> GetAllNames() => 
        Collections.Keys.ToList();

    // 外部から追加
    public static void Add(string name, SpyKitRecords.SpyMorphData data) => 
        Collections[name] = data;

    // 削除
    public static void Remove(string name) => 
        Collections.Remove(name);
}