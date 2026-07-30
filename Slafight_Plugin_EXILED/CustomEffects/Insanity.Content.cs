#nullable enable

using System;
using System.Collections.Generic;
using CustomRendering;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// <see cref="Insanity"/> の演出データ。フェーズ定義・進行パターン・文言バンク・死因・
/// 医療アイテム服用時の悪化プロファイルをここに集約している。
/// <para>
/// すべて public static なので、プラグイン側から差し替え・追記できる。
/// <see cref="Insanity.Patterns"/> は初回アクセス時に組み立てられるため、
/// 文言バンクを書き換えてから最初の発狂が起きればそれが反映される。
/// </para>
/// </summary>
public partial class Insanity
{
    // ===== 文言トークン =====

    /// <summary>プレイヤー本人の名前。</summary>
    private const string NameToken = "{NAME}";

    /// <summary>同じラウンドにいる別のプレイヤーの名前。</summary>
    private const string OtherToken = "{OTHER}";

    /// <summary>現在の部屋名（日本語）。</summary>
    private const string RoomToken = "{ROOM}";

    /// <summary>現在のゾーン名（日本語）。</summary>
    private const string ZoneToken = "{ZONE}";

    /// <summary>現在の体力。</summary>
    private const string HealthToken = "{HP}";

    /// <summary>手に持っているアイテム。</summary>
    private const string ItemToken = "{ITEM}";

    /// <summary>ラウンド経過時間 (mm:ss)。</summary>
    private const string TimeToken = "{TIME}";

    /// <summary>ランダムな 2 桁〜3 桁の数値。</summary>
    private const string NumberToken = "{NUM}";

    /// <summary>FogControl に渡せる Intensity の上限（= FogType の数）。</summary>
    private static readonly int FogTypeCount = Enum.GetValues(typeof(FogType)).Length;

    /// <summary><see cref="TripPhase.BurstPool"/> 未指定時に使うバースト種別。</summary>
    public static BurstKind[] DefaultBurstPool { get; set; } =
    [
        BurstKind.Zoom,
        BurstKind.Zoom,
        BurstKind.Flood,
        BurstKind.Flood,
        BurstKind.Corrupt,
        BurstKind.Sweep,
        BurstKind.Split,
        BurstKind.Swarm,
        BurstKind.Wall,
        BurstKind.Blackout,
    ];

    /// <summary>
    /// ノイズ素材に混ぜる「機械のログ」風断片。文書だけだと語彙が偏るので厚めに混ぜている。
    /// </summary>
    public static string[] SystemFragments { get; set; } =
    [
        "0x00000000 0x0000DEAD",
        "ERR_NO_SIGNAL",
        "STACK OVERFLOW AT",
        "FATAL: heap corrupted",
        "retrying (attempt 41)",
        "observer.dll not found",
        "SIGSEGV received",
        "CHECKSUM MISMATCH",
        "[WARN] unbound handle",
        "assert(self != null)",
        "while(true){ }",
        "IT IS STILL RUNNING",
        "0xC0000005",
        "kernel panic - not syncing",
        "unreachable code reached",
        "██████████████",
        "NULL NULL NULL NULL",
        "site02.integrity = false",
        "MEMORY DUMP 0/0 bytes",
        "recursion depth exceeded",
        "SCP-████ 収容記録 欠落",
        "被験体は自らの名を忘却した",
        "監視カメラ 応答なし",
        "本記録は閲覧されていない",
        "END OF FILE END OF FILE",
    ];

    // ===== フェーズ / パターン =====

    /// <summary>1 フェーズ分の描画・効果パラメータ。</summary>
    public sealed class TripPhase
    {
        /// <summary>全体進捗（0..1）に対する開始比率。</summary>
        public float StartRatio;

        /// <summary>再描画間隔（秒）。そのまま Hint の送信頻度になる。</summary>
        public float Interval = 0.3f;

        // --- ノイズ層 ---

        /// <summary>ノイズ層を出さずバッファも捨てる。</summary>
        public bool ClearNoise;

        /// <summary>行の配置方法。</summary>
        public NoiseLayout Layout = NoiseLayout.Block;

        /// <summary>1 tick で描き直す既存行の本数。</summary>
        public int ChurnLines;

        /// <summary>同色で塗る文字数。小さいほど派手だが送信量が増える。</summary>
        public int ColorRunLength = 20;

        /// <summary>1 文字あたりの文字化け確率。</summary>
        public float CorruptChance;

        /// <summary>彩度を落としたパレットを使うか。序盤の「じわじわ来る」表現用。</summary>
        public bool DimNoise;

        /// <summary>1 行の長さ倍率。<see cref="NoiseLayout.Block"/> 以外では短くして散らす。</summary>
        public float LineWidthScale = 1f;

        /// <summary>Block 以外のレイアウトで使う横方向の振れ幅。</summary>
        public float NoiseSpread = 380f;

        /// <summary>レイアウトオフセットの移動速度（units/sec）。斜め流し・横スクロールの速さ。</summary>
        public float ScrollSpeed;

        /// <summary>目標行数の倍率。1 未満なら埋め尽くさない。</summary>
        public float FillRatio = 1f;

        /// <summary>Hint 座標のランダム振れ幅（画面揺れ表現）。</summary>
        public float Shake;

        // --- バースト ---

        /// <summary>1 tick あたりのバースト発生確率。</summary>
        public float BurstChance;

        /// <summary>このフェーズで抽選されるバースト種別。null なら <see cref="DefaultBurstPool"/>。</summary>
        public BurstKind[]? BurstPool;

        // --- メッセージ層 ---

        public int MessageFontSize = 40;

        /// <summary>メッセージ差し替え間隔（秒）。<see cref="Interval"/> と同値なら毎 tick 差し替わる。</summary>
        public float MessageInterval = 1f;

        /// <summary>メッセージを空にする確率。明滅の速さを決める。</summary>
        public float MessageBlankChance;

        /// <summary>メッセージ本文の文字化け率。</summary>
        public float MessageCorruptChance;

        /// <summary>メッセージの色替えピッチ。大きいほど単色に近づく。</summary>
        public int MessageColorRunLength = 24;

        /// <summary>null ならランダム色。</summary>
        public string? MessageColor;

        /// <summary>メッセージ表示位置のふらつき幅。大きいほど画面のどこにでも出る。</summary>
        public float MessageWander;

        /// <summary>装飾（縦積み・傾き・字間開け・反復・ハイライト）が乗る確率。</summary>
        public float MessageStyleChance;

        public string[] Messages = [];

        // --- 暴走テキスト層 ---

        /// <summary>Intensity 255 のときに走らせる本数。</summary>
        public int RoamerCount;

        /// <summary>移動速度（units/sec）。</summary>
        public float RoamerSpeed = 620f;

        /// <summary>基準フォントサイズ。実際は ±ランダムが乗る。</summary>
        public int RoamerFontSize = 34;

        /// <summary>跳ね返り以外で文言が入れ替わる確率（1 tick あたり）。</summary>
        public float RoamerRespawnChance = 0.06f;

