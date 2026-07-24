using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;

public sealed class AntiMemeProtocolFunction : FacilityControlRoomFunction
{
    public static bool IsActive { get; private set; }
    public static bool HasActivatedInPast { get; private set; }

    public override string Id => "AntiMemeProtocol";
    public override string DisplayName => "アンチミームプロトコル";
    public override string Description => "SCP-3005 / SCP-3125 に対する反ミーム性無力化処理を開始または停止する。";
    public override KeycardPermissions RequiredPermissions => KeycardPermissions.ContainmentLevelThree;

    public override void ResetState()
    {
        IsActive = false;
        HasActivatedInPast = false;
    }

    public override FacilityControlRoomFunctionResult Execute(FacilityControlRoomFunctionContext context)
    {
        return IsActive ? StopProtocol() : StartProtocol(context.Player);
    }

    private static FacilityControlRoomFunctionResult StartProtocol(Player player)
    {
        if (player.GetTeam() is CTeam.Fifthists)
        {
            return Failure("<size=24>アンチミームプロトコルの開始に失敗しました。\n第五教会は開始できません。</size>");
        }

        var targets = Player.List
            .Where(IsAntiMemeProtocolTarget)
            .ToList();

        if (targets.Count <= 0)
        {
            return Failure("<size=24>アンチミームプロトコルの開始に失敗しました。\n対象が見つかりませんでした。</size>");
        }

        foreach (var target in targets)
        {
            if (!HasActivatedInPast)
                target.Health = 10000;

            target.EnableEffect(EffectType.Poisoned, 255);
            target.EnableEffect(EffectType.Decontaminating, 255);
        }

        AnnounceStarted();
        IsActive = true;

        return Success("<size=24>アンチミームプロトコルを実行しました。</size>");
    }

    private static FacilityControlRoomFunctionResult StopProtocol()
    {
        foreach (var player in Player.List.Where(IsAntiMemeProtocolTarget))
        {
            player.DisableEffect(EffectType.Poisoned);
            player.DisableEffect(EffectType.Decontaminating);
        }

        if (Player.List.Any())
        {
            Exiled.API.Features.Cassie.MessageTranslated(
                "$pitch_.85 Anti- $pitch_1 Me mu Protocol Stopped .",
                $"<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコル</color>が停止されました。",
                false,
                false);
        }

        IsActive = false;
        return Success("<size=24>アンチミームプロトコルを停止しました。</size>");
    }

    private static void AnnounceStarted()
    {
        if (!HasActivatedInPast)
        {
            Exiled.API.Features.Cassie.MessageTranslated(
                "By order of Facility Manager Control Room , $pitch_.85 Anti- $pitch_1 Me mu Protocol Activated .",
                $"<color=#ff0087>施設管理者制御室</color>からの命令により、<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコロル</color>が有効化されました。エージェントにより反ミーム性物体の非活性化が開始されます。",
                true,
                false);
            HasActivatedInPast = true;
            return;
        }

        Exiled.API.Features.Cassie.MessageTranslated(
            "$pitch_.85 Anti- $pitch_1 Me mu Protocol Resumed .",
            $"<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコル</color>が再開されました。",
            false,
            false);
    }

    private static bool IsAntiMemeProtocolTarget(Player player)
    {
        return player.GetCustomRole() is CRoleTypeId.Scp3005 or CRoleTypeId.Scp3125;
    }
}
