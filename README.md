# Slafight_Plugin_EXILED

**Version:** `v1.8.0.2` | **Framework:** `EXILED v9.13.3` 

SCP: Secret LaboratoryのEXILEDフレームワークで動作する、自サーバー向けの大型拡張プラグインです。

完全にプライベートサーバー（自鯖）の運営・運用を前提として設計・開発されているため、コードの可読性や汎用性は全く考慮されておりません。しかしながら、日本発の最新版SCP:SL・EXILED開発資料が少ない現状を鑑みて、「誰かの参考になれば」「何かの役に立つかもしれない」という思いからオープンソースとして公開しています。

作者自身、C#については手探りで学んでいる部分が多く、実装に粗がある可能性がありますが、少しでも日本のSCP:SL開発コミュニティの助けになれれば幸いです。

---

## 📦 主な機能 (Features)
本プラグインは非常に多岐にわたる機能をサーバーに追加します。
- **カスタムロール (Custom Roles):** 独自のクラスや役職を追加
- **カスタムアイテム (Custom Items):** 特殊な効果やアビリティを持つアイテム
- **豊富なアビリティ (Abilities):** シンクホール(Sinkhole)、マジックミサイル(Magic Missile)などの固有能力
- **特殊イベント (Special Events):** オメガ弾頭(Omega Warhead)やデルタ弾頭(Delta Warhead)などのイベント発生と制御
- **近接チャット拡張 (Proximity Chat):** 限定的な音声コミュニケーションの調整
- **カスタムマップ・ギミック:** Terminal Rift、Pocket Dimension EX、SCP-3005モデリング対応など

---

## 📚 Wiki / GitHub Pages
ユーザー向けのロール、アイテム、特殊イベント、マップギミック一覧は [`docs/`](docs/) にGitHub Pages用Wikiとして整理しています。

- 公開設定: GitHub PagesのSourceを `main` / `/docs` に設定
- 更新方法: [`docs/maintenance.md`](docs/maintenance.md)
- データ編集: `docs/_data/*.yml` に追記すると一覧ページと検索に反映
- 既存資料: [`docs/scp914-keycard-flow.html`](docs/scp914-keycard-flow.html)

---

## ⚙️ 必須要件・前提条件 (Dependencies)
このプラグインを正常に動作させるためには、以下のプラグイン、ライブラリ、およびツールが必要です。

### 必須プラグイン・ライブラリ
- **EXILED** (v9.13.1以上推奨)
- **HSM (HintServiceMeow)**
- **0Harmony**
- **AudioPlayerApi**

### 作者製ツール・フォークリポジトリ
本プラグインの核となる機能は、以下の専用前提ツールに強く依存しています。かならず導入してください。
- 🔗 [**ProjectMER**](https://github.com/Slaviaaa2/ProjectMER) 

**配置例 (ディレクトリ構造):**
```text
[ポート番号]/
 ├─ HSM.dll

dependencies/
 ├─ 0Harmony.dll
 ├─ AudioPlayerApi.dll

SCP Secret Laboratory/LabAPI/plugins/[ポート番号]/
 ├─ ProjectMER.dll
 ├─ MEROptimizer.dll
 ├─ AdvancedMERtools.dll
```

---

## 🚀 インストール & 設定 (Installation & Configuration)
1. 上記の前提プラグインをすべて適切なディレクトリに配置します。
2. `Slafight_Plugin_EXILED.dll` を `%AppData%\EXILED\Plugins` (または各ポートのディレクトリ) に配置します。
3. サーバーを起動すると、Configファイルが以下の場所に生成されます。
   📄 `%AppData%\EXILED\Configs\Plugins\Slafight_Plugin_EXILED\[サーバーポート].yml`
4. 用途に合わせてConfigを調整してください。

---

## 🎧 BGM設定について (イベントBGM)
Omega Warhead や Delta Warhead などの大型イベント発生時、デフォルトの設定ではBGMは再生されません。
これらを再生させたい場合は、以下の手順に従ってください。
1. Configファイルにて、音楽ファイルを配置するフォルダパスを指定します。
2. 指定したフォルダ内に `omega.ogg`、`delta.ogg` などのファイル名で音声ファイル(ogg形式)を配置してください。

---

## 🏗️ カスタムマップ・モデルデータの読み込みについて
SCP-3005 や Pocked Dimension EX (PDEx) 等で使用される専用モデルや配置データをスポーンさせるためには、Config等で座標やデータを指定する必要があります。
- 配置先: `SCP Secret Laboratory/LabAPI/configs/ProjectMER/Maps` および `Schematics`
- **注意:** マップやモデルデータ（Schematics）が存在しない場合でも、内部でエラーログが出力されるだけで、サーバー自体がクラッシュすることはありません。ただしモデル自体は表示されません。

---

## 💬 連絡先・サポート (Contact)
導入に関する質問、バグ報告、またはその他のお問い合わせがありましたら、作者のX (旧Twitter) プロフィール等から直接ご連絡ください。

- **Author:** Slaviaaa_2
- **GitHub:** [Slaviaaa2](https://github.com/Slaviaaa2)