        /// <summary>暴走テキストの色替えピッチ。</summary>
        public int RoamerColorRunLength = 6;

        /// <summary>暴走テキスト専用の文言。null なら <see cref="Messages"/> を使う。</summary>
        public string[]? RoamerMessages;

        // --- 付随エフェクト ---

        /// <summary>フェーズ中の FogControl Intensity（= FogType + 1）。</summary>
        public byte FogIntensity;

        /// <summary>VisualTraumatized の Intensity。0 で解除。</summary>
        public byte TraumatizedIntensity;

        /// <summary>VisualSinkhole の Intensity。0 で解除。</summary>
        public byte SinkholeIntensity;

        /// <summary>フェーズ中ずっと Deafened を掛けるか。</summary>
        public bool Deafen;

        /// <summary>フェーズ突入時に短い Flashed を差し込むか。</summary>
        public bool FlashOnEnter;

        /// <summary>フェーズ突入時に短い Blindness（暗転）を差し込むか。</summary>
        public bool BlindOnEnter;

        /// <summary>毎 tick、短命の視覚 / 聴覚エフェクトが差し込まれる確率。</summary>
        public float FlickerChance;

        /// <summary>毎 tick、霧の種類がランダムに切り替わる確率。</summary>
        public float FogFlickerChance;
    }

    /// <summary>
    /// 進行パターン 1 つぶん。付与のたびに <see cref="Weight"/> で抽選される。
    /// </summary>
    public sealed class TripPattern
    {
        public TripPattern(string name, float weight, TripPhase[] phases)
        {
            Name = name;
            Weight = weight;
            Phases = phases;
        }

        /// <summary>識別名。<see cref="ForcedPatternName"/> や <see cref="OverdoseProfile.PatternName"/> で指定する。</summary>
        public string Name { get; }

        /// <summary>抽選の重み。0 にすると通常抽選から外れる（悪化時の指定専用になる）。</summary>
        public float Weight { get; set; }

        /// <summary>フェーズ列。<see cref="TripPhase.StartRatio"/> の昇順で並べること。</summary>
        public TripPhase[] Phases { get; set; }

        /// <summary>持続時間の中でフェーズ列を何周させるか。2 以上で「波」になる。</summary>
        public int Cycles { get; set; } = 1;

        /// <summary>全フェーズの <see cref="TripPhase.Interval"/> に掛かる倍率。小さいほど速い。</summary>
        public float SpeedScale { get; set; } = 1f;
    }

    /// <summary>医療アイテムを服用したときの悪化のしかた。</summary>
    public sealed class OverdoseProfile
    {
        /// <summary>「治った」と錯覚させるために全部消しておく秒数。</summary>
        public float CalmSeconds = 1.2f;

        /// <summary>発狂状態の残り時間に上乗せする秒数。</summary>
        public float ExtraDuration = 25f;

        /// <summary>以降の tick 間隔に掛かる倍率（累積）。小さいほど速くなる。</summary>
        public float SpeedMultiplier = 0.85f;

        /// <summary>再発時に切り替える進行パターン名。null なら維持。</summary>
        public string? PatternName;

        /// <summary>再発直後に優先表示される専用文言。</summary>
        public string[] Messages = [];

        /// <summary>専用文言を優先する秒数。</summary>
        public float MessageSeconds = 6f;
    }

    /// <summary>未登録アイテムを飲んだときのプロファイル。</summary>
    public static OverdoseProfile DefaultOverdoseProfile { get; set; } = new()
    {
        CalmSeconds = 1.0f,
        ExtraDuration = 15f,
        SpeedMultiplier = 0.92f,
    };

    private static Dictionary<ItemType, OverdoseProfile>? _overdoseProfiles;

    /// <summary>
    /// 「発狂状態に効く薬」として名乗り出るアイテムと、その悪化内容。
    /// ここに載っているアイテムだけが <see cref="IsHealable"/> で true を返す。
    /// <para>
    /// 文言バンクを参照するため、静的初期化ではなく初回アクセス時に組み立てる。
    /// </para>
    /// </summary>
    public static Dictionary<ItemType, OverdoseProfile> OverdoseProfiles
    {
        get => _overdoseProfiles ??= BuildDefaultOverdoseProfiles();
        set => _overdoseProfiles = value;
    }

    private static Dictionary<ItemType, OverdoseProfile> BuildDefaultOverdoseProfiles()
    {
        return new Dictionary<ItemType, OverdoseProfile>
        {
            // 痛みも音も遠のいていく。白く飛ぶ方向へ倒れる。
            [ItemType.Painkillers] = new OverdoseProfile
            {
                CalmSeconds = 1.4f,
                ExtraDuration = 30f,
                SpeedMultiplier = 0.85f,
                PatternName = "Whiteout",
                MessageSeconds = 7f,
                Messages = PainkillerMessages,
            },

            // 心拍だけが上がって、全部が早送りになる。
            [ItemType.Adrenaline] = new OverdoseProfile
            {
                CalmSeconds = 0.7f,
                ExtraDuration = 25f,
                SpeedMultiplier = 0.62f,
                PatternName = "Meltdown",
                MessageSeconds = 7f,
                Messages = AdrenalineMessages,
            },

            // 万能薬。一番長く「治った」と思わせてから、一番悪い形で戻る。
            [ItemType.SCP500] = new OverdoseProfile
            {
                CalmSeconds = 2.6f,
                ExtraDuration = 45f,
                SpeedMultiplier = 0.7f,
                PatternName = "Relapse",
                MessageSeconds = 9f,
                Messages = Scp500Messages,
            },

            // 傷は塞がる。中身には効かない。
            [ItemType.Medkit] = new OverdoseProfile
            {
                CalmSeconds = 1.1f,
                ExtraDuration = 18f,
                SpeedMultiplier = 0.9f,
                PatternName = "Ambush",
                MessageSeconds = 6f,
                Messages = MedkitMessages,
            },
        };
    }

    private static TripPattern[]? _patterns;

    /// <summary>
    /// 進行パターン一覧。初回アクセス時に <see cref="BuildDefaultPatterns"/> で組み立てる。
    /// 差し替えると以降の発狂に反映される。
    /// </summary>
    public static TripPattern[] Patterns
    {
        get => _patterns ??= BuildDefaultPatterns();
        set => _patterns = value;
    }

    // ===== 文言 =====

    /// <summary>予兆。まだ「気のせい」で済ませられる違和感。</summary>
    public static string[] WhisperMessages { get; set; } =
    [
        "……なにか、におう",
        "…………",
        "いま、だれか しゃべった？",
        "{NAME}",
        "……あれ",
        "へやの かたちが ちがう",
        "まばたき、した？",
        "ゆびの かず",
        "{ROOM} は こんなに ひろかった？",
        "うしろの ドア、さっき しまってた？",
        "……いま、なまえを よばれた",
        "てのひらが つめたい",
        "かべの おと",
        "{OTHER} は どこ？",
        "じかんが とんだ",
        "みぎめだけ ぼやける",
    ];

