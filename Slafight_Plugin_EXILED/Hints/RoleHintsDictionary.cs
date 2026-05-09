using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.Hints;

internal static class RoleHintsDictionary
{
    private const string ScpTeam       = "<color=#c50000>The SCPs</color>";
    private const string FoundTeam     = "<color=#00b7eb>The Foundation</color>";
    private const string ChaosTeam     = "<color=#228b22>Chaos Insurgency</color>";
    private const string FifthTeam     = "<color=#ff00fa>The Fifthists</color>";
    private const string GoCTeam       = "<color=#0000c8>Global Occult Coalition</color>";
    private const string NeutFoundTeam = "<color=#faff86>Neutral - Side Foundation</color>";
    private const string NeutChaosTeam = "<color=#ee7600>Neutral - Side Chaos</color>";

    private const string FoundObj = "研究員を救出し、施設の秩序を守護せよ。";
    private const string ChaosObj = "Dクラス職員を救出し、施設を略奪せよ。";
    private const string GoCObj   = "人類第一に、財団に抵抗せよ。";

    // (role text, team text, objective text)
    internal static readonly Dictionary<CRoleTypeId, (string Role, string Team, string Objective)> Table =
        new Dictionary<CRoleTypeId, (string, string, string)>
    {
        // ── SCPs ──────────────────────────────────────────────────────────
        [CRoleTypeId.Scp096Anger]  = ("<color=#c50000>SCP-096: ANGER</color>",    ScpTeam, "怒りに任せ、施設中で暴れまわれ！！！"),
        [CRoleTypeId.Scp3114]      = ("<color=#c50000>SCP-3114</color>",           ScpTeam, "皆に素敵なサプライズをして驚かせましょう！"),
        [CRoleTypeId.Scp966]       = ("<color=#c50000>SCP-966</color>",            ScpTeam, "背後から忍び寄り、奴らに恐怖を与えよ！"),
        [CRoleTypeId.Scp682]       = ("<color=#c50000>SCP-682</color>",            ScpTeam, "無敵の爬虫類の力を見せてやれ！！！"),
        [CRoleTypeId.Zombified]    = ("<color=#c50000>Zombified Subject</color>",  ScpTeam, "何らかの要因でゾンビの様になってしまった。とにかく暴れろ！"),
        [CRoleTypeId.Scp106]       = ("<color=#c50000>SCP-106</color>",            ScpTeam, "自身の欲求に従い、財団職員共を弄べ！"),
        [CRoleTypeId.Scp999]       = ("<color=#ff1493>SCP-999</color>",            ScpTeam, "可愛いペットとして施設を歩き回れ！　※勝敗に影響しません。良い感じに遊んでね！"),
        [CRoleTypeId.Scp035]       = ("<color=#c50000>SCP-035</color>",            ScpTeam, "あなたは仮面に乗っ取られ、精神が不安定になっている。<color=red>核弾頭を起動しろ</color>"),

        // ── Fifthists ─────────────────────────────────────────────────────
        [CRoleTypeId.Scp3005]            = ("<color=#ff00fa>SCP-3005</color>",              ScpTeam + " - " + FifthTeam, "第五教会に道を示し、施設を占領せよ"),
        [CRoleTypeId.Scp3125]            = ("<color=#ff00fa>SCP-3125</color>",              FifthTeam, "マリオン・ホイーラーを探し出し第五せよ"),
        [CRoleTypeId.FifthistRescure]    = ("<color=#ff00fa>Fifthist: Rescue</color>",      FifthTeam, "第五を探し出し、救出し、従い、施設を占領せよ。"),
        [CRoleTypeId.FifthistPriest]     = ("<color=#ff00fa>Fifthist: Priest</color>",      FifthTeam, "あなたは幸福な事に第五の加護を受けている。全てを第五せよ！"),
        [CRoleTypeId.FifthistConvert]    = ("<color=#ff5ffa>Fifthist: Convert</color>",     FifthTeam, "あなたは第五教会の新入りだ。第五とは何かについて考え、理解し、そして従いなさい。"),
        [CRoleTypeId.FifthistGuidance]   = ("<color=#ff00fa>Fifthist: Guidance</color>",    FifthTeam, "杖を用い、第五主義を施設に広めなさい。あなたの導きは教会にとって重要です！"),
        [CRoleTypeId.FifthistMarionette] = ("<color=#ff5ffa>Fifthist: Marionette</color>",  FifthTeam, "第五教会に従い、生存者どもを騙しながら第五しろ！"),

        // ── Chaos Insurgency ──────────────────────────────────────────────
        [CRoleTypeId.ChaosCommando]        = ("<color=#228b22>Chaos Insurgency Commando</color>",        ChaosTeam, ChaosObj),
        [CRoleTypeId.ChaosSignal]          = ("<color=#228b22>Chaos Insurgency Signal</color>",          ChaosTeam, ChaosObj),
        [CRoleTypeId.ChaosTacticalUnit]    = ("<color=#228b22>Chaos Insurgency Tactical Unit</color>",   ChaosTeam, ChaosObj),
        [CRoleTypeId.ChaosPenal]           = ("<color=#228b22>Chaos Insurgency Breaker</color>",         ChaosTeam, ChaosObj),
        [CRoleTypeId.ChaosUndercoverAgent] = ("<color=#228b22>Chaos Insurgency Undercover Agent</color>",ChaosTeam, ChaosObj),

        // ── Foundation Forces ─────────────────────────────────────────────
        [CRoleTypeId.NtfLieutenant]  = ("<color=#00b7eb>MTF E-11: Lieutenant</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.NtfGeneral]     = ("<color=blue>MTF E-11: General</color>",           FoundTeam, FoundObj),
        [CRoleTypeId.HdInfantry]     = ("<color=#353535>MTF Nu-7: Infantry</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.HdCommander]    = ("<color=#252525>MTF Nu-7: Commander</color>",      FoundTeam, FoundObj),
        [CRoleTypeId.HdMarshal]      = ("<color=#151515>MTF Nu-7: Marshal</color>",        FoundTeam, FoundObj),
        [CRoleTypeId.SnePurify]      = ("<color=#FF1493>MTF Eta-10: Purify</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.SneNeutralitist]= ("<color=#FF1493>MTF Eta-10: Neutralitist</color>", FoundTeam, FoundObj),
        [CRoleTypeId.SneGears]       = ("<color=#FF1493>MTF Eta-10: Gears</color>",        FoundTeam, FoundObj),
        [CRoleTypeId.SneOperator]    = ("<color=#FF1493>MTF Eta-10: Operator</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.AraOrun]        = ("<color=#ffff00>MTF Omega-0: Ará Orún</color>",    FoundTeam, "マリオン・ホイーラーを手助けし、反ミーム爆弾へと導け！"),

        // ── Guards ────────────────────────────────────────────────────────
        [CRoleTypeId.EvacuationGuard] = ("<color=#00b7eb>Emergency Evacuation Guard</color>", FoundTeam, "職員達を上部階層へ避難させ、施設の秩序を守護せよ。"),
        [CRoleTypeId.SecurityChief]   = ("<color=#00b7eb>Security Chief</color>",             FoundTeam, "職員達を地上へ脱出させ、施設の秩序を守護せよ。"),
        [CRoleTypeId.ChamberGuard]    = ("<color=#00b7eb>Chamber Guard</color>",              FoundTeam, "Dクラスとオブジェクトに注意し、確実に職員達を避難させよ。"),

        // ── Scientists / Neutral-Foundation ───────────────────────────────
        [CRoleTypeId.ZoneManager]    = ("<color=#00ffff>Zone Manager</color>",      NeutFoundTeam, "施設からの脱出を目指しながら、警備職員達を監督せよ"),
        [CRoleTypeId.FacilityManager]= ("<color=#dc143c>Facility Manager</color>",  NeutFoundTeam, "施設からの脱出を目指しながら、サイトの行く末を監督せよ"),
        [CRoleTypeId.Engineer]       = ("<color=#faff86>Engineer</color>",          NeutFoundTeam, "様々なタスクをこなし、最強の弾頭を起動せよ！"),
        [CRoleTypeId.ObjectObserver] = ("<color=#faff86>Object Observer</color>",   NeutFoundTeam, "オブジェクトに注意しながら、施設から脱出せよ。"),
        [CRoleTypeId.MarionWheeler]  = ("<color=#ffa500>Marion Wheeler</color>",    NeutFoundTeam, "第五の目を搔い潜り、反ミーム爆弾を起爆しろ！"),

        // ── Class-D / Neutral-Chaos ───────────────────────────────────────
        [CRoleTypeId.Janitor] = ("<color=#ee7600>Janitor</color>", NeutChaosTeam, "施設から脱出せよ。また、汚物をグレネードで清掃せよ。"),

        // ── GoC ───────────────────────────────────────────────────────────
        [CRoleTypeId.GoCOperative]      = ("<color=#0000c8>Broken Dagger: Operative</color>",     GoCTeam, GoCObj),
        [CRoleTypeId.GoCThaumaturgist]  = ("<color=#0000c8>Broken Dagger: Thaumaturgist</color>", GoCTeam, GoCObj),
        [CRoleTypeId.GoCCommunications] = ("<color=#0000c8>Broken Dagger: Communications</color>",GoCTeam, GoCObj),
        [CRoleTypeId.GoCMedic]          = ("<color=#0000c8>Broken Dagger: Medic</color>",         GoCTeam, GoCObj),
        [CRoleTypeId.GoCDeputy]         = ("<color=#0000c8>Broken Dagger: Deputy</color>",        GoCTeam, GoCObj),
        [CRoleTypeId.GoCSquadLeader]    = ("<color=#0000c8>Broken Dagger: Squad Leader</color>",  GoCTeam, GoCObj),
        [CRoleTypeId.GoCHoundDog]       = ("<color=#0000c8>Hound Dog: White Suit</color>",        GoCTeam, GoCObj),

        // ── Others ────────────────────────────────────────────────────────
        [CRoleTypeId.SnowWarrier] = (
            "<b><color=#ffffff>SNOW WARRIER</color></b>",
            "<b><color=#ffffff>SNOW WARRIER's DIVISION</color></b>",
            "全施設にクリスマスと雪玉の正義を執行しろ"),
        [CRoleTypeId.CandyWarrierApril] = (
            "<b><color=#ffffff>CANDY WARRIER</color></b>",
            "<b><color=#ffffff>CANDY WARRIER's DIVISION</color></b>",
            "全施設にFunnyなお菓子の正義を執行しろ"),
        [CRoleTypeId.CandyWarrierHalloween] = (
            "<b><color=#ffffff>CANDY WARRIER</color></b>",
            "<b><color=#ffffff>CANDY WARRIER's DIVISION</color></b>",
            "全施設にFunnyなお菓子の正義を執行しろ"),

        // ── Special ───────────────────────────────────────────────────────
        [CRoleTypeId.Sculpture] = (
            "<color=#00b7eb>Sculpture</color>", FoundTeam,
            "財団に従い、人類を根絶させよ。"),
        [CRoleTypeId.SergeyMakarov] = (
            "<color=#dc143c>Facility Manager - Sergey Makarov</color>",
            "<color=#faff86>The Foundation</color>",
            "持てる全てを使い、<color=#228b22><b>奴ら</b></color>への<color=red><b>復讐</b></color>を果たせ"),
        [CRoleTypeId.SergeyMakarovAwaken] = (
            "<color=red>Cursemaster - Sergey Makarov</color>",
            "<color=#a0a0a0>Alone</color>",
            "<color=red><b>邪魔者を滅ぼし、サイト-02から毒を浄化せよ</b></color>"),
        
        // ── Experimental Features ─────────────────────────────────────────
        [CRoleTypeId.SecurityTeamGuard] = ("<color=#00b7eb>Security Team Guard</color>", FoundTeam, FoundObj),
        [CRoleTypeId.ChaosIntruder] = ("<color=#228b22>Chaos Insurgency Intruder</color>",ChaosTeam, ChaosObj),
    };
}
