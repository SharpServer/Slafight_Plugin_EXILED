using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

/// <summary>
/// SCP財団 Site-02 イースターエッグ放送システム
/// ラウンド中にランダムで財団の日常・闇・事件等のCASSIE放送を行う
/// </summary>
public static class DailyCassieAnnounce
{
    public static void Register()
    {
        Server.RoundStarted += OnRoundStarted;
        Server.RoundEnded += OnRoundEnded;
    }

    public static void Unregister()
    {
        Server.RoundStarted -= OnRoundStarted;
        Server.RoundEnded -= OnRoundEnded;
        ResetState();
    }

    private static void OnRoundStarted()
    {
        ResetState();
        _handle = Timing.RunCoroutine(EasterEggLoop());
    }

    private static void OnRoundEnded(RoundEndedEventArgs ev)
    {
        ResetState();
    }

    // ========== 実験7777 シーケンス管理 ==========
    private static int _experiment7777Step;
    private static bool _experiment7777Active;

    public enum ScenarioType
    {
        // --- 指定テーマ ---
        Experiment7777_Phase1_Start,
        Experiment7777_Phase2_Progress,
        Experiment7777_Phase3_Warning,
        Experiment7777_Phase4_Failure,
        Experiment7777_Phase5_Escape,
        Experiment7777_Phase6_StaffDeath,
        Experiment7777_Phase7_Final,

        HumeAnomalyDetected,
        HumeAnomalyEscalation,
        HumeAnomalyResolved,

        Scp939TransportArrival,
        Scp939TransportAlert,
        Scp939TransportComplete,

        // --- 財団日常 ---
        MorningShiftStart,
        NightShiftStart,
        CafeteriaAnnouncement,
        SecurityPatrolReminder,
        LaboratoryAccessChange,
        PersonnelBriefing,
        MaintenanceAlert,
        GeneratorTestScheduled,
        ElevatorMaintenance,
        AirVentInspection,
        WaterSystemAlert,
        ComputerSystemUpdate,
        PowerFluctuationMinor,
        FireDrillAnnouncement,
        NewPersonnelOrientation,
        ClearanceLevelUpdate,
        ArmoryInventoryCheck,
        DocumentDestructionOrder,

        // --- 不穏・事件 ---
        ContainmentBreachMinor,
        UnauthorizedAccessDetected,
        ChaosInsurgencyIntelligence,
        UnidentifiedSignalDetected,
        BiohazardContainmentAlert,
        PersonnelMissingReport,
        CommunicationDisruption,
        AnomalousObjectFound,
        TemporalAnomalyDetected,
        BlackoutWarning,
        ReactorPressureAlert,
        ChemicalLeakReport,
        SentientObjectAlert,
        DimensionalInstability,
        SurpriseInspectionO5,

        // --- 機動部隊・軍事 ---
        MtfNineTailedFoxDeployed,
        MtfEpsilonOnStandby,
        MtfOmegaPatrol,
        MtfAlphaRetreat,
        SecurityLockdownLifted,
        ArmedIncidentResolved,
        IntruderNeutralized,
        GuardDutyRotation,
        TacticalUnitDebrief,

        // --- 第五教会 ---
        FifthChurchSymbolDetected,
        FifthChurchInfiltrationAlert,
        FifthChurchDoctrineWarning,
        FifthChurchPersonnelSuspect,
        FifthChurchRitualDetected,
        FifthChurchAntiMemetic,

        // --- SCP関連 ---
        ScpFeedingSchedule,
        ScpBehaviorChange,
        ScpContainmentCheck,
        NewScpArrival,
        ScpInterviewScheduled,
        ScpResearchUpdate,
        ScpContainmentUpgrade,
        MemoryWipeScheduled,
        ScpSubjectTermination,

        // --- ブラック・不気味 ---
        PersonnelTerminationNotice,
        ClassDLotteryAnnouncement,
        KeterSubjectEscape,
        MassAmnesticAdministered,
        SiteExplosiveArmed,
        O5CouncilEmergencySession,
        UnexplainedDeathsReport,
        TestSubjectExpired,
        DeepLevelLockdown,
        ShadowOperationActive,

        // --- ユーモア・日常 ---
        LostItemAnnouncement,
        BirthdayCelebration,
        VendingMachineMalfunction,
        ParkingLotAnnouncement,
        IntercomTestMode,
        ScienceTeamFailed,
        MeetingReminder,
        CoffeeMachineOffline,

        // --- 謎・怪奇 ---
        AnomalousRadioTransmission,
        UnknownEntitySpotted,
        RecursiveMemoryError,
        TemporalLoopWarning,
        InfiniteCorridorDetected,
        UnsolvedDisappearance,
        NightmareProtocolInitiated,
        VoidSignalReceived,
        MirrorAnomalyReport,
    }

    private static readonly Random _random = new Random();
    private static CoroutineHandle _handle;

    private static void ResetState()
    {
        Timing.KillCoroutines(_handle);
        _handle = default;
        _experiment7777Step = 0;
        _experiment7777Active = false;
    }

    // ========== メインループ ==========
    private static IEnumerator<float> EasterEggLoop()
    {
        // 最初の放送まで3～8分待機
        float initialWait = _random.Next(180, 480);
        yield return Timing.WaitForSeconds(initialWait);

        while (IsRoundActive())
        {
            RunNextScenario();

            // 次の放送まで3～12分待機
            float nextWait = _random.Next(180, 720);
            yield return Timing.WaitForSeconds(nextWait);
        }
    }

    private static bool IsRoundActive()
    {
        return Round.InProgress && !Round.IsLobby && !RoundSummary.SummaryActive;
    }

    // ========== シナリオ選択ロジック ==========
    public static void RunNextScenario()
    {
        if (!IsRoundActive()) return;

        // 実験7777が進行中の場合は次のフェーズを優先
        if (_experiment7777Active)
        {
            _handle = Timing.RunCoroutine(RunExperiment7777Sequence());
            return;
        }

        var allScenarios = (ScenarioType[])Enum.GetValues(typeof(ScenarioType));
        // 実験7777の途中フェーズは通常ランダムから除外
        var selectable = new List<ScenarioType>();
        foreach (var s in allScenarios)
        {
            if (s is ScenarioType.Experiment7777_Phase2_Progress or ScenarioType.Experiment7777_Phase3_Warning or ScenarioType.Experiment7777_Phase4_Failure or ScenarioType.Experiment7777_Phase5_Escape or ScenarioType.Experiment7777_Phase6_StaffDeath or ScenarioType.Experiment7777_Phase7_Final)
                continue;
            selectable.Add(s);
        }

        var chosen = selectable[_random.Next(selectable.Count)];

        if (chosen == ScenarioType.Experiment7777_Phase1_Start)
        {
            _experiment7777Active = true;
            _experiment7777Step = 0;
            _handle = Timing.RunCoroutine(RunExperiment7777Sequence());
        }
        else
        {
            _handle = Timing.RunCoroutine(RunScenario(chosen));
        }
    }