    /// <summary>侵食。読んでいる文書と自分の現実が混ざり始める。</summary>
    public static string[] CreepMessages { get; set; } =
    [
        "この文書を読んだ記録は残りません",
        "うしろの人数が さっきと違う",
        "あなたの職員番号を思い出せますか",
        "■■■博士の署名がある",
        "読むのを やめてください",
        "{NAME} という職員は在籍していません",
        "そこに書いてあるのは あなたの名前です",
        "まだ 半分も 読んでいない",
        "目を 離さないでください",
        "この部屋は 記録上 存在しません",
        "収容記録の日付が 明日になっている",
        "{ROOM} での死亡記録は {NUM} 件です",
        "{OTHER} は {NUM} 分前に死亡しています",
        "あなたの持っている {ITEM} は支給されていません",
        "体温 {NUM}.4 度 — 正常範囲外",
        "経過時間 {TIME} — 記録と一致しません",
        "残存体力 {HP} — この値は改竄されています",
        "この部屋には 出口が ありません",
        "誰かが あなたのふりを しています",
        "同じ文章を {NUM} 回 読み返しています",
    ];

    /// <summary>崩壊開始。システム側が壊れていく。</summary>
    public static string[] FractureMessages { get; set; } =
    [
        "SLAFIGHT.EXE (応答なし)",
        "MEMORY_ACCESS_VIOLATION AT 0x00000000",
        "整合性チェック: 失敗 (対象: {NAME})",
        "SCP-███ は収容されていません",
        "再起動を試みています ... {NUM} 回目",
        "observer.dll を読み込めませんでした",
        "あなたの視点は現在 別の場所にあります",
        "█████████ を削除できません",
        "この記録は書き換えられました",
        "接続が確立されました (発信元: 不明)",
        "D-██████ の生存記録が見つかりません",
        "MEMORY LEAK: 意識",
        "HANDLE_NOT_CLOSED: {NAME}",
        "同じフレームを {NUM} 回 描画しています",
        "{ZONE} のレンダリングを中止しました",
        "player[{NAME}].alive = false",
        "視覚出力を {OTHER} に転送しています",
        "ROLLBACK FAILED — 戻る先がありません",
        "あなたのセーブデータは存在しません",
        "TIMESTAMP {TIME} は未来です",
    ];

    /// <summary>最大強度。短く、断定的で、こちらを名指しする。</summary>
    public static string[] CollapseMessages { get; set; } =
    [
        "ミ テ イ ル",
        "うしろ",
        "{NAME}",
        "ソレハ アナタ デハ ナイ",
        "目 ヲ 開 ケ ル ナ",
        "█████",
        "ここは Site-02 ではない",
        "かえして",
        "ドウシテ キヅカナイノ",
        "モウ オソイ",
        "アナタハ 何回目 デスカ",
        "ワタシノ 名前ヲ 言エ",
        "ズット イタ",
        "{OTHER} ジャ ナイ",
        "ソコ ニ イル ノハ ダレ",
        "{ROOM}",
        "ニゲラレナイ",
        "テ ヲ ハナセ",
        "ヨンダ ノハ アナタ",
        "アト {NUM} カイ",
        "ミ ツ ケ タ",
        "イタイ",
    ];

    /// <summary>静寂。何事もなかったことにされる。</summary>
    public static string[] SilenceMessages { get; set; } =
    [
        "…………",
        "…………",
        "収容違反は発生していません",
        "あなたは なにも 見ませんでした",
        "…………",
        "ご協力ありがとうございました",
        "本件の記録は破棄されました",
        "{NAME} は正常です",
        "……",
        "おやすみなさい",
    ];

    /// <summary>バースト時にだけ 1 tick 表示される、短く大きい文言。</summary>
    public static string[] BurstMessages { get; set; } =
    [
        "ミツケタ",
        "オカエリ",
        "ヨンダ？",
        "ソコ",
        "{NAME}",
        "ミルナ",
        "■■■■■■",
        "ワタシヲ ミテ",
        "ネエ",
        "ウシロ",
        "ドイテ",
        "ソレ ヲ ハナセ",
        "{OTHER}",
        "マダ イル",
        "ア",
        "モット",
        "コッチ",
        "ダメ",
    ];

    /// <summary>ジョークウイルス寄り。システムが壊れて煽ってくる。</summary>
    public static string[] SystemErrorMessages { get; set; } =
    [
        "システムを破壊しています ... {NUM}%",
        "全ファイルを削除しますか？ [ はい ] [ はい ]",
        "OK を押しても閉じません",
        "ウイルスを検出しました: {NAME}.exe",
        "この操作は取り消せません",
        "処理を続行するには 目を閉じてください",
        "再インストールを開始します (残り ∞ 分)",
        "警告: あなたは既に感染しています",
        "×ボタンは無効化されました",
        "C:\\{NAME}\\ を削除中 ... ",
        "応答なし ... 応答なし ... 応答なし",
        "強制終了できません (所有者: 不明)",
        "アップデートが {NUM} 件 保留中です",
        "この画面は録画されています",
        "管理者権限を要求しています: ███",
        "デスクトップに戻ることはできません",
        "エラー コード: {TIME}",
        "残りメモリ: {HP} MB",
        "しばらくお待ちください ... ずっと",
        "ファイル {ITEM} は使用中です",
    ];

    /// <summary>多幸。色鮮やかで、やたら親切で、噛み合っていない。</summary>
    public static string[] EuphoriaMessages { get; set; } =
    [
        "きれい",
        "ぜんぶ うまくいってる",
        "こわがらなくて いいよ",
        "{NAME} は よく がんばりました",
        "いっしょに いこう",
        "もう いたくない でしょう？",
        "わらって",
        "ここは あんぜんです",
        "{OTHER} も よろこんでる",
        "ずっと ここに いようね",
        "あたたかい",
        "め を つぶって",
        "ありがとう ありがとう ありがとう",
        "たのしい？",
        "おかえりなさい",
        "もう かえらなくて いい",
    ];

    /// <summary>他人が信用できなくなる系。</summary>
    public static string[] ParanoiaMessages { get; set; } =
    [
        "{OTHER} は さっきから 同じ場所にいる",
        "{OTHER} は あなたの顔をしている",
        "うしろの足音は {NUM} 人ぶん",
        "{OTHER} の名前を 誰も知らない",
        "無線は 誰にも 届いていません",
        "{OTHER} は もう 死んでいます",
        "その声は 外から 聞こえていますか",
        "{OTHER} と目を合わせないでください",
        "味方は {NUM} 人 減りました",
        "誰かが あなたの {ITEM} を数えている",
        "{ROOM} には あなた以外 いません",
        "{OTHER} が こっちを 見た",
        "その扉は 誰が 開けましたか",
        "背後の呼吸が 自分のものと ズレている",
    ];

    /// <summary>身体感覚の異常。</summary>
    public static string[] BodyHorrorMessages { get; set; } =
    [
        "指が {NUM} 本 あります",
        "心臓の音が 背中から 聞こえる",
        "呼吸の 仕方を 忘れています",
        "口の中に 砂がある",
        "体力 {HP} — それは誰の値ですか",
        "皮膚の下で なにか 動いた",
        "自分の足音だけ 遅れて 届く",
        "瞬きを {NUM} 秒 していません",
        "喉に 手が 触れている",
        "手が 冷たい 手が 冷たい 手が 冷たい",
        "目を 開けているのは 誰ですか",
        "血の味がする 傷はない",
        "歯を 数えないでください",
        "腕が 一本 多い",
    ];

