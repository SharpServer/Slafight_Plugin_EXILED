# 定型文通信のカテゴリと内容

`CommunicationMenu` は `CannedChatNode.Category` / `Phrase` / `Command` のツリーを検証・保持します。各階層は最大6件で、SCP-330と文字メニューの両方に対応します。登録時に配列をコピーするため、元配列を書き換えても表示や選択内容は変わりません。

```csharp
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;

private static readonly CommunicationMenu MyMenu = new(new[]
{
    CannedChatNode.Category("挨拶",
        CannedChatNode.Phrase("こんにちは", "こんにちは。", "挨拶")),
});

// CustomRole / CustomTeamのプロパティで設定する。
public override CommunicationPolicy Communication =>
    CommunicationPolicy.Enabled(menu: MyMenu);

// 接続中のプレイヤーへの最優先設定。退出・ラウンドリセットで消える。
CommunicationApi.SetPlayerPolicy(player, CommunicationPolicy.Inherit(menu: MyMenu));
CommunicationApi.RemovePlayerPolicy(player);

// UserId単位で設定。再接続・ラウンド変更後も有効。プラグイン終了時に消える。
CommunicationApi.SetUserPolicy("76561198000000000@steam", CommunicationPolicy.Inherit(menu: MyMenu));
CommunicationApi.RemoveUserPolicy("76561198000000000@steam");

// バニラロールへの設定も可能。
CommunicationApi.SetRolePolicy(PlayerRoles.RoleTypeId.Scientist, CommunicationPolicy.Inherit(menu: MyMenu));
CommunicationApi.RemoveRolePolicy(PlayerRoles.RoleTypeId.Scientist);
```

上書き順は **バニラ陣営 → バニラロール → CustomTeam → CustomRole → UserId → プレイヤー**。各フィールドがnullなら直前の設定を引き継ぎ、`menu`はカテゴリ・内容のツリー全体を置換します。全層で未指定なら従来の共通メニューを使います。空の `CommunicationMenu` を明示すれば選択メニューを利用できなくなります。

APIによる設定変更時は開いている対象メニューを閉じ、次に開いたときに新しい内容を表示します。ロール・陣営・共通メニューの設定変更時は全員のメニューを閉じます。選択中のセッションは開いたときのツリーを保持します。個人設定はコードによる登録であり、Server Specificsの編集画面やファイルへの永続化はありません。

CustomRoleの `Scp049` には「遭遇」「悪疫」「救済」「追跡」の4カテゴリ、12句を設定しています。Yで開き、能力オプション左右キーで選択、能力使用キーで決定、能力切り替えキーで戻ります。バニラSCP-049の既定値は変更していません。

## SCP-049台詞の出典

SCP: Containment Breachの台詞を日本語化し、`cure`はSCP財団日本語版の「救済」という表現を参考にしています。CBの公式日本語字幕をそのまま転載したものではありません。

- ゲーム: [SCP - Containment Breach](https://github.com/Regalis11/scpcb)、Regalis / SCP: Containment Breach contributors、CC BY-SA 3.0。
- CB台詞一覧: [SCP-049 / Quotes](https://containmentbreach.fandom.com/wiki/SCP-049#Quotes)。
- 日本語表現: [SCP-049](https://scp-jp.wikidot.com/scp-049)、Gabriel Jade、djkaktus、翻訳 wired990、CC BY-SA 3.0。
- 悪疫・治療の表現: [SCP-049-ARC](https://scp-jp.wikidot.com/scp-049-arc)、Gabriel Jade / SCP財団日本語版の翻訳寄稿者、CC BY-SA 3.0。

上記を基にした本項の台詞の日本語化・調整部分は [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) で提供します。
