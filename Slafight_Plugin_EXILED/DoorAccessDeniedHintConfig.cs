using System.ComponentModel;

namespace Slafight_Plugin_EXILED;

/// <summary>
/// ドアのアクセス拒否 Hint の文言です。ポート別のプラグイン設定から変更できます。
/// </summary>
public sealed class DoorAccessDeniedHintConfig
{
    [Description("アクセス拒否 Hint 全体の書式。{0} に理由の文言が入ります。改行は実際の改行、または二重引用符で囲んで \\n と書いてください。")]
    public string MessageFormat { get; set; } =
        "<size=26><color=red>🔒️アクセス拒否🔒️</color>\n理由:{0}</size>";

    [Description("理由: ドアがロックされている場合")]
    public string LockedMessage { get; set; } = "このドアはロックされています";

    [Description("理由: 権限不足で、表示できる権限がない場合")]
    public string MissingPermissionsMessage { get; set; } = "権限不足";

    [Description("理由: 権限不足で権限一覧を表示する場合。{0} に権限一覧が入ります。")]
    public string MissingPermissionsWithPermissionsFormat { get; set; } = "権限不足({0})";

    [Description("理由: シークレットコードが違う場合")]
    public string IncorrectCodeMessage { get; set; } = "コードが違います";

    [Description("理由: 上記のいずれにも当てはまらない場合")]
    public string GenericMessage { get; set; } = "このドアは操作できません";

    [Description("持っている権限の前に付ける記号")]
    public string GrantedPermissionMark { get; set; } = "<color=green>✓</color>";

    [Description("持っていない権限の前に付ける記号")]
    public string DeniedPermissionMark { get; set; } = "<color=red>☓</color>";
}