    /// <summary>記憶の齟齬。いま見ているものが信用できなくなる。</summary>
    public static string[] MemoryMessages { get; set; } =
    [
        "あなたは {ROOM} に来た覚えがありますか",
        "{TIME} からの記憶がありません",
        "{ITEM} を どこで 拾いましたか",
        "さっきまで {ZONE} にいたはずです",
        "この会話は {NUM} 回目です",
        "あなたは 一度 ここで 死んでいます",
        "{NAME} は 別の名前でした",
        "扉を開けた記憶が 巻き戻されました",
        "同じ部屋に {NUM} 回 入っています",
        "起きたのは いつですか",
        "{OTHER} と 話した内容を 言えますか",
        "地図が 一致しません",
    ];

    /// <summary>秒読み・急かし。何が終わるのかは言わない。</summary>
    public static string[] CountdownMessages { get; set; } =
    [
        "あと {NUM} 秒",
        "3",
        "2",
        "1",
        "0",
        "まだ 間に合います",
        "もう 間に合いません",
        "はやく",
        "はやく はやく はやく",
        "うごいて",
        "とまらないで",
        "そこから 離れて",
        "{NUM}",
        "いま",
    ];

    /// <summary>断片だけが残る。白飛び用。</summary>
    public static string[] StaticMessages { get; set; } =
    [
        "█",
        "…",
        "ア",
        "—",
        "{NAME}",
        "■",
        "▒▒▒",
        "・",
        "ノ",
        "…",
        "ザ",
        "█ █ █",
    ];

    /// <summary>暴走テキスト専用の短い語。走り回るので長い文は入らない。</summary>
    public static string[] RoamerShortMessages { get; set; } =
    [
        "ミテル",
        "ウシロ",
        "コッチ",
        "{NAME}",
        "ニゲロ",
        "ダメ",
        "ア",
        "ハヤク",
        "ソコ",
        "イヤ",
        "█████",
        "オイデ",
        "ヤメテ",
        "マダ",
        "ドコ",
        "{OTHER}",
        "シ",
        "ネエ",
        "ミルナ",
        "モウイイ",
        "タスケテ",
        "ワタシ",
        "ウソ",
        "ソレ",
    ];

    /// <summary>鎮痛剤を飲んだ直後の文言。</summary>
    public static string[] PainkillerMessages { get; set; } =
    [
        "痛みは 消えました",
        "痛みは 消えました",
        "痛みは 消えました",
        "なにも 感じません",
        "感じるものが なくなりました",
        "{NAME} は もう 痛がりません",
        "音が 遠い",
        "効いています ずっと 効いています",
        "用量を 超えました",
        "からだが どこにあるか わからない",
    ];

    /// <summary>アドレナリンを打った直後の文言。</summary>
    public static string[] AdrenalineMessages { get; set; } =
    [
        "心拍 {NUM}",
        "はやい はやい はやい",
        "全部 早送りになった",
        "止まれない",
        "手が 震えて 止まらない",
        "{NAME} 心拍数 異常",
        "もっと はやく",
        "追いつかれる",
        "血が 熱い",
        "まばたきが 間に合わない",
    ];

    /// <summary>SCP-500 を飲んだ直後の文言。</summary>
    public static string[] Scp500Messages { get; set; } =
    [
        "治りました",
        "……治りましたか？",
        "SCP-500 は これに 効きません",
        "治療対象が 見つかりません",
        "なおって ない",
        "{NAME} の症状は 記録されていません",
        "もう 一度 のみますか",
        "薬は 飲みこまれました こちらに",
        "オカエリナサイ",
        "治ったふりを してくれて ありがとう",
        "これで {NUM} 錠目です",
    ];

    /// <summary>メディキットを使った直後の文言。</summary>
    public static string[] MedkitMessages { get; set; } =
    [
        "傷は 塞がりました",
        "中身には 効きません",
        "{HP} まで 回復しました",
        "包帯の下を 見ないでください",
        "手当てをしたのは 誰ですか",
        "血は 止まりました 音は 止まりません",
        "処置完了 — 対象は 既に 死亡",
    ];

    /// <summary>
    /// 発狂状態のまま死んだプレイヤーに表示される死因。
    /// 実際の死因（銃撃・SCP など）に関係なく、この中からランダムに 1 つ選ばれる。
    /// </summary>
    public static string[] DeathReasons { get; set; } =
    [
        "自分の首を引っ掻いて死んだ",
        "自分の喉を掻き裂いて死んだ",
        "見えない何かから逃げようとして事切れた",
        "自分の目を抉り出した",
        "「来るな」と叫び続けたまま息絶えた",
        "壁に頭を打ちつけ続けて死んだ",
        "舌を噛み切って死んだ",
        "誰もいない方向へ命乞いをしながら死んだ",
        "自分の心臓を掴み出そうとして死んだ",
        "笑いながら自分を殴り続けて死んだ",
        "「やっと静かになった」と呟いて動かなくなった",
        "存在しない出口を掻き続け、力尽きた",
        "鏡の中の自分に殺された",
        "呼吸の仕方を忘れて窒息した",
        "自分の耳を削ぎ落として失血死した",
        "███ に名前を呼ばれて心臓が止まった",
        "自分の指を一本ずつ数え終えて死んだ",
        "天井に向かって謝り続けたまま死んだ",
        "床に無い階段を降りようとして首を折った",
        "誰かに返事をしながら息を止めた",
        "自分の名前を思い出せずに死んだ",
        "「三人目はどこだ」と言い残して死んだ",
        "見えている扉を開けようとして壁に潰された",
        "自分の影を剥がそうとして死んだ",
        "何もない場所へ全力で走り、動かなくなった",
        "数え終わった瞬間に心臓が止まった",
        "口を塞いだまま声を出そうとして窒息した",
        "「もう痛くない」と繰り返して冷たくなった",
        "自分の心音を止めようとして死んだ",
        "笑い声だけを残して倒れた",
    ];

    /// <summary>死因テキストを 1 つランダムに選ぶ。</summary>
    public static string PickDeathReason()
    {
        string[] reasons = DeathReasons;

        if (reasons is null || reasons.Length == 0)
            return "発狂して死んだ";

        return reasons[UnityEngine.Random.Range(0, reasons.Length)];
    }

    // ===== 進行パターン定義 =====

    /// <summary>
    /// 既定の進行パターン一式を組み立てる。
    /// 文言バンクを参照するため、静的初期化ではなく初回アクセス時に呼ばれる。
    /// </summary>
    private static TripPattern[] BuildDefaultPatterns()
    {
        return
        [
            BuildDescentPattern(),
            BuildAmbushPattern(),
            BuildMeltdownPattern(),
            BuildKaleidoscopePattern(),
            BuildWhiteoutPattern(),
            BuildChoirPattern(),
            BuildRelapsePattern(),
        ];
    }