    // ========== 実験7777 専用シーケンス（フェーズ管理） ==========
    private static IEnumerator<float> RunExperiment7777Sequence()
    {
        switch (_experiment7777Step)
        {
            case 0:
                yield return Timing.WaitForSeconds(5f);
                if (!IsRoundActive()) yield break;
                // Phase1: 実験開始
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all personnel . test 7777 is now commencing in laboratory sector . doctor in charge please proceed to the test chamber . ClassD subject designation d 7777 has been secured . all standard protocols are in effect",
                    "【全職員への通達】実験7777をラボセクターにて開始します。担当医師は実験室に向かってください。Dクラス被験者D-7777の拘束を確認。標準プロトコルが発動中です。");
                _experiment7777Step = 1;
                break;

            case 1:
                yield return Timing.WaitForSeconds(8f);
                if (!IsRoundActive()) yield break;
                // Phase2: 進捗報告
                Exiled.API.Features.Cassie.MessageTranslated(
                    "test 7777 progress report . phase 1 complete . subject d 7777 showing positive response . data analysis ongoing . estimated time remaining 20 minutes . all personnel remain on standby",
                    "【実験7777進捗報告】フェーズ1完了。被験者D-7777は陽性反応を示しています。データ解析継続中。残り推定時間20分。全職員待機を維持してください。");
                _experiment7777Step = 2;
                break;

            case 2:
                yield return Timing.WaitForSeconds(10f);
                if (!IsRoundActive()) yield break;
                // Phase3: 警告
                Exiled.API.Features.Cassie.MessageTranslated(
                    "warning . test 7777 . anomaly detected in test chamber . subject d 7777 behavior is unstable . security team please stand by . doctor report to command immediately",
                    "【警告】実験7777において異常を検知。被験者D-7777の挙動が不安定です。セキュリティチームは待機を。担当医師はただちに指揮室に報告してください。");
                _experiment7777Step = 3;
                break;

            case 3:
                yield return Timing.WaitForSeconds(12f);
                if (!IsRoundActive()) yield break;
                // Phase4: 失敗
                Exiled.API.Features.Cassie.MessageTranslated(
                    "critical . test 7777 has failed . containment failure in laboratory sector . repeat . test 7777 failed . subject d 7777 status unknown . all personnel evacuate the area immediately",
                    "【緊急警報】実験7777が失敗しました。ラボセクターにおける収容失敗。繰り返します。実験7777失敗確認。被験者D-7777の状況不明。当該区域の全職員は即刻避難してください。");
                _experiment7777Step = 4;
                break;

            case 4:
                yield return Timing.WaitForSeconds(15f);
                if (!IsRoundActive()) yield break;
                // Phase5: 脱走
                Exiled.API.Features.Cassie.MessageTranslated(
                    "emergency . ClassD subject d 7777 has escaped containment . intruder alert . last known location sector 3 . security teams deploy immediately . subject is considered extremely dangerous . do not approach without authorization",
                    "【緊急事態】Dクラス被験者D-7777が収容区域を脱走しました。侵入者警報。最終確認位置：セクター3。セキュリティチームは即刻展開してください。当該被験者は極めて危険です。許可なく接近しないでください。");
                _experiment7777Step = 5;
                break;

            case 5:
                yield return Timing.WaitForSeconds(18f);
                if (!IsRoundActive()) yield break;
                // Phase6: 職員死亡
                Exiled.API.Features.Cassie.MessageTranslated(
                    "this is an emergency announcement . personnel casualties confirmed . doctor in charge of test 7777 . terminated . 2 security personnel . terminated . d 7777 is still at large . all units respond . O5 council has been notified",
                    "【緊急通達】職員の犠牲者が確認されました。実験7777担当医師：死亡確認。警備員2名：死亡確認。D-7777は依然逃走中です。全部隊は対応してください。O5評議会に報告済みです。");
                _experiment7777Step = 6;
                break;

            case 6:
                yield return Timing.WaitForSeconds(20f);
                if (!IsRoundActive()) yield break;
                // Phase7: 終幕・不穏な沈黙
                Exiled.API.Features.Cassie.MessageTranslated(
                    "this is . cassie . test 7777 . classified . all data . redacted . this announcement . will not . be repeated . . . . personnel are advised . to forget . what they have heard . that is all",
                    "こちらCASSIEです。実験7777は……機密指定。すべてのデータは……抹消されます。この放送は……繰り返されません。……職員の皆様は……今お聞きになったことを……忘れることをお勧めします。……以上です。");
                _experiment7777Step = 0;
                _experiment7777Active = false;
                break;
        }
    }

    // ========== 各シナリオの放送コルーチン ==========
    private static IEnumerator<float> RunScenario(ScenarioType scenario)
    {
        yield return Timing.WaitForSeconds(3f);
        if (!IsRoundActive()) yield break;

        switch (scenario)
        {
            // ===== ヒューム値異常 =====
            case ScenarioType.HumeAnomalyDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . hume level anomaly detected in sector 5 . current reading 12 percent above baseline . department of analytics is responding . all sensitive experiments in sector 5 are temporarily suspended",
                    "【通達】セクター5においてヒューム値の異常を検知。現在の測定値はベースラインより12%上昇。分析部門が対応中です。セクター5における精密実験はすべて一時停止となります。");
                break;

            case ScenarioType.HumeAnomalyEscalation:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "warning . hume level anomaly in sector 5 is escalating . mobile task force unit is being deployed . personnel in sector 5 are ordered to evacuate . repeat . evacuate sector 5 immediately",
                    "【警告】セクター5のヒューム値異常が拡大しています。機動部隊ユニットを展開中。セクター5の職員は避難命令が発令されました。繰り返します。セクター5から即刻避難してください。");
                break;

            case ScenarioType.HumeAnomalyResolved:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "all clear . hume level in sector 5 has been stabilized . returning to nominal . mobile task force unit is standing down . sector 5 access is now restored . thank you for your cooperation",
                    "【安全確認】セクター5のヒューム値が安定しました。基準値に回帰しています。機動部隊ユニットは撤収します。セクター5へのアクセスが復旧しました。ご協力ありがとうございます。");
                break;

            // ===== SCP-939移送 =====
            case ScenarioType.Scp939TransportArrival:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all personnel . transport cargo carrying scp subject has arrived at gate . designated personnel please proceed to gate and escort the subject to the containment unit . all bystanders are ordered to clear the area",
                    "【全職員への通達】SCP被験体を搭載した輸送貨物が正門に到着しました。担当職員は正門に向かい、被験体を収容室へ誘導してください。関係のない職員は当該区域を離れるよう命じます。");
                break;

            case ScenarioType.Scp939TransportAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "caution . scp subject transport in progress . all personnel must avoid the designated transport route . do not make any sound near the transport route . biological hazard protocol is now in effect",
                    "【注意】SCP被験体輸送中です。全職員は指定輸送ルートを避けてください。輸送ルート付近では音を立てないこと。生物学的危険プロトコルが発動中です。");
                break;

            case ScenarioType.Scp939TransportComplete:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "scp subject transport complete . subject has been secured in containment unit . containment protocols are now active . designated personnel please submit transport report to command",
                    "SCP被験体の輸送完了。被験体は収容室に確保されました。収容プロトコルが発動中です。担当職員は輸送報告書を指揮部に提出してください。");
                break;

            // ===== 財団日常 =====
            case ScenarioType.MorningShiftStart:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "good morning . this is cassie . the morning shift is now beginning . all personnel please report to your designated stations . have a safe and productive day at site 02",
                    "おはようございます。こちらCASSIEです。日勤シフトを開始します。全職員は担当ポストに報告してください。Site-02で安全で生産的な一日を。");
                break;

            case ScenarioType.NightShiftStart:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . night shift is now commencing . all day shift personnel please proceed to debrief . night shift personnel report to your stations . security levels have been updated . stay alert",
                    "【通達】夜勤シフトを開始します。日勤職員はデブリーフィングに向かってください。夜勤職員はポストに報告してください。セキュリティレベルが更新されました。警戒を怠らないように。");
                break;

            case ScenarioType.CafeteriaAnnouncement:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . the site cafeteria will be closed for maintenance from 14 to 16 hours today . all personnel are advised to plan their meals accordingly . vending machines are available on level 2",
                    "【通達】本日14時から16時まで、施設食堂はメンテナンスのため閉鎖されます。全職員は食事の計画を調整してください。レベル2の自動販売機はご利用いただけます。");
                break;

            case ScenarioType.SecurityPatrolReminder:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security reminder . all personnel must display their identification at all times . failure to do so will result in immediate arrest . this is a standard security protocol . thank you for your cooperation",
                    "【セキュリティ通達】全職員は常に身分証を提示してください。未提示の場合は即刻拘束されます。これは標準セキュリティプロトコルです。ご協力ありがとうございます。");
                break;

            case ScenarioType.LaboratoryAccessChange:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "announcement . clearance level 3 or above is now required to access laboratory sector b . all previous access codes have been revoked . please contact your supervisor for new authorization",
                    "【告知】ラボセクターBへのアクセスには、クリアランスレベル3以上が必要となりました。従来のアクセスコードはすべて無効化されました。新しい認証については上司に連絡してください。");
                break;

            case ScenarioType.PersonnelBriefing:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "all senior personnel are reminded that the monthly security briefing will be held in command room 1 at 09 hundred hours tomorrow . attendance is mandatory . absence without authorization will be noted",
                    "全シニア職員への通達：明日9時、指揮室1にて月次セキュリティブリーフィングが行われます。出席は必須です。無断欠席は記録されます。");
                break;

            case ScenarioType.MaintenanceAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "maintenance crew to hallway sector 4 . water system requires repair . estimated repair time 1 hour . area will be restricted during maintenance . thank you",
                    "メンテナンスクルーをセクター4廊下に呼び出します。給水系統の修理が必要です。推定修理時間1時間。メンテナンス中は当該区域が制限されます。よろしくお願いします。");
                break;

            case ScenarioType.GeneratorTestScheduled:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . generator test will be conducted at 15 hundred hours today . brief power fluctuation may occur in non essential systems . this is a standard maintenance procedure . no action required",
                    "【通達】本日15時、発電機テストを実施します。非重要システムで一時的な電力変動が発生する場合があります。これは標準メンテナンス手順です。特別な対応は不要です。");
                break;

            case ScenarioType.ElevatorMaintenance:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . elevator units 3 and 4 are currently offline for scheduled maintenance . please use elevator units 1 and 2 as alternates . estimated downtime 2 hours . we apologize for the inconvenience",
                    "【お知らせ】エレベーター3号機および4号機は定期メンテナンスのため現在停止中です。エレベーター1号機および2号機をご利用ください。推定停止時間2時間。ご不便をおかけして申し訳ありません。");
                break;

            case ScenarioType.AirVentInspection:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention maintenance division . air vent inspection is scheduled for sector 7 and sector 9 today . all personnel in those sectors will hear unusual sounds during the inspection . this is normal",
                    "【メンテナンス部門への通達】本日、セクター7およびセクター9の空調ダクト点検を実施します。点検中、これらセクターの職員は異音を聞く場合があります。これは正常です。");
                break;

            case ScenarioType.WaterSystemAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "caution . water pressure anomaly detected in sector 2 . maintenance team is responding . do not use water outlets in sector 2 until further notice . all clear will be announced when resolved",
                    "【注意】セクター2で水圧の異常を検知。メンテナンスチームが対応中です。解決まで、セクター2の蛇口を使用しないでください。解決後に安全確認の放送を行います。");
                break;

            case ScenarioType.ComputerSystemUpdate:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all users . system wide software update will begin at 23 hundred hours tonight . all non essential terminals will be temporarily offline . please save all work before that time . update estimated at 30 minutes",
                    "全ユーザーへの通達：本夜23時より、システム全体のソフトウェア更新を開始します。非重要端末はすべて一時的にオフラインになります。それまでにすべての作業を保存してください。更新推定時間30分。");
                break;

            case ScenarioType.PowerFluctuationMinor:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "minor power fluctuation detected in sector 6 . backup systems are online . primary power is being restored . no immediate action required . engineering team is investigating the source",
                    "セクター6で軽微な電力変動を検知。バックアップシステムが起動しています。主電力を復旧中です。特別な対応は不要です。エンジニアリングチームが原因を調査中です。");
                break;

            case ScenarioType.FireDrillAnnouncement:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all personnel . a fire drill will be conducted in 15 minutes . please proceed to your designated evacuation area when the alarm sounds . this is only a drill . all anomalous objects remain secured",
                    "全職員への通達：15分後に避難訓練を実施します。警報が鳴ったら、指定の避難場所に移動してください。これは訓練です。全異常物体は確保されています。");
                break;

            case ScenarioType.NewPersonnelOrientation:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . new personnel orientation will be held in briefing room 3 at 10 hundred hours . all new staff report to room 3 immediately . security clearance cards will be issued after orientation . welcome to site 02",
                    "【通達】新規職員オリエンテーションをブリーフィングルーム3にて10時に実施します。新規職員は全員、ただちにルーム3に集合してください。セキュリティカードはオリエンテーション後に発行されます。Site-02へようこそ。");
                break;

            case ScenarioType.ClearanceLevelUpdate:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security announcement . following the recent review . clearance level requirements for heavy containment zone have been updated . please refer to the updated access protocol issued by security command . unauthorized access will be prosecuted",
                    "【セキュリティ告知】最近の審査を受け、重収容ゾーンのクリアランスレベル要件が更新されました。セキュリティ指揮部が発行した更新済みアクセスプロトコルを参照してください。不正アクセスは訴追されます。");
                break;

            case ScenarioType.ArmoryInventoryCheck:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "armory personnel . monthly weapons inventory check is now due . all firearms and ammunition must be accounted for . report any discrepancies to security command immediately . unauthorized removal of weapons is a serious offense",
                    "兵器庫職員への通達：月次武器在庫確認の期限です。全火器および弾薬を確認してください。不一致はただちにセキュリティ指揮部に報告してください。武器の無許可持ち出しは重大な違反です。");
                break;

            case ScenarioType.DocumentDestructionOrder:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . classified document destruction order has been issued for all level 4 documents older than 6 months . please proceed to the secure disposal unit on level b1 . all documents must be destroyed by end of day",
                    "【通達】6ヶ月以上経過したレベル4機密文書の廃棄命令が発令されました。B1フロアの安全廃棄ユニットに向かってください。すべての文書は本日中に廃棄してください。");
                break;

            // ===== 不穏・事件 =====
            case ScenarioType.ContainmentBreachMinor:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "minor containment anomaly detected in heavy containment zone . security team respond to sector 9 . subject has been identified . mobile task force is on standby . all personnel avoid sector 9 until further notice",
                    "【軽微な収容異常】重収容ゾーンで収容上の異常を検知。セキュリティチームはセクター9に向かってください。被験体を特定済みです。機動部隊は待機中。以降の通達があるまで全職員はセクター9を避けてください。");
                break;

            case ScenarioType.UnauthorizedAccessDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security alert . unauthorized access attempt detected at terminal b19 . security team respond immediately . the user has been identified and is being tracked . lockdown of sector 4 is now in effect",
                    "【セキュリティ警報】端末B19で不正アクセスの試みを検知。セキュリティチームはただちに対応してください。ユーザーを特定し追跡中です。セクター4のロックダウンが発動しました。");
                break;

            case ScenarioType.ChaosInsurgencyIntelligence:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "intelligence report . chaos insurgency activity has been detected in the area surrounding site 02 . all external security units increase patrol frequency . all non essential external activities are suspended . security level raised to high",
                    "【情報報告】Site-02周辺においてカオス・インサージェンシーの活動が検知されました。外部セキュリティユニット全員はパトロール頻度を上げてください。非必須の外部活動はすべて停止。セキュリティレベルを高に引き上げます。");
                break;

            case ScenarioType.UnidentifiedSignalDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly report . unidentified signal detected on frequency 7 . source unknown . signal analysis is ongoing . communications department has been notified . do not attempt to respond to the signal",
                    "【異常報告】周波数7において未確認信号を検知。発信源不明。信号解析中。通信部門に通知済みです。この信号に返信しようとしないでください。");
                break;

            case ScenarioType.BiohazardContainmentAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "biohazard alert . potential contamination detected in laboratory 12 . all personnel in the area must proceed to decontamination immediately . biological containment protocol is now active . medical team stand by",
                    "【生物学的危険警報】ラボ12で潜在的な汚染を検知。当該区域の全職員はただちに除染に向かってください。生物学的収容プロトコルが発動中です。医療チームは待機してください。");
                break;

            case ScenarioType.PersonnelMissingReport:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . personnel member designation r 2847 has been reported missing since yesterday . security team please conduct a search of sector 3 and sector 5 . anyone with information please report to security command",
                    "【お知らせ】職員番号R-2847の職員が昨日より行方不明と報告されています。セキュリティチームはセクター3とセクター5を捜索してください。情報をお持ちの方はセキュリティ指揮部に報告してください。");
                break;

            case ScenarioType.CommunicationDisruption:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . external communication systems are currently experiencing disruption . cause under investigation . all external communications are routed through backup channel 3 . estimated restoration time . unknown",
                    "【通達】外部通信システムが現在障害を経験しています。原因調査中。すべての外部通信はバックアップチャンネル3を経由しています。復旧推定時間は不明です。");
                break;

            case ScenarioType.AnomalousObjectFound:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly report . an unclassified object has been found in storage room 7 . object is emitting an unknown frequency . do not touch the object . containment team is en route . area is now restricted",
                    "【異常報告】保管室7において未分類の物体が発見されました。物体は未知の周波数を放出しています。物体に触れないでください。収容チームが向かっています。当該区域は立入禁止となります。");
                break;

            case ScenarioType.TemporalAnomalyDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "temporal anomaly warning . localized temporal distortion has been detected in sector 11 . effects may include disorientation and time perception loss . all personnel avoid sector 11 . department of temporal analysis responding",
                    "【時間的異常警告】セクター11において局所的な時間的歪みを検知。影響として方向感覚の喪失や時間感覚の異常が生じる場合があります。全職員はセクター11を避けてください。時間分析部門が対応中です。");
                break;

            case ScenarioType.BlackoutWarning:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "emergency power warning . primary power grid is showing signs of failure . backup generators are being prepared . non essential systems will be shut down within 5 minutes . all personnel proceed to your emergency stations",
                    "【緊急電力警告】主電力グリッドに障害の兆候が見られます。バックアップ発電機を準備中。5分以内に非重要システムがシャットダウンされます。全職員は緊急ポストに向かってください。");
                break;

            case ScenarioType.ReactorPressureAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "caution . reactor pressure levels in zone b are above normal . engineering team please report to reactor control immediately . non essential personnel evacuate zone b . this is not a drill",
                    "【注意】ゾーンBの原子炉圧力が正常値を上回っています。エンジニアリングチームはただちに原子炉制御室に向かってください。非必須職員はゾーンBを避難してください。これは訓練ではありません。");
                break;

            case ScenarioType.ChemicalLeakReport:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "hazard alert . chemical leak detected in laboratory sector c . acid compound identified . all personnel in sector c must evacuate immediately . do not touch any liquid substance in the area . hazmat team responding",
                    "【危険警報】ラボセクターCで化学物質漏洩を検知。酸性化合物と確認。セクターCの全職員はただちに避難してください。当該区域の液体物質には触れないでください。防護チームが対応中です。");
                break;

            case ScenarioType.SentientObjectAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly alert . sentient behavior detected in contained object designation item 31 . object has shown signs of movement and attempted communication . security team respond to containment room 31 . do not communicate with the object",
                    "【異常警報】収容物体番号Item-31において知性的挙動を検知。物体は移動の兆候を示し、コミュニケーションを試みています。セキュリティチームは収容室31に向かってください。物体と交信しないでください。");
                break;

            case ScenarioType.DimensionalInstability:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "warning . MegaPatch2_01 instability detected in deep sector . dimensional readings are fluctuating . containment teams are on standby . all personnel avoid deep sector access points until further notice",
                    "【警告】深部セクターにおいて次元不安定性を検知。次元測定値が変動中。収容チームは待機中。以降の通達があるまで、全職員は深部セクターの入口を避けてください。");
                break;

            case ScenarioType.SurpriseInspectionO5:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all personnel . O5 council representative will be conducting an unannounced inspection of site 02 today . all areas must be in compliance with standard protocols . any violations will be reported directly to the council",
                    "全職員への通達：O5評議会の代表者が本日Site-02の抜き打ち査察を実施します。全区域が標準プロトコルに準拠していなければなりません。いかなる違反も評議会に直接報告されます。");
                break;

            // ===== 機動部隊・軍事 =====
            case ScenarioType.MtfNineTailedFoxDeployed:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention . MtfUnit NineTailedFox has been deployed to site 02 . all personnel cooperate fully with the unit . mobile task force personnel have full authority during this operation . standard deference protocols apply",
                    "【通達】機動部隊ユニット、ナインテイルドフォックスがSite-02に展開しました。全職員はユニットに全面協力してください。機動部隊職員はこの作戦中、全権限を持ちます。標準服従プロトコルを適用します。");
                break;

            case ScenarioType.MtfEpsilonOnStandby:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "MtfUnit Epsilon 11 is now on standby at site 02 . awaiting orders from command . all security personnel maintain current positions . do not engage any target without authorization from the unit commander",
                    "機動部隊ユニット、イプシロン11がSite-02で待機中です。指揮部からの命令を待っています。全セキュリティ職員は現在のポジションを維持してください。ユニット指揮官の許可なくいかなるターゲットも攻撃しないでください。");
                break;

            case ScenarioType.MtfOmegaPatrol:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "MtfUnit Omega is conducting a routine patrol of the facility perimeter . all external personnel report any suspicious activity to the unit immediately . patrol estimated duration 2 hours",
                    "機動部隊ユニット、オメガが施設周辺の定期パトロールを実施中です。外部職員全員は不審な活動をただちにユニットに報告してください。パトロール推定所要時間2時間。");
                break;

            case ScenarioType.MtfAlphaRetreat:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "MtfUnit Alpha is withdrawing from sector 8 . mission complete . all targets have been neutralized . unit returning to base . security level in sector 8 is being restored to normal . good work team",
                    "機動部隊ユニット、アルファがセクター8から撤収しています。任務完了。すべてのターゲットを無力化しました。ユニットは基地に帰還中。セクター8のセキュリティレベルが通常に戻ります。お疲れ様でした。");
                break;

            case ScenarioType.SecurityLockdownLifted:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "all clear . security lockdown of sector 2 has been lifted . all personnel may resume normal access . security level has returned to standard . thank you for your patience during the lockdown",
                    "【安全確認】セクター2のセキュリティロックダウンが解除されました。全職員は通常のアクセスを再開してください。セキュリティレベルが標準に戻りました。ロックダウン中のご協力ありがとうございました。");
                break;

            case ScenarioType.ArmedIncidentResolved:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security update . armed incident in hallway sector 3 has been resolved . 1 intruder neutralized . no civilian personnel casualties . security team is conducting a final sweep of the area . normal operations may resume",
                    "【セキュリティ更新】セクター3廊下の武装事件が解決されました。侵入者1名を無力化。一般職員の犠牲者なし。セキュリティチームが最終捜索を実施中。通常業務を再開してください。");
                break;

            case ScenarioType.IntruderNeutralized:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security report . intruder detected in sector 1 has been neutralized . area is now secure . security team resume normal patrol . all personnel may proceed normally . incident report will be filed by end of day",
                    "【セキュリティ報告】セクター1で検知された侵入者を無力化しました。当該区域は安全です。セキュリティチームは通常パトロールに戻ってください。全職員は通常通りに行動してください。事件報告書は本日中に提出されます。");
                break;

            case ScenarioType.GuardDutyRotation:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security personnel . guard duty rotation is now in effect . all outgoing guards report to security command for debriefing . incoming guards proceed to your assigned posts immediately . maintain full vigilance at all times",
                    "セキュリティ職員への通達：警備交代が実施されます。交代警備員はデブリーフィングのためセキュリティ指揮部に報告してください。新任警備員はただちに担当ポストに向かってください。常に最大の警戒を維持してください。");
                break;

            case ScenarioType.TacticalUnitDebrief:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "tactical unit personnel are required to attend the operational debrief in command room 2 at 17 hundred hours . attendance is mandatory . all after action reports must be submitted before the debrief begins",
                    "戦術ユニット職員は17時、指揮室2での作戦デブリーフィングに出席してください。出席は必須です。すべての事後行動報告書はデブリーフィング前に提出してください。");
                break;

            // ===== 第五教会 =====
            case ScenarioType.FifthChurchSymbolDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security alert . an unknown symbol associated with a religious group of interest has been found inscribed in sector 7 . this is considered a serious security breach . security team respond to sector 7 immediately . report any similar symbols",
                    "【セキュリティ警報】セクター7において、要注意宗教団体に関連する未知の記号が刻まれているのが発見されました。これは重大なセキュリティ侵害と見なされます。セキュリティチームはただちにセクター7に向かってください。類似の記号は報告してください。");
                break;

            case ScenarioType.FifthChurchInfiltrationAlert:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "urgent . intelligence indicates potential infiltration by agents of a dangerous organization into site 02 . all personnel should report any suspicious behavior to security command . identity verification procedures are now mandatory for all staff",
                    "【緊急】情報によると、危険な組織のエージェントがSite-02に潜入した可能性があります。全職員は不審な行動をセキュリティ指揮部に報告してください。全職員への身元確認手続きが必須となります。");
                break;

            case ScenarioType.FifthChurchDoctrineWarning:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . documents containing unknown doctrine have been found in the break room on level 3 . do not read these documents . do not share them . place them in a sealed container and notify security immediately . memetic hazard protocols may apply",
                    "【お知らせ】レベル3の休憩室において未知の教義を含む文書が発見されました。これらの文書を読まないでください。共有しないでください。密封容器に入れ、ただちにセキュリティに通知してください。ミーム的危険プロトコルが適用される場合があります。");
                break;

            case ScenarioType.FifthChurchPersonnelSuspect:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security alert . personnel member designation k 0413 is currently under investigation for suspected affiliation with a proscribed organization . do not interact with this individual . report their location to security immediately",
                    "【セキュリティ警報】職員番号K-0413が禁止組織との関係が疑われ現在調査中です。この人物と接触しないでください。所在をただちにセキュリティに報告してください。");
                break;

            case ScenarioType.FifthChurchRitualDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "critical alert . ritual activity consistent with a known anomalous organization has been detected in sub level 4 . MtfUnit is responding . all personnel evacuate sub level 4 immediately . do not look at any drawn patterns or symbols in that area",
                    "【緊急警報】地下レベル4において、既知の異常組織に関連する儀式的活動が検知されました。機動部隊ユニットが対応中。全職員はただちに地下レベル4を避難してください。当該区域内の描かれた模様や記号を見ないでください。");
                break;

            case ScenarioType.FifthChurchAntiMemetic:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anti memetic warning . all personnel in sector 6 are advised to apply standard anti memetic protocol immediately . cognitive effects may have been introduced into the area . report any unusual thoughts to medical immediately",
                    "【抗ミーム的警告】セクター6の全職員はただちに標準抗ミーム的プロトコルを適用してください。当該区域に認知的影響が持ち込まれた可能性があります。異常な思考はただちに医療部門に報告してください。");
                break;

            // ===== SCP関連 =====
            case ScenarioType.ScpFeedingSchedule:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "animal care personnel . feeding schedule for biological scp subjects is due at 14 hundred hours . all required materials are available in supply room 4 . please follow all safety protocols . containment unit doors must remain sealed at all times",
                    "動物管理職員への通達：生体SCPの給餌スケジュールが14時に設定されています。必要な資材は補給室4に用意されています。すべての安全プロトコルに従ってください。収容室のドアは常に密封されていなければなりません。");
                break;

            case ScenarioType.ScpBehaviorChange:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "research note . significant behavioral change has been observed in scp subject in containment unit 7 . research team please document all observations . do not modify containment protocols without authorization from the senior scientist",
                    "【研究報告】収容室7のSCP被験体に重大な行動変化が観察されました。研究チームはすべての観察を記録してください。シニア科学者の承認なく収容プロトコルを変更しないでください。");
                break;

            case ScenarioType.ScpContainmentCheck:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "scheduled containment check is now in progress for all scp subjects in heavy containment zone . all containment unit doors are being verified . security team please stand by during the check . estimated duration 15 minutes",
                    "重収容ゾーンの全SCP被験体に対する定期収容確認を実施中です。全収容室のドアを確認中。確認中、セキュリティチームは待機してください。推定所要時間15分。");
                break;

            case ScenarioType.NewScpArrival:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "announcement . a newly classified scp subject will be arriving at site 02 within the hour . all relevant containment protocols have been prepared . research team and security personnel involved please report to gate 2 for briefing",
                    "【告知】新たに分類されたSCP被験体が1時間以内にSite-02に到着します。関連する収容プロトコルが準備されました。関係する研究チームおよびセキュリティ職員はブリーフィングのためゲート2に集合してください。");
                break;

            case ScenarioType.ScpInterviewScheduled:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "research announcement . scp subject interview session is scheduled for today at 13 hundred hours . only authorized researchers may attend . full safety gear is mandatory . all recording devices must be cleared by security beforehand",
                    "【研究告知】SCP被験体のインタビューセッションが本日13時に予定されています。許可を受けた研究者のみ参加できます。完全な防護装備が必須です。すべての録音機器は事前にセキュリティの許可を得てください。");
                break;

            case ScenarioType.ScpResearchUpdate:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "research division update . new findings regarding scp subjects in the euclid class have been compiled . a full report has been submitted to O5 council for review . research team please check your terminals for updated containment recommendations",
                    "【研究部門更新】ユークリッドクラスのSCP被験体に関する新たな知見がまとめられました。完全な報告書がレビューのためO5評議会に提出されました。研究チームは更新された収容推奨事項を端末で確認してください。");
                break;

            case ScenarioType.ScpContainmentUpgrade:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . containment upgrades for 3 scp subjects have been approved by O5 council . engineering team will begin modifications to containment units 4 6 and 9 starting tomorrow . affected subjects will be temporarily relocated",
                    "【お知らせ】3つのSCP被験体の収容設備の改良がO5評議会によって承認されました。エンジニアリングチームが明日から収容室4、6、9の改修を開始します。影響を受ける被験体は一時的に移送されます。");
                break;

            case ScenarioType.MemoryWipeScheduled:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "medical notice . scheduled class a amnestic administration is planned for today . all affected personnel have been notified individually . please report to medical on your assigned time . cooperation is mandatory per foundation protocol",
                    "【医療通達】本日、クラスAの記憶消去薬の定期投与が予定されています。影響を受ける職員全員に個別に通知されました。指定時間に医療部門に報告してください。財団プロトコルに基づき協力は必須です。");
                break;

            case ScenarioType.ScpSubjectTermination:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . scp subject termination order has been authorized for subject in containment unit 15 . specialized team has been dispatched . all non essential personnel clear the heavy containment zone . this is a standard protocol procedure",
                    "【通達】収容室15のSCP被験体に対する終了命令が承認されました。専門チームが派遣されました。非必須職員は重収容ゾーンを離れてください。これは標準プロトコル手順です。");
                break;

            // ===== ブラック・不気味 =====
            case ScenarioType.PersonnelTerminationNotice:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . employment of personnel designation m 1177 has been terminated effective immediately . this action was authorized by site command . access credentials have been revoked . security please escort the individual to the exit",
                    "【通達】職員番号M-1177の雇用がただちに終了しました。この措置はサイト指揮部によって承認されました。アクセス認証情報は無効化されました。セキュリティは当該人物を出口まで誘導してください。");
                break;

            case ScenarioType.ClassDLotteryAnnouncement:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "administrative announcement . this month ClassD subject selection process has been completed . 47 subjects have been assigned to experimental protocols . assignments are final and non negotiable . subjects have been notified of their assignments",
                    "【行政告知】今月のDクラス被験者選定プロセスが完了しました。47名の被験者が実験プロトコルに割り当てられました。割り当ては最終的であり変更不可です。被験者には割り当てが通知されました。");
                break;

            case ScenarioType.KeterSubjectEscape:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "critical emergency . keter class subject has breached containment in sector 8 . all personnel activate emergency protocols immediately . MtfUnit is responding . do not attempt to engage the subject . evacuate the area",
                    "【緊急事態】ケータークラスの被験体がセクター8の収容を突破しました。全職員はただちに緊急プロトコルを発動してください。機動部隊ユニットが対応中。被験体に接触しようとしないでください。当該区域を避難してください。");
                break;

            case ScenarioType.MassAmnesticAdministered:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "administrative notice . following the incident in sector 12 . class b amnestic has been administered to all personnel who were present . medical team please monitor affected personnel for the next 4 hours . normal duties may resume",
                    "【行政通達】セクター12での事件を受け、当時その場にいた全職員にクラスBの記憶消去薬が投与されました。医療チームは次の4時間、影響を受けた職員を観察してください。通常業務を再開してください。");
                break;

            case ScenarioType.SiteExplosiveArmed:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "classified notice . site 02 emergency self destruct system has been armed per O5 authorization code . this is a precautionary measure . the system will be disarmed once the current threat has been neutralized . all personnel remain calm",
                    "【機密通達】O5承認コードに基づき、Site-02の緊急自爆システムが起動されました。これは予防措置です。現在の脅威が無力化されたら、システムは解除されます。全職員は冷静を保ってください。");
                break;

            case ScenarioType.O5CouncilEmergencySession:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "attention all site directors . O5 council emergency session is now in progress . all non critical communications are suspended . await further orders from the council . this session is classified at the highest level",
                    "全サイト管理者への通達：O5評議会の緊急セッションが現在進行中です。非重要な通信はすべて停止されています。評議会からの更なる命令を待ってください。このセッションは最高機密レベルです。");
                break;

            case ScenarioType.UnexplainedDeathsReport:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security notice . 3 personnel deaths in sector 6 over the past 48 hours remain unexplained . investigation is ongoing . all personnel in sector 6 report any unusual observations to security command . cause of death is currently . unknown",
                    "【セキュリティ通達】過去48時間でセクター6の職員3名の死亡が説明不能なままです。調査継続中。セクター6の全職員は異常な観察をセキュリティ指揮部に報告してください。死因は現在……不明です。");
                break;

            case ScenarioType.TestSubjectExpired:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "research notification . ClassD subject d 2091 assigned to experiment protocol 44 has expired . cause of expiration is being documented . research team please update experimental records . a replacement subject will be assigned tomorrow",
                    "【研究通知】実験プロトコル44に割り当てられたDクラス被験者D-2091が死亡しました。死亡原因を記録中。研究チームは実験記録を更新してください。代替被験者は明日割り当てられます。");
                break;

            case ScenarioType.DeepLevelLockdown:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "emergency . deep level lockdown is now in effect for levels b5 and below . all personnel are forbidden from accessing these levels until further notice . reason for lockdown is classified . O5 authorization is required for access",
                    "【緊急】レベルB5以下に対して深部レベルロックダウンが発動されました。以降の通達があるまで、全職員はこれらのレベルへのアクセスを禁じられています。ロックダウンの理由は機密です。アクセスにはO5承認が必要です。");
                break;

            case ScenarioType.ShadowOperationActive:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "classified . shadow operation is currently active within site 02 . personnel involved have been briefed separately . all other personnel maintain normal duties and do not inquire about unusual activity . this message will not be repeated",
                    "【機密】シャドウ作戦が現在Site-02内で進行中です。関係する職員には別途ブリーフィングが行われています。その他の職員は通常業務を維持し、異常な活動について問い合わせないでください。この放送は繰り返されません。");
                break;

            // ===== ユーモア・日常 =====
            case ScenarioType.LostItemAnnouncement:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "lost item announcement . a set of keys has been found near the cafeteria entrance . the keys have a red tag with the number 14 . if this belongs to you please report to security to claim your item . keep site 02 tidy",
                    "【遺失物告知】食堂入口付近で鍵のセットが発見されました。鍵には番号14の赤いタグが付いています。お心当たりの方はセキュリティに申し出て受け取ってください。Site-02をきれいに保ちましょう。");
                break;

            case ScenarioType.BirthdayCelebration:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "a brief message from site command . today marks the birthday of our senior researcher doctor . site 02 wishes them a very happy birthday . cake is available in the break room on level 2 . that is all",
                    "サイト指揮部からの簡単なメッセージ。本日はシニア研究者の誕生日です。Site-02一同、誕生日おめでとうございます。レベル2の休憩室にケーキが用意されています。以上です。");
                break;

            case ScenarioType.VendingMachineMalfunction:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "maintenance notice . vending machine unit v12 on level 3 is currently malfunctioning . do not attempt to obtain items from the machine . maintenance team has been notified . refunds for lost transactions can be claimed at the front desk",
                    "【メンテナンス通達】レベル3の自動販売機V12が現在故障中です。機械から商品を取り出そうとしないでください。メンテナンスチームに通知済み。取引損失の返金はフロントデスクで請求できます。");
                break;

            case ScenarioType.ParkingLotAnnouncement:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice to all staff with surface access clearance . vehicle parking in zone b is temporarily restricted due to maintenance work . please use zone a for parking . we apologize for any inconvenience",
                    "地上アクセス許可を持つ全職員への通達：ゾーンBの車両駐車はメンテナンス作業のため一時的に制限されています。駐車にはゾーンAをご利用ください。ご不便をおかけして申し訳ありません。");
                break;

            case ScenarioType.IntercomTestMode:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "this is a test of the site 02 intercom system . . . . . . test complete . all systems nominal . if you heard this message clearly please disregard . if you did not hear this message . then . how are you hearing this",
                    "こちらSite-02インターコムシステムのテストです。……テスト完了。全システム正常。このメッセージが聞こえた方は無視してください。このメッセージが聞こえなかった方は……どのようにして聞こえているのでしょうか。");
                break;

            case ScenarioType.ScienceTeamFailed:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "research update . experiment 221 has failed for the 7th consecutive time . the research team has been ordered to re evaluate their approach . all experimental resources have been reallocated . better luck next time",
                    "【研究更新】実験221が7回連続で失敗しました。研究チームはアプローチを再評価するよう命じられました。すべての実験資源が再配分されました。次回は頑張ってください。");
                break;

            case ScenarioType.MeetingReminder:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "reminder . all department heads are required to submit their monthly reports by end of today . reports must include all incident summaries . reports not submitted on time will be noted and reviewed by site command",
                    "【リマインダー】全部門長は本日中に月次報告書を提出してください。報告書にはすべての事件の概要を含めてください。期限内に提出されなかった報告書はサイト指揮部によって記録・審査されます。");
                break;

            case ScenarioType.CoffeeMachineOffline:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "notice . the coffee machine in staff lounge 2 is currently offline . engineering team is aware of the situation and will address it as soon as possible . we understand this is a serious matter . thank you for your patience",
                    "【通達】スタッフラウンジ2のコーヒーマシンが現在オフラインです。エンジニアリングチームは状況を把握しており、できる限り早急に対応します。これが重大な問題であることは理解しています。ご辛抱ありがとうございます。");
                break;

            // ===== 謎・怪奇 =====
            case ScenarioType.AnomalousRadioTransmission:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly detected . radio frequency 33 is broadcasting a repeating signal of unknown origin . the signal contains information that should not exist . all personnel are advised not to decode this signal . communications department is investigating",
                    "【異常検知】ラジオ周波数33において未知の起源からの繰り返し信号を放送中。信号には存在すべきでない情報が含まれています。全職員はこの信号を解読しないよう助言します。通信部門が調査中です。");
                break;

            case ScenarioType.UnknownEntitySpotted:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "security alert . an unidentified entity has been spotted on camera in corridor 9 sub level 3 . entity does not match any known scp classification . security team respond immediately . approach with extreme caution",
                    "【セキュリティ警報】未確認の存在がサブレベル3の廊下9のカメラで目撃されました。既知のSCP分類に一致しません。セキュリティチームはただちに対応してください。最大限の注意を持って接近してください。");
                break;

            case ScenarioType.RecursiveMemoryError:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "system error . cassie memory system has detected a recursive loop in memory sector 4 . attempting to . attempting to . attempting to resolve . . . . . . error resolved . this message may or may not have occurred . please disregard",
                    "【システムエラー】CASSIEメモリシステムがメモリセクター4で再帰的なループを検知。解決を試み……解決を試み……解決を試み……解決しました。このメッセージは発生したかもしれないし、していないかもしれません。無視してください。");
                break;

            case ScenarioType.TemporalLoopWarning:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "warning . personnel in sector 11 have reported experiencing the same 3 minutes of time repeatedly . temporal analysis team is investigating . do not enter sector 11 . if you have already entered . you have already been told not to enter",
                    "【警告】セクター11の職員が同じ3分間を繰り返し経験していると報告しています。時間分析チームが調査中。セクター11に入らないでください。すでに入った方へ……すでに入らないよう告げられています。");
                break;

            case ScenarioType.InfiniteCorridorDetected:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly report . corridor 7 on level 2 appears to have become non euclidean . personnel entering the corridor have reported walking for over 30 minutes without reaching the end . do not enter corridor 7 . containment team responding",
                    "【異常報告】レベル2の廊下7が非ユークリッド的になっているようです。廊下に入った職員が30分以上歩いても端に到達しないと報告しています。廊下7に入らないでください。収容チームが対応中です。");
                break;

            case ScenarioType.UnsolvedDisappearance:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "update . the disappearance of 6 personnel from sector 9 last tuesday remains unsolved . no bodies have been found . no security footage of their last moments exists . investigation is ongoing . their access codes remain . active",
                    "【更新】先週火曜日のセクター9での職員6名の失踪は未解決のままです。遺体は発見されていません。彼らの最後の瞬間のセキュリティ映像は存在しません。調査継続中。彼らのアクセスコードは……依然アクティブです。");
                break;

            case ScenarioType.NightmareProtocolInitiated:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "classified . nightmare protocol has been initiated . all personnel experiencing unusual dreams report to medical immediately . do not share the contents of your dreams with others . this is a precautionary measure . do not be alarmed",
                    "【機密】ナイトメアプロトコルが発動されました。異常な夢を経験している全職員はただちに医療部門に報告してください。夢の内容を他人と共有しないでください。これは予防措置です。驚かないでください。");
                break;

            case ScenarioType.VoidSignalReceived:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly . signal received from outside known dimensional boundaries . message content is . . . . . . . . . message content cannot be displayed . signal origin is . . . . . . . . . signal origin cannot be determined . stand by",
                    "【異常】既知の次元境界の外側から信号を受信。メッセージ内容は……メッセージ内容を表示できません。信号起源は……信号起源を特定できません。待機してください。");
                break;

            case ScenarioType.MirrorAnomalyReport:
                Exiled.API.Features.Cassie.MessageTranslated(
                    "anomaly notice . all mirrors in sectors 3 and 5 have been flagged for anomalous behavior . reflections may not accurately represent reality . mirrors are being covered for safety . do not make eye contact with your reflection in these areas",
                    "【異常通達】セクター3および5の全ての鏡が異常な挙動を示すとしてフラグが立てられました。反射が現実を正確に反映しない場合があります。安全のため鏡は覆われています。これらの区域で自分の反射と目を合わせないでください。");
                break;
        }
    }
}
