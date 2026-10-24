using PlayerRoles;
using Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp049 : CustomRole
{
    // SCP: Containment Breachの台詞を日本語化。cure/悪疫の語感はSCP財団日本語版に合わせる。
    // 出典・翻訳クレジット: docs/internal/communications.md
    private static readonly CommunicationMenu DoctorMenu = new(new[]
    {
        CannedChatNode.Category("遭遇",
            CannedChatNode.Phrase("挨拶", "ごきげんよう……。", "遭遇"),
            CannedChatNode.Phrase("来客", "おや、来客とは気づかなかった。ここを訪れる者は滅多にいないのでね。", "遭遇"),
            CannedChatNode.Phrase("医者", "君は医者ではないな……。", "遭遇")),
        CannedChatNode.Category("悪疫",
            CannedChatNode.Phrase("犠牲者", "おや、また悪疫の犠牲者か。", "悪疫"),
            CannedChatNode.Phrase("感染", "君の中に悪疫を感じる。", "悪疫"),
            CannedChatNode.Phrase("気配", "ここには悪疫がある。私には感覚でわかるんだ。", "悪疫")),
        CannedChatNode.Category("救済",
            CannedChatNode.Phrase("救済", "私こそが救済だ。", "救済"),
            CannedChatNode.Phrase("恐れるな", "恐れることはない。私こそが救済だ。", "救済"),
            CannedChatNode.Phrase("治療", "私の治療はこの上なく効果的なのだよ。", "救済")),
        CannedChatNode.Category("追跡",
            CannedChatNode.Phrase("抵抗", "抵抗はやめたまえ。君を治療しに来たのだ。", "追跡"),
            CannedChatNode.Phrase("隠れるな", "隠れる必要はない。私は君を助けに来たのだ。", "追跡"),
            CannedChatNode.Phrase("息遣い", "君の息遣いが聞こえるよ。", "追跡")),
    });

    public override CommunicationPolicy Communication => CommunicationPolicy.Enabled(
        prefix: "SCP-049",
        isRadioAvailable: false,
        menuMode: CommunicationMenuMode.Text,
        menu: DoctorMenu);

    public override string Name => "SCP-049";
    public override string Description => "悪疫を信じ人々を救済する医師。";
    public override string Objective => "全ての人々に悪疫からの救済を与えよ！";
    public override string CustomInfo => Name;
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp049;
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();
}
