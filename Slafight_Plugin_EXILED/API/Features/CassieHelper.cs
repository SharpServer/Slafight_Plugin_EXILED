using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features;

public static class CassieHelper
{
    public static void AnnounceNtfArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"MtfUnit Epsilon 11 Designated Ninetailedfox HasEntered AllRemaining",
            $"<color=#5bc5ff>機動部隊Epsilon-11 \"九尾狐\"</color>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。",
            true);
    }

    public static void AnnounceNtfBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "Ninetailedfox Backup unit has entered the facility .",
            "<color=#5bc5ff>九尾狐 予備部隊</color>が施設に到着しました。",
            true);
    }
    
    public static void AnnounceHdArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"MtfUnit Nu 7 Designated Her man down HasEntered AllRemaining This Forces Work Epsilon 11 Task and operated by O5 Command . for Big Containment Breachs .",
            $"<b><color=#353535>機動部隊Nu-7 \"下される鉄槌 - ハンマーダウン\"</color></b>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。" +
            $"<split>本部隊は<color=#5bc5ff>Epsilon-11 \"九尾狐\"</color>の任務の代替として大規模な収容違反の対応の為O5評議会に招集されました。",
            true);
    }

    public static void AnnounceHdBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "Her man down Backup unit has entered the facility .",
            "<b><color=#353535>下される鉄槌 - ハンマーダウン 予備部隊</color></b>が施設に到着しました。",
            true);
    }
    
    public static void AnnounceSneArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"MtfUnit Eta 10 designated See no E be l HasEntered AllRemaining . This forces work for the anti- me mu termination",
            $"<color=#FF1493>機動部隊Eta-10 \"シー・ノー・イーヴル\"</color>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。<split>" +
            $"この部隊は反ミーム存在の終了の為に招集されました。",
            true);
    }

    public static void AnnounceSneBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "See no E be l Backup unit has entered the facility .",
            "<color=#FF1493>シー・ノー・イーヴル 予備部隊</color>が施設に到着しました。",
            true);
    }

    public static void AnnounceLwsArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "MtfUnit Omega 1 designated Low Left Hand HasEntered AllRemaining .",
            $"<color={ServerColors.Silver}><b>機動部隊Omega-1 \"Law's Left Hand\"</color>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。",
            true);
    }

    public static void AnnounceLwsBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "Low Left Hand Backup unit has entered the facility .",
            $"<color={ServerColors.Silver}><b>Law's Left Hand 予備部隊</color>が施設に到着しました。",
            true);
    }
    
    public static void AnnounceRrhArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "MtfUnit Alpha 1 designated Red Right Hand HasEntered AllRemaining .",
            $"<color={ServerColors.Red}><b>機動部隊Alpha-1 \"Red Right Hand\"</color>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。",
            true);
    }

    public static void AnnounceRrhBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "Red Right Hand Backup unit has entered the facility .",
            $"<color={ServerColors.Red}><b>Red Right Hand 予備部隊</color>が施設に到着しました。",
            true);
    }
    
    public static void AnnounceChaos(int count)
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"Attention All personnel . Detected {count} Chaos Insurgency Forces in Gate A . Please Terminate Them",
            $"全職員に通達。Gate Aに{count}人の<color=#228b22>カオス・インサージェンシー</color>部隊が検出されました。<split>見つけ次第終了してください。",
            true);
    }

    public static void AnnounceFifthist(int count)
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"Attention All personnel . Detected {count} $pitch_1.05 5 5 5 $pitch_1 Forces in Gate B . Please Terminate Them",
            $"全職員に通達。Gate Bに{count}人の<color=#ff0090>第五主義者</color>が検出されました。<split>見つけ次第終了してください。",
            true);
    }
    
    public static void AnnounceLastOperationArrival()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"MtfUnit Alpha 0 designated Last Operation HasEntered AllRemaining .",
            $"<color=red>機動部隊Alpha-0 \"最終指令\"</color>が施設に到着しました。残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。",
            true);
    }

    public static void AnnounceLastOperationBackup()
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            "Last Operation Backup unit has entered the facility .",
            "<color=red>最終指令 予備部隊</color>が施設に到着しました。",
            true);
    }
    
    public static void AnnounceGoCEnter(int count)
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"Attention All personnel . Detected {count} G o C Forces in Gate B . Please Terminate Them",
            $"全職員に通達。Gate Bに{count}人の<b><color=#0000c8>世界オカルト連合</color></b>部隊が検出されました。<split>見つけ次第終了してください。",
            true);
    }
    
    // =================================== //
    public static void AnnounceSecurityTeamEnter(int count)
    {
        Exiled.API.Features.Cassie.MessageTranslated(
            $"Attention All personnel . Security Team has entered the facility .",
            $"全職員に通達。<color={CTeam.Guards.GetTeamColor()}>保安部隊</color>が施設に到着しました。",
            true);
    }
    
    // =================================== //

    public static void AnnounceTermination(DyingEventArgs ev, string targetCassie, string targetTranslated, bool clearCassie = false)
    {
        if (ev.Player == null) return;
        if (clearCassie) Exiled.API.Features.Cassie.Clear();
        if (ev.Attacker != null)
        {
            var info = ev.Attacker.GetTeam().GetTeamInfo();
            Exiled.API.Features.Cassie.MessageTranslated(
                $"{targetCassie} contained successfully by {info.CassieString}", 
                $"{targetTranslated} は、<color={info.TeamColor}>{info.TeamName}</color>によって正常に収容されました。",
                true);
        }
        else
        {
            Exiled.API.Features.Cassie.MessageTranslated(
                $"{targetCassie} successfully terminated. Termination cause unspecified.", 
                $"{targetTranslated} は、不明な原因によって終了されました。",
                true);
        }
    }
}
