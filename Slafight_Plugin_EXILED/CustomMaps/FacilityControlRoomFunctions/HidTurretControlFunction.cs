using Exiled.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

namespace Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;

public sealed class HidTurretControlFunction : FacilityControlRoomFunction
{
    private const float ActiveDurationSeconds = 120f;
    private const float RestartCooldownSeconds = 200f;

    public override string Id => "HidTurretControl";
    public override string DisplayName => "H.I.D Turretシステム";
    public override string Description =>
        HIDTurretObject.IsPowerEnabled
            ? $"H.I.D Turretを停止する。残り稼働時間：{HIDTurretObject.PowerRemainingSeconds:F0}秒"
            : HIDTurretObject.PowerRestartCooldownRemaining > 0f
                ? $"H.I.D Turretは再起動待機中。残り：{HIDTurretObject.PowerRestartCooldownRemaining:F0}秒"
                : $"H.I.D Turretを最大{ActiveDurationSeconds:F0}秒間起動する。再実行で途中停止できる。";

    public override KeycardPermissions RequiredPermissions => KeycardPermissions.AlphaWarhead;
    public override int Order => 30;

    public override void ResetState()
    {
        HIDTurretObject.ResetPowerState();
    }

    public override FacilityControlRoomFunctionResult Execute(FacilityControlRoomFunctionContext context)
    {
        if (HIDTurretObject.IsPowerEnabled)
        {
            HIDTurretObject.DisablePower(RestartCooldownSeconds);
            Exiled.API.Features.Cassie.MessageTranslated("Stopped H I D System. . . . .", $"HIDタレットシステムを停止しました。<split>再起動には{RestartCooldownSeconds:F0}秒必要です。");
            return Success(
                $"<size=24>HIDタレットシステムを停止しました。\n再起動には{RestartCooldownSeconds:F0}秒必要です。</size>");
        }

        float cooldownRemaining = HIDTurretObject.PowerRestartCooldownRemaining;
        if (cooldownRemaining > 0f)
        {
            return Failure(
                $"<size=24>HIDタレットシステムは再起動待機中です。\n残り {cooldownRemaining:F0} 秒</size>");
        }

        if (!HIDTurretObject.EnablePower(ActiveDurationSeconds))
        {
            return Failure(
                "<size=24>HIDタレットシステムを起動できません。\n稼働可能なタレットが存在しません。</size>");
        }

        Exiled.API.Features.Cassie.MessageTranslated("Started H I D System. . . . .", $"HIDタレットシステムを起動しました。<split>{ActiveDurationSeconds:F0}秒後に自動停止します。");
        return Success(
            $"<size=24>HIDタレットシステムを起動しました。\n{ActiveDurationSeconds:F0}秒後に自動停止します。</size>");
    }
}
