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
    private const string AlphaObj = "<b>秩序のために、必要な犠牲を厭わず施設を掌握せよ。</b>";
    private const string OmegaObj = "<b>法のために、秩序の暴走を許さず施設を守護せよ。</b>";

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
        [CRoleTypeId.Scp079]       = ("<color=#c50000>SCP-079</color>",            ScpTeam, "施設制御システムを操り、施設に混沌を引き起こせ。"),
        [CRoleTypeId.Scp173]       = ("<color=#c50000>SCP-173</color>",            ScpTeam, "一瞬の隙を突き、財団職員共をへし折れ！"),
        [CRoleTypeId.Scp610]       = ("<color=#c50000>SCP-610</color>",            ScpTeam, "生存者を探し出し、施設をにくで埋め尽くせ"),

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
        [CRoleTypeId.ChaosSniper] = ("<color=#228b22>Chaos Insurgency Sniper</color>",ChaosTeam, ChaosObj),

        // ── Foundation Forces ─────────────────────────────────────────────
        [CRoleTypeId.NtfLieutenant]  = ("<color=#00b7eb>MTF E-11: Lieutenant</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.NtfGeneral]     = ("<color=blue>MTF E-11: General</color>",           FoundTeam, FoundObj),
        [CRoleTypeId.NtfSpecialist]  = ("<color=#00b7eb>MTF E-11: Specialist</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.NtfDetainer]    = ("<color=#00b7eb>MTF E-11: Detainer</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.NtfFieldMedic]  = ("<color=#00b7eb>MTF E-11: Field Medic</color>",    FoundTeam, FoundObj),
        [CRoleTypeId.HdInfantry]     = ("<color=#353535>MTF Nu-7: Infantry</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.HdShotgunner]   = ("<color=#353535>MTF Nu-7: Shotgunner</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.HdDisarmer]     = ("<color=#353535>MTF Nu-7: Disarmer</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.HdShielder]     = ("<color=#353535>MTF Nu-7: Shielder</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.HdCommander]    = ("<color=#252525>MTF Nu-7: Commander</color>",      FoundTeam, FoundObj),
        [CRoleTypeId.HdMarshal]      = ("<color=#151515>MTF Nu-7: Marshal</color>",        FoundTeam, FoundObj),
        [CRoleTypeId.SnePurify]      = ("<color=#FF1493>MTF Eta-10: Purify</color>",       FoundTeam, FoundObj),
        [CRoleTypeId.SneNeutralitist]= ("<color=#FF1493>MTF Eta-10: Neutralitist</color>", FoundTeam, FoundObj),
        [CRoleTypeId.SneGears]       = ("<color=#FF1493>MTF Eta-10: Gears</color>",        FoundTeam, FoundObj),
        [CRoleTypeId.SneOperator]    = ("<color=#FF1493>MTF Eta-10: Operator</color>",     FoundTeam, FoundObj),
        [CRoleTypeId.AraOrun]        = ("<color=#ffff00>MTF Omega-0: Ará Orún</color>",    FoundTeam, "マリオン・ホイーラーを手助けし、反ミーム爆弾へと導け！"),
        [CRoleTypeId.LwsJudgement]   = ($"<color={ServerColors.Silver}><b>MTF Omega-1: Judgement</b></color>",   FoundTeam + " - " + GoCTeam, OmegaObj),
        [CRoleTypeId.LwsLiaison]     = ($"<color={ServerColors.Silver}><b>MTF Omega-1: Liaison</b></color>",     FoundTeam + " - " + GoCTeam, OmegaObj),
        [CRoleTypeId.LwsForensic]    = ($"<color={ServerColors.Silver}><b>MTF Omega-1: Forensic</b></color>",    FoundTeam + " - " + GoCTeam, OmegaObj),
        [CRoleTypeId.LwsAgent]       = ($"<color={ServerColors.Silver}><b>MTF Omega-1: Agent</b></color>",       FoundTeam + " - " + GoCTeam, OmegaObj),
        [CRoleTypeId.RrhWarden]      = ($"<color={ServerColors.Red}><b>MTF Alpha-1: Warden</b></color>",      FoundTeam, AlphaObj),
        [CRoleTypeId.RrhEnforcer]    = ($"<color={ServerColors.Red}><b>MTF Alpha-1: Enforcer</b></color>",    FoundTeam, AlphaObj),
        [CRoleTypeId.RrhAegis]       = ($"<color={ServerColors.Red}><b>MTF Alpha-1: Aegis</b></color>",       FoundTeam, AlphaObj),
        [CRoleTypeId.RrhAssaulter]   = ($"<color={ServerColors.Red}><b>MTF Alpha-1: Assaulter</b></color>",   FoundTeam, AlphaObj),

        // ── Guards ────────────────────────────────────────────────────────
        [CRoleTypeId.EvacuationGuard] = ("<color=#00b7eb>Emergency Evacuation Guard</color>", FoundTeam, "職員達を上部階層へ避難させ、施設の秩序を守護せよ。"),
        [CRoleTypeId.SecurityChief]   = ("<color=#00b7eb>Security Chief</color>",             FoundTeam, "職員達を地上へ脱出させ、施設の秩序を守護せよ。"),
        [CRoleTypeId.ChamberGuard]    = ("<color=#00b7eb>Chamber Guard</color>",              FoundTeam, "Dクラスとオブジェクトに注意し、確実に職員達を避難させよ。"),
        [CRoleTypeId.SupplyManager]   = ("<color=#00b7eb>Supply Manager</color>",             FoundTeam, "施設内に向かい警備員たちと合流し、備品と搬入口の管理を遂行せよ。"),

        // ── Scientists / Neutral-Foundation ───────────────────────────────
        [CRoleTypeId.ZoneManager]    = ("<color=#00ffff>Zone Manager</color>",      NeutFoundTeam, "施設からの脱出を目指しながら、警備職員達を監督せよ"),
        [CRoleTypeId.FacilityManager]= ("<color=#dc143c>Facility Manager</color>",  NeutFoundTeam, "施設からの脱出を目指しながら、サイトの行く末を監督せよ"),
        [CRoleTypeId.Engineer]       = ("<color=#faff86>Engineer</color>",          NeutFoundTeam, "様々なタスクをこなし、最強の弾頭を起動せよ！"),
        [CRoleTypeId.SiteNavigator]  = ("<color=#faff86>Site Navigator</color>",    NeutFoundTeam, "S-NAVを活用し、施設から脱出せよ。"),
        [CRoleTypeId.ObjectObserver] = ("<color=#faff86>Object Observer</color>",   NeutFoundTeam, "オブジェクトに注意しながら、施設から脱出せよ。"),
        [CRoleTypeId.Surveillance]   = ("<color=#faff86>Surveillance</color>",      NeutFoundTeam, "施設の状況を監視し、脱出の機会を見極めよ。"),
        [CRoleTypeId.CandyResearcher]= ("<color=#faff86>Candy Researcher</color>",  NeutFoundTeam, "キャンディーを活用しながら、施設から脱出せよ。"),
        [CRoleTypeId.MarionWheeler]  = ("<color=#ffa500>Marion Wheeler</color>",    NeutFoundTeam, "第五の目を搔い潜り、反ミーム爆弾を起爆しろ！"),

        // ── Class-D / Neutral-Chaos ───────────────────────────────────────
        [CRoleTypeId.Janitor]      = ("<color=#ee7600>Janitor</color>",       NeutChaosTeam, "施設から脱出せよ。また、汚物をグレネードで清掃せよ。"),
        [CRoleTypeId.CandySubject] = ("<color=#ee7600>Candy Subject</color>", NeutChaosTeam, "キャンディーを活用しながら、施設から脱出せよ。"),

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
        [CRoleTypeId.HideAdmin] = (
            "<color=#FF1493><b>THE ADMINISTRATOR</b></color>",
            "<color=#FF1493>THE ADMINISTRATOR</color>",
            "なぁ～んでもできる！"),
        [CRoleTypeId.HideWatch] = (
                $"<color={ServerColors.Cyan}><b>THE HIDEWATCH</b></color>",
                "<color=#FF1493>THE ADMINISTRATOR</color>",
                "ぐへへへへ"),
        
        // ── Experimental Features ─────────────────────────────────────────
        [CRoleTypeId.SecurityTeamGuard] = ("<color=#00b7eb>Security Team Guard</color>", FoundTeam, FoundObj),
        [CRoleTypeId.ChaosIntruder] = ("<color=#228b22>Chaos Insurgency Intruder</color>",ChaosTeam, ChaosObj),
    };
}
