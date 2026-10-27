using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// SCP 側のカスタム役職の見本です。SCP チャットを近接ボイスとして流します。
/// </summary>
public sealed class SampleScpRole : CustomRole
{
    public override string Name => "Sample SCP";

    public override string Description => "動作確認用の SCP 役職です。";

    public override CustomTeam Team => CustomTeam.Get<SampleScpTeam>();

    /// <summary>SCP チャットを持つ土台。近接ボイスにはこれが要ります。</summary>
    public override RoleTypeId BaseRole => RoleTypeId.Scp049;

    public override float? MaxHealth => 2000f;

    public override float? MaxHumeShield => 1000f;

    public override string CustomInfo => "Sample SCP";

    /// <summary>
    /// 声の扱いは役職が名乗ります。配線側 (VoiceRoutingApi / ProximityChat) は触りません。
    /// </summary>
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();

}

/// <summary>
/// <see cref="SampleScpRole"/> が属する陣営の見本です。
/// </summary>
public sealed class SampleScpTeam : CustomTeam
{
    public override string Name => "Sample SCP";

    public override string Color => ServerColors.Red;

    public override string Objective => "動作確認用の SCP 陣営です。";

    public override VictoryCondition Victory => VictoryCondition.LastStanding(priority: 2);

    protected override bool IncludesVanilla(Player player) => false;
}
