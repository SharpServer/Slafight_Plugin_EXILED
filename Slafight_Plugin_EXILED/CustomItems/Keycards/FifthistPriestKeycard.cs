using Exiled.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Enums;

namespace Slafight_Plugin_EXILED.CustomItems.Keycards;

/// <summary>第五教会の司祭が持つ、より上位のキーカードです。</summary>
public sealed class FifthistPriestKeycard : FifthistKeycard
{
    public override string Name => "第五教会 司祭デバイス";
    protected override string ItemName => Name;
    protected override string Label => "PST. FIFTH";
    protected override KeycardPermissions Permissions => base.Permissions | KeycardPermissions.AlphaWarhead;
}
