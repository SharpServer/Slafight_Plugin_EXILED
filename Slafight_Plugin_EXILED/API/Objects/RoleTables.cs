using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.API.Objects
{
    /// <summary>
    /// 重み付きロール
    /// </summary>
    public readonly record struct WeightedRoleEntry(object Role, float Weight);

    /// <summary>
    /// 重み付きロール一覧からランダムに1つ選択
    /// </summary>
    public static class WeightedRole
    {
        public static object? Choose(List<WeightedRoleEntry>? source)
        {
            if (source == null || source.Count == 0)
                return null;

            float totalWeight = 0f;
            foreach (var item in source)
                totalWeight += item.Weight;

            if (totalWeight <= 0f)
                return null;

            float random = Random.value * totalWeight;
            float current = 0f;

            foreach (var item in source)
            {
                current += item.Weight;
                if (random <= current)
                    return item.Role;
            }

            // 万が一落ちてきたら最後の1つ
            return source[^1].Role;
        }
    }

    /// <summary>
    /// 1 ロールの上限情報
    /// </summary>
    public readonly record struct RoleLimitEntry(object Role, int Limit);

    /// <summary>
    /// 1 モード分のロール制限集合
    /// </summary>
    public readonly record struct RoleLimitPool(List<RoleLimitEntry> Limits);

    /// <summary>
    /// 1 モード分のロールセット（SCP＋人間全種）
    /// </summary>
    public record struct RoleTablePool(
        List<WeightedRoleEntry> ScpRoles,
        List<WeightedRoleEntry> ScientistRoles,
        List<WeightedRoleEntry> GuardRoles,
        List<WeightedRoleEntry> ClassDRoles
    );

    /// <summary>
    /// モードごとのロールテーブル＋ロール上限を1つにまとめる
    /// </summary>
    public record struct RoleModePool(
        RoleTablePool Tables,
        RoleLimitPool Limits
    );

    /// <summary>
    /// 動的なロールテーブル・ロール上限の切り替え用
    /// </summary>
    public static class RoleTables
    {
        private static WeightedRoleEntry W(object role, float weight = 1.0f)
            => new(role, weight);

        // 通常 SCP
        private static readonly List<WeightedRoleEntry> ScpRolesNormal =
        [
            W(RoleTypeId.Scp173, 1.15f),
            W(RoleTypeId.Scp049, 1.08f),
            W(RoleTypeId.Scp079, 1.05f),
            W(RoleTypeId.Scp096, 0.85f),
            W(RoleTypeId.Scp106, 1.1f),
            W(RoleTypeId.Scp939, 1.1f),
            W(RoleTypeId.Scp3114, 0.95f),
            W(CRoleTypeId.Scp3005, 1.05f),
            W(CRoleTypeId.Scp966, 1.05f),
            W(CRoleTypeId.Scp682, 0.8f),
            W(CRoleTypeId.Scp035, 0.8f),
            W(CRoleTypeId.Scp999, 0.77f),
            W(CRoleTypeId.Scp610, 1.0264f)
        ];

        // Scientist
        private static readonly List<WeightedRoleEntry> ScientistRoles =
        [
            W(RoleTypeId.Scientist, 1.0f),
            W(CRoleTypeId.ZoneManager, 1.0f),
            W(CRoleTypeId.FacilityManager, 1.0f),
            W(CRoleTypeId.Engineer, 1.0f),
            W(CRoleTypeId.ObjectObserver, 1.0f)
        ];
        
        private static readonly List<WeightedRoleEntry> ScientistRolesApril =
        [
            W(RoleTypeId.Scientist, 1.0f),
            W(CRoleTypeId.ZoneManager, 1.0f),
            W(CRoleTypeId.FacilityManager, 1.0f),
            W(CRoleTypeId.Engineer, 1.0f),
            W(CRoleTypeId.ObjectObserver, 1.0f),
            
            W(CRoleTypeId.CandyResearcher, 1.05f),
        ];

        // Guard
        private static readonly List<WeightedRoleEntry> GuardRoles =
        [
            W(RoleTypeId.FacilityGuard, 1.0f),
            W(CRoleTypeId.EvacuationGuard, 1.0f),
            W(CRoleTypeId.SecurityChief, 1.0f),
            W(CRoleTypeId.ChamberGuard, 1.0f)
        ];

        // Class-D
        private static readonly List<WeightedRoleEntry> ClassDRoles =
        [
            W(RoleTypeId.ClassD, 1.0f),
            W(CRoleTypeId.Janitor, 1.0f),
            W(CRoleTypeId.ChaosUndercoverAgent, 1.0f)
        ];

        private static readonly List<WeightedRoleEntry> ClassDRolesApril =
        [
            W(RoleTypeId.ClassD, 1.0f),
            W(CRoleTypeId.Janitor, 1.0f),
            W(CRoleTypeId.ChaosUndercoverAgent, 1.0f),
            
            W(CRoleTypeId.CandySubject, 1.05f),
        ];


        // 通常モードの上限
        private static readonly RoleLimitPool RoleLimitPoolNormal = new(
            [
                new(CRoleTypeId.Janitor, 3),
                new(CRoleTypeId.ChaosUndercoverAgent, 1),

                new(CRoleTypeId.ZoneManager, 2),
                new(CRoleTypeId.FacilityManager, 1),
                new(CRoleTypeId.ObjectObserver, 1),

                new(CRoleTypeId.EvacuationGuard, 1),
                new(CRoleTypeId.SecurityChief, 1),
                new(CRoleTypeId.ChamberGuard, 1),

                new(CRoleTypeId.Scp682, 1)
            ]
        );

        // April Fools
        private static readonly RoleLimitPool RoleLimitPoolApril = new(
            [
                new(CRoleTypeId.Janitor, 2),
                new(CRoleTypeId.ChaosUndercoverAgent, 1),
                new(CRoleTypeId.CandySubject, 2),

                new(CRoleTypeId.ZoneManager, 2),
                new(CRoleTypeId.FacilityManager, 1),
                new(CRoleTypeId.ObjectObserver, 1),
                new(CRoleTypeId.CandyResearcher, 2),

                new(CRoleTypeId.EvacuationGuard, 1),
                new(CRoleTypeId.SecurityChief, 1),
                new(CRoleTypeId.ChamberGuard, 1),

                new(CRoleTypeId.Scp682, 1)
            ]
        );

        // モードごとのテーブル＋上限ペア
        private static readonly Dictionary<string, RoleModePool> ModePools = new()
        {
            {
                "Normal",
                new RoleModePool(
                    Tables: new RoleTablePool(
                        ScpRoles: ScpRolesNormal,
                        ScientistRoles: ScientistRoles,
                        GuardRoles: GuardRoles,
                        ClassDRoles: ClassDRoles
                    ),
                    Limits: RoleLimitPoolNormal
                )
            },
            {
                "April",
                new RoleModePool(
                    Tables: new RoleTablePool(
                        ScpRoles: ScpRolesNormal,
                        ScientistRoles: ScientistRolesApril,
                        GuardRoles: GuardRoles,
                        ClassDRoles: ClassDRolesApril
                    ),
                    Limits: RoleLimitPoolApril
                )
            }
            // 追加で、"hardcore", "casual" などあとで追加可
        };

        private static string _currentMode = "Normal";

        /// <summary>
        /// モードを切り替える（例: "normal", "no682"）
        /// </summary>
        public static void SetCurrentMode(string mode)
        {
            if (ModePools.ContainsKey(mode))
            {
                _currentMode = mode;
            }
            else
            {
                Log.Warn($"[RoleTables] Unknown mode: {mode}, fallback to 'normal'");
            }
        }

        /// <summary>
        /// 現在のモードに対応するロールセット（テーブル）を返す
        /// </summary>
        public static RoleTablePool GetCurrentTablePool()
            => ModePools[_currentMode].Tables;

        /// <summary>
        /// 現在のモードに対応するロール上限セットを返す
        /// </summary>
        public static RoleLimitPool GetCurrentLimitPool()
            => ModePools[_currentMode].Limits;

        /// <summary>
        /// SCP ロールテーブル（重み付き）を動的に返す
        /// </summary>
        public static List<WeightedRoleEntry> GetScpRoles()
            => GetCurrentTablePool().ScpRoles;

        /// <summary>
        /// 科学者系ロールテーブル
        /// </summary>
        public static List<WeightedRoleEntry> GetScientistRoles()
            => GetCurrentTablePool().ScientistRoles;

        /// <summary>
        /// 警備員系ロールテーブル
        /// </summary>
        public static List<WeightedRoleEntry> GetGuardRoles()
            => GetCurrentTablePool().GuardRoles;

        /// <summary>
        /// Class‑D 系ロールテーブル
        /// </summary>
        public static List<WeightedRoleEntry> GetClassDRoles()
            => GetCurrentTablePool().ClassDRoles;
    }
}