    /// <summary>
    /// 沈降。予兆 → 侵食 → 崩壊 → 最大強度 → 静寂。じわじわ悪くなる王道の形。
    /// </summary>
    private static TripPattern BuildDescentPattern()
    {
        return new TripPattern("Descent", 26f,
        [
            // 0.00 - 0.12 : 予兆。数行だけ、彩度も低い。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.42f,
                ChurnLines = 1,
                ColorRunLength = 28,
                CorruptChance = 0.03f,
                DimNoise = true,
                FillRatio = 0.5f,
                BurstChance = 0.03f,
                BurstPool = [BurstKind.Zoom, BurstKind.Blackout],
                MessageFontSize = 30,
                MessageInterval = 1.7f,
                MessageBlankChance = 0.50f,
                MessageColorRunLength = 40,
                MessageWander = 40f,
                MessageStyleChance = 0.10f,
                FogIntensity = (byte)(FogType.Amnesia + 1),
                FogFlickerChance = 0.02f,
                Messages = WhisperMessages,
            },

            // 0.12 - 0.34 : 侵食。文字が溜まり始め、暴走テキストが 2 本だけ走り出す。
            new TripPhase
            {
                StartRatio = 0.12f,
                Interval = 0.30f,
                ChurnLines = 2,
                ColorRunLength = 26,
                CorruptChance = 0.08f,
                FillRatio = 0.75f,
                Shake = 5f,
                BurstChance = 0.09f,
                MessageFontSize = 38,
                MessageInterval = 1.0f,
                MessageBlankChance = 0.35f,
                MessageCorruptChance = 0.02f,
                MessageColorRunLength = 24,
                MessageWander = 90f,
                MessageStyleChance = 0.18f,
                RoamerCount = 3,
                RoamerSpeed = 480f,
                RoamerFontSize = 28,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = (byte)(FogType.Scp244 + 1),
                TraumatizedIntensity = 80,
                FlickerChance = 0.06f,
                FogFlickerChance = 0.05f,
                Messages = CreepMessages,
            },

            // 0.34 - 0.58 : 崩壊開始。行が斜めに流れ出す。
            new TripPhase
            {
                StartRatio = 0.34f,
                Interval = 0.21f,
                Layout = NoiseLayout.Diagonal,
                LineWidthScale = 0.55f,
                NoiseSpread = 470f,
                ScrollSpeed = 420f,
                ChurnLines = 5,
                ColorRunLength = 22,
                CorruptChance = 0.18f,
                Shake = 14f,
                BurstChance = 0.19f,
                MessageFontSize = 52,
                MessageInterval = 0.45f,
                MessageBlankChance = 0.26f,
                MessageCorruptChance = 0.08f,
                MessageColorRunLength = 12,
                MessageWander = 170f,
                MessageStyleChance = 0.30f,
                RoamerCount = 6,
                RoamerSpeed = 780f,
                RoamerFontSize = 34,
                RoamerRespawnChance = 0.08f,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = (byte)(FogType.Nuke + 1),
                TraumatizedIntensity = 160,
                SinkholeIntensity = 255,
                FlickerChance = 0.16f,
                FogFlickerChance = 0.12f,
                Messages = FractureMessages,
            },

            // 0.58 - 0.86 : 最大強度。画面が埋まりきり、常時なにかが跳ね回る。
            new TripPhase
            {
                StartRatio = 0.58f,
                Interval = 0.13f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.62f,
                NoiseSpread = 540f,
                ChurnLines = 9,
                ColorRunLength = 20,
                CorruptChance = 0.35f,
                Shake = 34f,
                BurstChance = 0.34f,
                MessageFontSize = 76,
                MessageInterval = 0.18f,
                MessageBlankChance = 0.30f,
                MessageCorruptChance = 0.18f,
                MessageColorRunLength = 5,
                MessageWander = 250f,
                MessageStyleChance = 0.45f,
                RoamerCount = 12,
                RoamerSpeed = 1250f,
                RoamerFontSize = 40,
                RoamerRespawnChance = 0.14f,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = 255, // MaxIntensity(=FogType の数) に丸められ FogType.PocketDimension になる
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.34f,
                FogFlickerChance = 0.26f,
                Messages = CollapseMessages,
            },

            // 0.86 - 1.00 : 静寂。暗転を挟んで全部消え、音も遠くなる。
            new TripPhase
            {
                StartRatio = 0.86f,
                Interval = 0.45f,
                ClearNoise = true,
                MessageFontSize = 44,
                MessageInterval = 2.0f,
                MessageBlankChance = 0.15f,
                MessageColor = "#9a9a9a",
                MessageWander = 30f,
                BurstChance = 0.04f,
                BurstPool = [BurstKind.Blackout, BurstKind.Zoom],
                FogIntensity = (byte)(FogType.BecomingFlamingo + 1),
                Deafen = true,
                BlindOnEnter = true,
                Messages = SilenceMessages,
            },
        ]);
    }

    /// <summary>
    /// 不意打ち。ほぼ何も起きない時間と最大強度が交互に来る。
    /// <see cref="TripPattern.Cycles"/> で複数回の波にする。
    /// </summary>
    private static TripPattern BuildAmbushPattern()
    {
        return new TripPattern("Ambush", 18f,
        [
            // 凪。ほとんど無音で、たまに一言だけ。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.40f,
                ClearNoise = true,
                MessageFontSize = 32,
                MessageInterval = 1.6f,
                MessageBlankChance = 0.62f,
                MessageColorRunLength = 44,
                MessageWander = 120f,
                MessageStyleChance = 0.12f,
                BurstChance = 0.02f,
                BurstPool = [BurstKind.Blackout],
                FogIntensity = (byte)(FogType.Amnesia + 1),
                Messages = WhisperMessages,
            },

            // ざわつき。1 行だけ視界の端に湧く。
            new TripPhase
            {
                StartRatio = 0.42f,
                Interval = 0.26f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.4f,
                NoiseSpread = 520f,
                ChurnLines = 3,
                ColorRunLength = 20,
                CorruptChance = 0.12f,
                FillRatio = 0.35f,
                Shake = 8f,
                BurstChance = 0.10f,
                MessageFontSize = 44,
                MessageInterval = 0.7f,
                MessageBlankChance = 0.30f,
                MessageCorruptChance = 0.06f,
                MessageWander = 200f,
                MessageStyleChance = 0.28f,
                RoamerCount = 4,
                RoamerSpeed = 900f,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = (byte)(FogType.Scp244 + 1),
                TraumatizedIntensity = 120,
                FlickerChance = 0.14f,
                FogFlickerChance = 0.10f,
                Messages = ParanoiaMessages,
            },

            // 襲撃。短いが最大強度。抜けたらまた凪に戻る。
            new TripPhase
            {
                StartRatio = 0.62f,
                Interval = 0.11f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.7f,
                NoiseSpread = 560f,
                ChurnLines = 12,
                ColorRunLength = 14,
                CorruptChance = 0.45f,
                Shake = 46f,
                BurstChance = 0.46f,
                MessageFontSize = 88,
                MessageInterval = 0.13f,
                MessageBlankChance = 0.24f,
                MessageCorruptChance = 0.22f,
                MessageColorRunLength = 4,
                MessageWander = 280f,
                MessageStyleChance = 0.5f,
                RoamerCount = 14,
                RoamerSpeed = 1500f,
                RoamerFontSize = 44,
                RoamerRespawnChance = 0.2f,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.45f,
                FogFlickerChance = 0.35f,
                Messages = CollapseMessages,
            },

            // 引き。何事もなかったことにされる。
            new TripPhase
            {
                StartRatio = 0.86f,
                Interval = 0.34f,
                ClearNoise = true,
                MessageFontSize = 40,
                MessageInterval = 1.4f,
                MessageBlankChance = 0.4f,
                MessageColor = "#8f8f8f",
                MessageWander = 60f,
                BlindOnEnter = true,
                FogIntensity = (byte)(FogType.Amnesia + 1),
                Messages = SilenceMessages,
            },
        ])
        {
            Cycles = 3,
        };
    }

    /// <summary>
    /// システム崩壊。最初から速く、加速し続ける。ジョークウイルス寄りの見た目。
    /// </summary>
    private static TripPattern BuildMeltdownPattern()
    {
        return new TripPattern("Meltdown", 16f,
        [
            // 起動。ウィンドウが 1 枚だけ出る。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.24f,
                Layout = NoiseLayout.Ticker,
                LineWidthScale = 0.5f,
                NoiseSpread = 480f,
                ScrollSpeed = 900f,
                ChurnLines = 3,
                ColorRunLength = 18,
                CorruptChance = 0.10f,
                FillRatio = 0.45f,
                Shake = 6f,
                BurstChance = 0.12f,
                BurstPool = [BurstKind.Corrupt, BurstKind.Sweep, BurstKind.Split],
                MessageFontSize = 40,
                MessageInterval = 0.55f,
                MessageBlankChance = 0.2f,
                MessageCorruptChance = 0.05f,
                MessageColorRunLength = 16,
                MessageWander = 150f,
                MessageStyleChance = 0.3f,
                RoamerCount = 5,
                RoamerSpeed = 1000f,
                RoamerFontSize = 26,
                RoamerMessages = SystemErrorMessages,
                FogIntensity = (byte)(FogType.Decontamination + 1),
                FlickerChance = 0.16f,
                FogFlickerChance = 0.16f,
                Messages = SystemErrorMessages,
            },

            // 増殖。ウィンドウが増え、斜めに流れ出す。
            new TripPhase
            {
                StartRatio = 0.28f,
                Interval = 0.17f,
                Layout = NoiseLayout.Diagonal,
                LineWidthScale = 0.45f,
                NoiseSpread = 560f,
                ScrollSpeed = 1500f,
                ChurnLines = 7,
                ColorRunLength = 15,
                CorruptChance = 0.24f,
                Shake = 22f,
                BurstChance = 0.28f,
                BurstPool = [BurstKind.Sweep, BurstKind.Split, BurstKind.Flood, BurstKind.Swarm, BurstKind.Corrupt],
                MessageFontSize = 58,
                MessageInterval = 0.3f,
                MessageBlankChance = 0.18f,
                MessageCorruptChance = 0.12f,
                MessageColorRunLength = 8,
                MessageWander = 230f,
                MessageStyleChance = 0.42f,
                RoamerCount = 10,
                RoamerSpeed = 1450f,
                RoamerFontSize = 30,
                RoamerRespawnChance = 0.16f,
                RoamerMessages = SystemErrorMessages,
                FogIntensity = (byte)(FogType.Nuke + 1),
                TraumatizedIntensity = 140,
                FlickerChance = 0.3f,
                FogFlickerChance = 0.3f,
                Messages = SystemErrorMessages,
            },

            // 飽和。画面が全部ウィンドウで埋まる。
            new TripPhase
            {
                StartRatio = 0.58f,
                Interval = 0.10f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.55f,
                NoiseSpread = 580f,
                ChurnLines = 14,
                ColorRunLength = 12,
                CorruptChance = 0.42f,
                Shake = 40f,
                BurstChance = 0.5f,
                BurstPool = [BurstKind.Wall, BurstKind.Flood, BurstKind.Sweep, BurstKind.Split, BurstKind.Swarm, BurstKind.Corrupt, BurstKind.Zoom],
                MessageFontSize = 80,
                MessageInterval = 0.12f,
                MessageBlankChance = 0.16f,
                MessageCorruptChance = 0.25f,
                MessageColorRunLength = 4,
                MessageWander = 300f,
                MessageStyleChance = 0.55f,
                RoamerCount = 14,
                RoamerSpeed = 1750f,
                RoamerFontSize = 34,
                RoamerRespawnChance = 0.22f,
                RoamerMessages = SystemErrorMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.5f,
                FogFlickerChance = 0.42f,
                Messages = SystemErrorMessages,
            },

            // 落ちる。全部消えて、最後に一言。
            new TripPhase
            {
                StartRatio = 0.9f,
                Interval = 0.3f,
                ClearNoise = true,
                MessageFontSize = 54,
                MessageInterval = 1.2f,
                MessageBlankChance = 0.25f,
                MessageColor = "#c8c8c8",
                MessageStyleChance = 0.2f,
                BlindOnEnter = true,
                Deafen = true,
                FogIntensity = (byte)(FogType.Decontamination + 1),
                Messages = SilenceMessages,
            },
        ])
        {
            SpeedScale = 0.9f,
        };
    }

    /// <summary>
    /// 万華鏡。色が派手で、やたら陽気で、途中で急に裏返る。
    /// </summary>
    private static TripPattern BuildKaleidoscopePattern()
    {
        return new TripPattern("Kaleidoscope", 14f,
        [
            // 開花。色付きの文字が散り始める。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.28f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.3f,
                NoiseSpread = 540f,
                ChurnLines = 4,
                ColorRunLength = 3,
                CorruptChance = 0.04f,
                FillRatio = 0.5f,
                BurstChance = 0.1f,
                BurstPool = [BurstKind.Zoom, BurstKind.Swarm, BurstKind.Split],
                MessageFontSize = 46,
                MessageInterval = 0.8f,
                MessageBlankChance = 0.25f,
                MessageColorRunLength = 2,
                MessageWander = 220f,
                MessageStyleChance = 0.4f,
                RoamerCount = 8,
                RoamerSpeed = 700f,
                RoamerFontSize = 32,
                RoamerColorRunLength = 2,
                RoamerMessages = EuphoriaMessages,
                FogIntensity = (byte)(FogType.BecomingFlamingo + 1),
                FlickerChance = 0.1f,
                FogFlickerChance = 0.2f,
                Messages = EuphoriaMessages,
            },

            // 乱舞。跳ね回る文字が画面を埋める。
            new TripPhase
            {
                StartRatio = 0.3f,
                Interval = 0.15f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.35f,
                NoiseSpread = 570f,
                ChurnLines = 10,
                ColorRunLength = 2,
                CorruptChance = 0.12f,
                Shake = 26f,
                BurstChance = 0.32f,
                BurstPool = [BurstKind.Swarm, BurstKind.Zoom, BurstKind.Split, BurstKind.Flood],
                MessageFontSize = 66,
                MessageInterval = 0.24f,
                MessageBlankChance = 0.2f,
                MessageCorruptChance = 0.06f,
                MessageColorRunLength = 1,
                MessageWander = 300f,
                MessageStyleChance = 0.55f,
                RoamerCount = 14,
                RoamerSpeed = 1350f,
                RoamerFontSize = 38,
                RoamerColorRunLength = 1,
                RoamerRespawnChance = 0.18f,
                RoamerMessages = EuphoriaMessages,
                FogIntensity = (byte)(FogType.Scp244 + 1),
                SinkholeIntensity = 200,
                FlickerChance = 0.28f,
                FogFlickerChance = 0.4f,
                Messages = EuphoriaMessages,
            },

            // 裏返り。同じ色のまま、内容だけが変わる。
            new TripPhase
            {
                StartRatio = 0.62f,
                Interval = 0.12f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.6f,
                NoiseSpread = 570f,
                ChurnLines = 12,
                ColorRunLength = 2,
                CorruptChance = 0.4f,
                Shake = 44f,
                BurstChance = 0.42f,
                MessageFontSize = 84,
                MessageInterval = 0.15f,
                MessageBlankChance = 0.22f,
                MessageCorruptChance = 0.24f,
                MessageColorRunLength = 1,
                MessageWander = 300f,
                MessageStyleChance = 0.6f,
                RoamerCount = 14,
                RoamerSpeed = 1600f,
                RoamerFontSize = 42,
                RoamerColorRunLength = 1,
                RoamerRespawnChance = 0.24f,
                RoamerMessages = BodyHorrorMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.42f,
                FogFlickerChance = 0.45f,
                Messages = BodyHorrorMessages,
            },

            // 色が抜ける。
            new TripPhase
            {
                StartRatio = 0.9f,
                Interval = 0.4f,
                ClearNoise = true,
                MessageFontSize = 46,
                MessageInterval = 1.6f,
                MessageBlankChance = 0.3f,
                MessageColor = "#7d7d7d",
                MessageWander = 50f,
                BlindOnEnter = true,
                FogIntensity = (byte)(FogType.Amnesia + 1),
                Messages = SilenceMessages,
            },
        ]);
    }

    /// <summary>
    /// 白飛び。文字は少ないが、フラッシュと暗転が止まらない。
    /// </summary>
    private static TripPattern BuildWhiteoutPattern()
    {
        return new TripPattern("Whiteout", 12f,
        [
            // 明滅の始まり。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.32f,
                ClearNoise = true,
                MessageFontSize = 120,
                MessageInterval = 0.5f,
                MessageBlankChance = 0.5f,
                MessageColorRunLength = 2,
                MessageWander = 260f,
                MessageStyleChance = 0.4f,
                BurstChance = 0.12f,
                BurstPool = [BurstKind.Blackout, BurstKind.Wall],
                RoamerCount = 4,
                RoamerSpeed = 1100f,
                RoamerFontSize = 60,
                RoamerMessages = StaticMessages,
                FogIntensity = (byte)(FogType.Decontamination + 1),
                FlickerChance = 0.3f,
                FogFlickerChance = 0.1f,
                Messages = StaticMessages,
            },

            // 焼き付き。白と黒の間に文字が挟まる。
            new TripPhase
            {
                StartRatio = 0.3f,
                Interval = 0.18f,
                Layout = NoiseLayout.Ticker,
                LineWidthScale = 0.35f,
                NoiseSpread = 600f,
                ScrollSpeed = 2200f,
                ChurnLines = 6,
                ColorRunLength = 30,
                CorruptChance = 0.3f,
                FillRatio = 0.4f,
                Shake = 30f,
                BurstChance = 0.3f,
                BurstPool = [BurstKind.Wall, BurstKind.Blackout, BurstKind.Zoom, BurstKind.Sweep],
                MessageFontSize = 150,
                MessageInterval = 0.22f,
                MessageBlankChance = 0.42f,
                MessageCorruptChance = 0.2f,
                MessageColorRunLength = 1,
                MessageWander = 280f,
                MessageStyleChance = 0.5f,
                RoamerCount = 8,
                RoamerSpeed = 1700f,
                RoamerFontSize = 64,
                RoamerRespawnChance = 0.25f,
                RoamerMessages = StaticMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 200,
                FlashOnEnter = true,
                FlickerChance = 0.55f,
                FogFlickerChance = 0.3f,
                Messages = CountdownMessages,
            },

            // 全白。ほぼ何も見えない。
            new TripPhase
            {
                StartRatio = 0.66f,
                Interval = 0.14f,
                Layout = NoiseLayout.Column,
                LineWidthScale = 0.45f,
                NoiseSpread = 520f,
                ChurnLines = 10,
                ColorRunLength = 26,
                CorruptChance = 0.5f,
                FillRatio = 0.7f,
                Shake = 40f,
                BurstChance = 0.48f,
                BurstPool = [BurstKind.Wall, BurstKind.Blackout, BurstKind.Flood, BurstKind.Swarm],
                MessageFontSize = 170,
                MessageInterval = 0.16f,
                MessageBlankChance = 0.4f,
                MessageCorruptChance = 0.3f,
                MessageColorRunLength = 1,
                MessageWander = 300f,
                MessageStyleChance = 0.4f,
                RoamerCount = 12,
                RoamerSpeed = 1900f,
                RoamerFontSize = 70,
                RoamerRespawnChance = 0.3f,
                RoamerMessages = StaticMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                Deafen = true,
                FlickerChance = 0.65f,
                FogFlickerChance = 0.4f,
                Messages = StaticMessages,
            },

            // 焼き切れ。
            new TripPhase
            {
                StartRatio = 0.92f,
                Interval = 0.5f,
                ClearNoise = true,
                MessageFontSize = 40,
                MessageInterval = 2.2f,
                MessageBlankChance = 0.2f,
                MessageColor = "#ffffff",
                BlindOnEnter = true,
                Deafen = true,
                FogIntensity = (byte)(FogType.BecomingFlamingo + 1),
                Messages = SilenceMessages,
            },
        ]);
    }

    /// <summary>
    /// 合唱。ノイズ層は薄いが、無数の声が画面中を飛び交って一斉に喋る。
    /// </summary>
    private static TripPattern BuildChoirPattern()
    {
        return new TripPattern("Choir", 12f,
        [
            // ひとり。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.34f,
                ClearNoise = true,
                MessageFontSize = 34,
                MessageInterval = 1.4f,
                MessageBlankChance = 0.45f,
                MessageColorRunLength = 30,
                MessageWander = 150f,
                MessageStyleChance = 0.15f,
                BurstChance = 0.04f,
                BurstPool = [BurstKind.Swarm],
                RoamerCount = 3,
                RoamerSpeed = 420f,
                RoamerFontSize = 26,
                RoamerColorRunLength = 12,
                RoamerMessages = WhisperMessages,
                FogIntensity = (byte)(FogType.Amnesia + 1),
                FogFlickerChance = 0.04f,
                Messages = WhisperMessages,
            },

            // ふえる。
            new TripPhase
            {
                StartRatio = 0.24f,
                Interval = 0.20f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.25f,
                NoiseSpread = 550f,
                ChurnLines = 4,
                ColorRunLength = 10,
                CorruptChance = 0.1f,
                FillRatio = 0.3f,
                Shake = 10f,
                BurstChance = 0.18f,
                BurstPool = [BurstKind.Swarm, BurstKind.Swarm, BurstKind.Zoom, BurstKind.Split],
                MessageFontSize = 48,
                MessageInterval = 0.5f,
                MessageBlankChance = 0.3f,
                MessageCorruptChance = 0.05f,
                MessageWander = 240f,
                MessageStyleChance = 0.35f,
                RoamerCount = 9,
                RoamerSpeed = 820f,
                RoamerFontSize = 30,
                RoamerRespawnChance = 0.12f,
                RoamerMessages = ParanoiaMessages,
                FogIntensity = (byte)(FogType.Scp244 + 1),
                TraumatizedIntensity = 120,
                FlickerChance = 0.14f,
                FogFlickerChance = 0.12f,
                Messages = ParanoiaMessages,
            },

            // 全員がしゃべる。
            new TripPhase
            {
                StartRatio = 0.52f,
                Interval = 0.12f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.3f,
                NoiseSpread = 570f,
                ChurnLines = 8,
                ColorRunLength = 8,
                CorruptChance = 0.28f,
                FillRatio = 0.55f,
                Shake = 32f,
                BurstChance = 0.4f,
                BurstPool = [BurstKind.Swarm, BurstKind.Swarm, BurstKind.Zoom, BurstKind.Sweep, BurstKind.Blackout],
                MessageFontSize = 70,
                MessageInterval = 0.16f,
                MessageBlankChance = 0.25f,
                MessageCorruptChance = 0.18f,
                MessageColorRunLength = 4,
                MessageWander = 300f,
                MessageStyleChance = 0.55f,
                RoamerCount = 14,
                RoamerSpeed = 1400f,
                RoamerFontSize = 36,
                RoamerRespawnChance = 0.24f,
                RoamerColorRunLength = 3,
                RoamerMessages = MemoryMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 220,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.38f,
                FogFlickerChance = 0.3f,
                Messages = MemoryMessages,
            },

            // 一斉に黙る。
            new TripPhase
            {
                StartRatio = 0.88f,
                Interval = 0.42f,
                ClearNoise = true,
                MessageFontSize = 42,
                MessageInterval = 1.8f,
                MessageBlankChance = 0.28f,
                MessageColor = "#8b8b8b",
                MessageWander = 40f,
                Deafen = true,
                BlindOnEnter = true,
                FogIntensity = (byte)(FogType.Amnesia + 1),
                Messages = SilenceMessages,
            },
        ])
        {
            Cycles = 2,
        };
    }

    /// <summary>
    /// 再発。SCP-500 を飲んだときだけ入る専用パターン。
    /// 立ち上がりが無く、最初から最大強度で、静寂も来ない。
    /// <see cref="TripPattern.Weight"/> が 0 なので通常抽選には出ない。
    /// </summary>
    private static TripPattern BuildRelapsePattern()
    {
        return new TripPattern("Relapse", 0f,
        [
            // 開幕から全開。
            new TripPhase
            {
                StartRatio = 0f,
                Interval = 0.11f,
                Layout = NoiseLayout.Scatter,
                LineWidthScale = 0.6f,
                NoiseSpread = 580f,
                ChurnLines = 14,
                ColorRunLength = 12,
                CorruptChance = 0.44f,
                Shake = 44f,
                BurstChance = 0.46f,
                MessageFontSize = 86,
                MessageInterval = 0.13f,
                MessageBlankChance = 0.2f,
                MessageCorruptChance = 0.26f,
                MessageColorRunLength = 3,
                MessageWander = 300f,
                MessageStyleChance = 0.6f,
                RoamerCount = 14,
                RoamerSpeed = 1650f,
                RoamerFontSize = 44,
                RoamerRespawnChance = 0.24f,
                RoamerColorRunLength = 2,
                RoamerMessages = RoamerShortMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                FlashOnEnter = true,
                FlickerChance = 0.5f,
                FogFlickerChance = 0.4f,
                Messages = Scp500Messages,
            },

            // 身体の方が壊れ始める。
            new TripPhase
            {
                StartRatio = 0.34f,
                Interval = 0.10f,
                Layout = NoiseLayout.Diagonal,
                LineWidthScale = 0.5f,
                NoiseSpread = 600f,
                ScrollSpeed = 2600f,
                ChurnLines = 16,
                ColorRunLength = 10,
                CorruptChance = 0.5f,
                Shake = 50f,
                BurstChance = 0.52f,
                BurstPool = [BurstKind.Sweep, BurstKind.Wall, BurstKind.Split, BurstKind.Swarm, BurstKind.Flood, BurstKind.Corrupt],
                MessageFontSize = 96,
                MessageInterval = 0.11f,
                MessageBlankChance = 0.18f,
                MessageCorruptChance = 0.3f,
                MessageColorRunLength = 2,
                MessageWander = 300f,
                MessageStyleChance = 0.65f,
                RoamerCount = 14,
                RoamerSpeed = 1850f,
                RoamerFontSize = 46,
                RoamerRespawnChance = 0.28f,
                RoamerColorRunLength = 2,
                RoamerMessages = BodyHorrorMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                Deafen = true,
                FlickerChance = 0.55f,
                FogFlickerChance = 0.45f,
                Messages = BodyHorrorMessages,
            },

            // 秒読み。終わりは来ない。
            new TripPhase
            {
                StartRatio = 0.7f,
                Interval = 0.09f,
                Layout = NoiseLayout.Column,
                LineWidthScale = 0.5f,
                NoiseSpread = 540f,
                ChurnLines = 18,
                ColorRunLength = 8,
                CorruptChance = 0.6f,
                Shake = 56f,
                BurstChance = 0.58f,
                MessageFontSize = 130,
                MessageInterval = 0.1f,
                MessageBlankChance = 0.16f,
                MessageCorruptChance = 0.34f,
                MessageColorRunLength = 2,
                MessageWander = 300f,
                MessageStyleChance = 0.55f,
                RoamerCount = 14,
                RoamerSpeed = 2100f,
                RoamerFontSize = 50,
                RoamerRespawnChance = 0.32f,
                RoamerColorRunLength = 1,
                RoamerMessages = CountdownMessages,
                FogIntensity = 255,
                TraumatizedIntensity = 255,
                SinkholeIntensity = 255,
                Deafen = true,
                FlashOnEnter = true,
                FlickerChance = 0.6f,
                FogFlickerChance = 0.5f,
                Messages = CountdownMessages,
            },
        ])
        {
            Cycles = 2,
            SpeedScale = 0.95f,
        };
    }
}
