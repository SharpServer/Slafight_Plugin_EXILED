using Exiled.API.Enums;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Keycards;

/// <summary>第五教会の司祭が持つ、核弾頭まで開けられるキーカードです。</summary>
public sealed class FifthistPriestKeycard : FifthistKeycard
{
    public override string Name => "第五教会 司祭デバイス";
    public override string Description => "第五教会が目的を達成するために作られた司祭用のアクセスデバイス。\n扉やゲートを第五することで施設に侵入する。";
    protected override string Label => "第五教会 司祭デバイス";
    protected override Color32? LabelColor => new(255, 0, 255, 255);
    protected override string HolderName => "Pst. Fifth";
    protected override Color32? TintColor => new(255, 0, 255, 255);
    protected override KeycardPermissions Permissions => base.Permissions | KeycardPermissions.AlphaWarhead;
}
