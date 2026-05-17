using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.Commands.DevTools;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using ServerHandler = Exiled.Events.Handlers.Server;
using MapHandler = Exiled.Events.Handlers.Map;
using PlayerHandler = Exiled.Events.Handlers.Player;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class CustomMapMainHandler : CustomEventsHandler, IBootstrapHandler
{
    public static CustomMapMainHandler Instance { get; private set; }
    public static void Register() { Instance = new(); CustomHandlersManager.RegisterEventsHandler(Instance); }
    public static void Unregister() { CustomHandlersManager.UnregisterEventsHandler(Instance); Instance = null; }

    private const float PositionToleranceSq = 1.45f * 1.45f; // SqrMagnitude 比較用に二乗済み
    private const float FemurJoinRadiusSq   = 1.005f * 1.005f;

    private SchematicObject ChaosBar;
    private Vector3 ChaosBarNormalPos;
    private Vector3 FBJoin;
    private SchematicObject FBDoor;
    private static bool FemurSetup;
    private SchematicObject FBButton;
    private static bool FemurBreaked;
    private Vector3 FBCP;
    private Vector3 OWB;
    public static Vector3 OWJoin;
    public static Vector3 STS;
    public static Vector3 STC;
    public static Vector3 STE;
    public static SchematicObject Scp012_t;

    // ドア位置 → DoorConfig のマッピング
    private readonly Dictionary<Vector3, DoorConfig> specialDoors = new();

    private readonly List<Player> femuredPlayers = [];
    private CoroutineHandle femurCoroutine;
    private CoroutineHandle trainCoroutine;

    public static Vector3 PDExJoin;
    public static Vector3 PDExJoinKing;
    public static bool _femurSetup   => FemurSetup;
    public static bool _femurBreaked => FemurBreaked;

    private readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    // ──────────────────────────────────────────────
    //  DoorConfig
    //  RequiredCItemType: null なら CItem チェックなし
    //  RequiredCode     : null なら passcode チェックなし
    //  両方 null なら常時ロック（管理者専用扱い）
    // ──────────────────────────────────────────────
    private class DoorConfig
    {
        /// <summary>
        /// 所持必須の CItem の具象型。
        /// null の場合はアイテムチェックをスキップする。
        /// </summary>
        public Type? RequiredCItemType { get; set; }

        /// <summary>プレイヤーが設定している RP パスコード。null ならスキップ。</summary>
        public string? RequiredCode { get; set; }

        /// <summary>条件未達時にプレイヤーへ送るヒント文。</summary>
        public string HintMessage { get; set; } = "専用のアクセスパスが必要そうだ・・・";

        // ── ヘルパー ──

        /// <summary>
        /// プレイヤーがこのドアを通過できるかを判定する。
        /// RequiredCItemType / RequiredCode の両方が設定されている場合は OR 判定。
        /// </summary>
        public bool CanOpen(Player player)
        {
            bool hasItem = CheckItem(player);
            bool hasCode = CheckCode(player);

            // 条件が何も設定されていない場合は通さない（設定漏れ防止）
            if (RequiredCItemType == null && RequiredCode == null)
                return false;

            // どちらか一方だけ設定 → その条件のみ評価
            // 両方設定 → OR
            if (RequiredCItemType != null && RequiredCode == null) return hasItem;
            if (RequiredCItemType == null && RequiredCode != null) return hasCode;
            return hasItem || hasCode;
        }

        // ── 内部チェック ──

        private bool CheckItem(Player player)
        {
            if (RequiredCItemType == null) return false;

            // CItem.GetAllInstances() から型一致のインスタンスを探し、
            // プレイヤーのインベントリにそのアイテムがあるか確認する。
            foreach (var ci in CItem.GetAllInstances())
            {
                if (ci.GetType() != RequiredCItemType) continue;
                if (ci.HasIn(player)) return true;
            }
            return false;
        }

        private bool CheckCode(Player player)
        {
            if (RequiredCode == null) return false;
            return RPNameSetter.TryGetPasscode(player, out var code) && code == RequiredCode;
        }
    }

    // ──────────────────────────────────────────────────────
    //  コンストラクタ / デストラクタ
    // ──────────────────────────────────────────────────────

    public CustomMapMainHandler()
    {
        MapHandler.Generated                             += OnGeneratorGenerating;
        ServerHandler.RoundStarted                       += OnRoundStarted;
        ServerHandler.RestartingRound                    += ResetInRestart;
        MapHandler.SpawningTeamVehicle                   += ChaosAnimation;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy  += InteractionButton;
        PlayerHandler.InteractingDoor                    += DoorInteracted;
    }

    ~CustomMapMainHandler()
    {
        MapHandler.Generated                               -= OnGeneratorGenerating;
        ServerHandler.RoundStarted                         -= OnRoundStarted;
        ServerHandler.RestartingRound                      -= ResetInRestart;
        MapHandler.SpawningTeamVehicle                     -= ChaosAnimation;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy    -= InteractionButton;
        PlayerHandler.InteractingDoor                      -= DoorInteracted;

        if (femurCoroutine.IsRunning) Timing.KillCoroutines(femurCoroutine);
        if (trainCoroutine.IsRunning) Timing.KillCoroutines(trainCoroutine);
    }

    // ──────────────────────────────────────────────────────
    //  ラウンドライフサイクル
    // ──────────────────────────────────────────────────────

    private void OnGeneratorGenerating()
    {
        foreach (var generator in Generator.List
                     .Where(g => g.Room.Type == RoomType.HczServerRoom))
        {
            generator.IsEngaged = true;
        }
    }

    private void OnRoundStarted()
    {
        WarheadBoomEffectUtil.StopAllEffects();

        if (femurCoroutine.IsRunning) Timing.KillCoroutines(femurCoroutine);
        if (trainCoroutine.IsRunning) Timing.KillCoroutines(trainCoroutine);
        
        SetDoorState();
        SetupMaps();
        HolidaySeasonMapLoader();
        SetCandyState();
        Timing.CallDelayed(1.5f, SetupSpecialDoors);
    }

    private static void SetCandyState()
    {
        Timing.CallDelayed(3f, () =>
        {
            if (!CandyChanges.CandyChances.ContainsKey("Default"))
                CandyChanges.Init();

            if (MapFlags.GetSeason() == SeasonTypeId.April)
            {
                CandyChanges.CandyChances.TryGetValue("Default", out var result);
                result.MostRareChance  = 0.22f;
                result.RareCandiesChance = 0.5f;
                CandyChanges.TryAddDictionary("April", result);
                CandyChanges.TrySetActiveDictionary("April", out _);
                return;
            }

            CandyChanges.TrySetActiveDictionary("Default", out _);
        });
    }

    // ──────────────────────────────────────────────────────
    //  SpecialDoors セットアップ
    //  ★ RequiredCItemType に CItem 派生クラスの Type を渡す
    //    例: RequiredCItemType = typeof(MyAccessPassItem)
    // ──────────────────────────────────────────────────────

    private void SetupSpecialDoors()
    {
        specialDoors.Clear();

        // OWJoin: CItem 継承クラスで管理するアクセスパス
        if (OWJoin != default)
        {
            specialDoors[OWJoin] = new DoorConfig
            {
                RequiredCItemType = typeof(OmegaWarheadAccess), // ← 実際の CItem 派生クラス名に変更
                HintMessage       = "専用のアクセスパスが必要そうだ・・・"
            };
        }

        // パスコード専用ドア（アイテム不要）
        specialDoors[new Vector3(-18.614f, 257.005f, -91.739f)] = new DoorConfig
        {
            RequiredCode = "55555",
            HintMessage  = "コードが正しくないようだ・・・"
        };

        specialDoors[MapFlags.SqDoorPoint] = new DoorConfig
        {
            RequiredCode = "0727",
            HintMessage  = "コードが正しくないようだ・・・"
        };
    }

    // ──────────────────────────────────────────────────────
    //  ドア状態の初期化
    // ──────────────────────────────────────────────────────

    public void SetDoorState()
    {
        foreach (var door in Door.List)
        {
            if (door is null) continue;

            switch (door.Type)
            {
                case DoorType.SurfaceGate:
                    door.RequireAllPermissions = true;
                    door.RequiredPermissions   = DoorPermissionFlags.ExitGates;
                    break;

                case DoorType.EscapeFinal:
                    door.Unlock();
                    break;

                default:
                    // specialDoors に登録されている位置と一致するドアをロック
                    if (IsSpecialDoor(door.Position))
                        door.Lock(DoorLockType.AdminCommand);
                    break;
            }
        }
    }

    /// <summary>指定座標が specialDoors のいずれかと一致するか。</summary>
    private bool IsSpecialDoor(Vector3 pos)
    {
        foreach (var key in specialDoors.Keys)
            if (Vector3.SqrMagnitude(pos - key) <= PositionToleranceSq)
                return true;
        return false;
    }

    /// <summary>指定座標に最も近い DoorConfig を返す。見つからなければ null。</summary>
    private DoorConfig? FindDoorConfig(Vector3 pos)
    {
        DoorConfig? best  = null;
        float       minSq = float.MaxValue;

        foreach (var kvp in specialDoors)
        {
            float sq = Vector3.SqrMagnitude(pos - kvp.Key);
            if (sq <= PositionToleranceSq && sq < minSq)
            {
                minSq = sq;
                best  = kvp.Value;
            }
        }

        return best;
    }

    // ──────────────────────────────────────────────────────
    //  マップセットアップ
    // ──────────────────────────────────────────────────────

    private void SetupMaps()
    {
        WarheadBoomEffectUtil.StopAllEffects();
        OmegaWarhead.Reset();

        if (femurCoroutine.IsRunning) Timing.KillCoroutines(femurCoroutine);

        ObjectPrefabLoader.LoadMap("aaa");

        Timing.CallDelayed(2.0f, () =>
        {
            GetSchematicsAndTriggerPoints();

            if (FBJoin != default && FBCP != default)
                femurCoroutine = Timing.RunCoroutine(FemurBreaker());

            if (STS != default && STC != default && STE != default)
            {
                Timing.CallDelayed(25f, () =>
                {
                    if (!Round.InProgress) return;
                    trainCoroutine = Timing.RunCoroutine(TrainComing.SpawnTrainAndAnim(STS, STC, STE));
                });
            }
            else
            {
                Log.Error("Train Points not successfully spawned.");
            }

            if (MapFlags.AntiAntiMemeDocPoint != default)
            {
                var doc = new Document().Create() as Document;
                doc?.DocumentType = DocumentType.AntiAntiMeme;
                doc?.Position     = MapFlags.AntiAntiMemeDocPoint;
                doc?.ShowModel    = false;
            }
        });
    }

    public void GetSchematicsAndTriggerPoints()
    {
        FemurSetup    = false;
        FemurBreaked  = false;
        femuredPlayers.Clear();

        foreach (var map in MapUtils.LoadedMaps.Values)
        {
            if (map.SpawnedObjects == null) continue;
            foreach (var meo in map.SpawnedObjects)
            {
                if (!meo.TryGetComponent(out SchematicObject schematic)) continue;

                switch (schematic.Name)
                {
                    case "Surface_CarStopper_Bar":
                        ChaosBar = schematic;
                        ChaosBarNormalPos = schematic.Position;
                        break;
                    case "FemurBreaker_Door":
                        FBDoor = schematic;
                        break;
                    case "FemurBreakerButton":
                        FBButton = schematic;
                        break;
                    case "Scp012_ThetaPrimed":
                        Scp012_t = schematic;
                        break;
                }
            }
        }

        foreach (var point in TriggerPointManager.GetAll())
        {
            if (point.Base is not SerializableCustomTriggerPoint trig || string.IsNullOrEmpty(trig.Tag))
                continue;

            var pos = TriggerPointManager.GetWorldPosition(point);
            switch (trig.Tag)
            {
                case "FemurBreaker_JoinPoint":    FBJoin    = pos; break;
                case "FemurBreaker_CapybaraPoint": FBCP      = pos; break;
                case "PDEX_JoinPoint":             PDExJoin  = pos; break;
                case "PDEX_JoinPointKing":         PDExJoinKing = pos; break;
                case "OWB":                        OWB       = pos; break;
                case "OWJoin":
                    OWJoin = pos;
                    // OWJoin は SetupSpecialDoors より後に確定するため、ここで上書き登録。
                    specialDoors[OWJoin] = new DoorConfig
                    {
                        RequiredCItemType = typeof(OmegaWarheadAccess), // ← 実際の型に合わせる
                        HintMessage       = "専用のアクセスパスが必要そうだ・・・"
                    };
                    break;
                case "ST_S":                       STS       = pos; break;
                case "ST_C":                       STC       = pos; break;
                case "ST_E":                       STE       = pos; break;
                case "AntiAntiMemeDoc":            MapFlags.AntiAntiMemeDocPoint = pos; break;
                case "SQ_Door":                    MapFlags.SqDoorPoint          = pos; break;
                case "AntiMemeButton":             MapFlags.AntiMemeButton = pos; break;
            }
        }
    }

    private static void ResetInRestart()
    {
        WarheadBoomEffectUtil.StopAllEffects();
    }

    // ──────────────────────────────────────────────────────
    //  ChaosBar アニメーション
    // ──────────────────────────────────────────────────────

    public void ChaosAnimation(SpawningTeamVehicleEventArgs ev)
    {
        if (ev.Team.TargetFaction != Faction.FoundationEnemy || ChaosBar is null)
            return;

        Timing.CallDelayed(2.25f, () =>
        {
            Timing.RunCoroutine(PlayBarAnim(ChaosBar, 22f));
        });
    }

    private IEnumerator<float> PlayBarAnim(SchematicObject? schem, float waitTime)
    {
        if (schem is null || Round.IsLobby || Round.IsEnded) yield break;

        yield return Timing.WaitUntilDone(Anim(schem, ChaosBarNormalPos, new Vector3(0, 4f, 0), 0.8f));

        if (Round.IsLobby || Round.IsEnded || schem?.transform == null) yield break;

        yield return Timing.WaitForSeconds(waitTime);

        if (Round.IsLobby || Round.IsEnded || schem?.transform == null) yield break;

        yield return Timing.WaitUntilDone(Anim(schem, ChaosBarNormalPos + new Vector3(0f, 4f, 0f), new Vector3(0, -4f, 0), 1.5f));
    }

    private static IEnumerator<float> Anim(SchematicObject schem, Vector3 startPos, Vector3 offset, float duration)
    {
        if (schem?.transform == null || duration <= 0f) yield break;

        float elapsed = 0f;
        Vector3 endPos = startPos + offset;

        while (elapsed < duration)
        {
            if (Round.IsLobby || Round.IsEnded || schem?.transform == null) yield break;

            elapsed += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return 0f;
        }

        if (schem?.transform != null)
            schem.transform.position = endPos;
    }

    // ──────────────────────────────────────────────────────
    //  ボタン・インタラクション
    // ──────────────────────────────────────────────────────

    private void InteractionButton(PlayerSearchedToyEventArgs ev)
    {
        var pos = ev.Interactable.Position;

        // ChaosBar 手動ボタン
        if (ChaosBar != null &&
            Vector3.SqrMagnitude(pos - new Vector3(-17.25f, 291.60f, -36.89f)) <= PositionToleranceSq)
        {
            Timing.RunCoroutine(PlayBarAnim(ChaosBar, 3f));
        }

        // Femur Breaker ボタン
        if (FBButton != null &&
            Vector3.SqrMagnitude(pos - FBButton.Position) <= PositionToleranceSq)
        {
            HandleFemurButton(ev.Player);
        }

        // Omega Warhead ボタン
        if (OWB != default &&
            Vector3.SqrMagnitude(pos - OWB) <= PositionToleranceSq)
        {
            HandleOmegaWarheadButton(ev.Player);
        }
    }

    private void HandleFemurButton(LabApi.Features.Wrappers.Player labPlayer)
    {
        var player = Player.Get(labPlayer.NetworkId);

        if (FemurSetup && !FemurBreaked)
        {
            FemurBreaked = true;

            foreach (var fp in femuredPlayers.ToList())
                if (fp?.IsConnected == true)
                    fp.Kill("Femur Breakerの犠牲となった");

            var scp106s = Player.List
                .Where(p => p?.IsConnected == true &&
                            (p.GetCustomRole() == CRoleTypeId.Scp106 ||
                             (p.GetCustomRole() == CRoleTypeId.None && p.Role.Type == RoleTypeId.Scp106)))
                .ToList();

            foreach (var scp in scp106s)
            {
                var captured = scp;
                Timing.CallDelayed(28f, () =>
                {
                    if (captured?.IsConnected == true && FemurSetup && FemurBreaked)
                        captured.Kill("Femur Breakerによって再収容された");
                });
            }

            CreateAndPlayAudio("FemurBreaker.ogg", "FemurBreaker", Vector3.zero, true, null, false, 999999999, 0);

            Timing.CallDelayed(28f, () =>
            {
                if (!FemurSetup || !FemurBreaked) return;

                bool stillAlive = Player.List.Any(p =>
                    p?.IsConnected == true &&
                    (p.GetCustomRole() == CRoleTypeId.Scp106 ||
                     (p.GetCustomRole() == CRoleTypeId.None && p.Role.Type == RoleTypeId.Scp106)));

                if (stillAlive)
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "SCP 1 0 6 recontained successfully by femur breaker",
                        "<color=red>SCP-106</color>のFEMUR BREAKERによる再収容に成功しました。");
                }
                else
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "Femur Breaker Process Successfully Completed. but no effect for containment breach.",
                        "FEMUR BREAKERプロセスが正常に完了しましたが、収容違反への影響が確認されませんでした。");
                }
            });
        }
        else
        {
            player?.ShowHint("準備が完了していないか、既に実行されています。");
        }
    }

    private static void HandleOmegaWarheadButton(LabApi.Features.Wrappers.Player labPlayer)
    {
        if (!SpecialEventsHandler.IsWarheadable() || OmegaWarhead.IsWarheadStarted)
        {
            labPlayer.SendHint("何らかの要因で実行できませんでした");
            return;
        }

        var player = Player.Get(labPlayer.NetworkId);
        OmegaWarhead.StartProtocol(0f, startedBy: player);
    }

    // ──────────────────────────────────────────────────────
    //  ドア操作イベント
    //  ★ CItem.HasIn で CItem 型チェックを行う
    // ──────────────────────────────────────────────────────

    private void DoorInteracted(InteractingDoorEventArgs ev)
    {
        if (ev.Player == null || ev.Door == null || specialDoors.Count == 0)
            return;

        var config = FindDoorConfig(ev.Door.Position);
        if (config == null)
            return;

        if (config.CanOpen(ev.Player))
        {
            ev.IsAllowed = true;
            return;
        }

        ev.IsAllowed = false;
        ev.Player.ShowHint(config.HintMessage);
    }

    // ──────────────────────────────────────────────────────
    //  Femur Breaker コルーチン
    // ──────────────────────────────────────────────────────

    private IEnumerator<float> FemurBreaker()
    {
        while (true)
        {
            if (!Round.InProgress)
            {
                femuredPlayers.Clear();
                FemurSetup   = false;
                FemurBreaked = false;
                yield break;
            }

            var target = Player.List.FirstOrDefault(p =>
                p.IsConnected &&
                p.GetTeam() != CTeam.SCPs &&
                Vector3.SqrMagnitude(p.Position - FBJoin) <= FemurJoinRadiusSq);

            if (target != null)
            {
                target.Handcuff();
                target.Position = FBCP;
                femuredPlayers.Add(target);
                FemurSetup = true;

                if (FBDoor != null)
                    Timing.RunCoroutine(Anim(FBDoor, FBDoor.Position, new Vector3(0f, -2.5f, 0f), 0.65f));

                yield break;
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    // ──────────────────────────────────────────────────────
    //  シーズンマップ
    // ──────────────────────────────────────────────────────

    public void HolidaySeasonMapLoader()
    {
        switch (MapFlags.GetSeason())
        {
            case SeasonTypeId.Halloween: MapUtils.LoadMap("Holiday_HalloweenMap"); break;
            case SeasonTypeId.Christmas: MapUtils.LoadMap("Holiday_ChristmasMap"); break;
        }
    }
}
