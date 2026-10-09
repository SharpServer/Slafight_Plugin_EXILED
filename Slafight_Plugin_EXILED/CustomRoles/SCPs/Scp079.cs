using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

/// <summary>SCP-079の定型文通信表示を専用化したカスタム役職です。</summary>
public sealed class Scp079 : CustomRole
{
    public override string Name => "SCP-079";

    public override string CustomInfo => Name;

    public override string Description => "施設システムを通して周囲へ語りかけます。";

    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();

    public override RoleTypeId BaseRole => RoleTypeId.Scp079;

    /// <summary>
    /// SCP陣営では無効な定型文通信をこの役職だけ許可し、近接側の正体表記を隠します。
    /// </summary>
    public override CommunicationPolicy Communication =>
        CommunicationPolicy.Enabled(
            proximityPrefix: "???",
            isRadioAvailable: false,
            menuMode: CommunicationMenuMode.Text);
}
