# Slafight Plugin EXILED

**Version:** `v1.9.0.0` | **Target:** SCP: Secret Laboratory / EXILED `9.14.2` / .NET Framework 4.8

シャープ鯖向けに開発されている大型EXILEDプラグインです。カスタムロール、
カスタムアイテム、特殊イベント、HUD、音声、ProjectMER製マップ・ギミックを
一つのサーバー構成として統合します。

本リポジトリは一般配布向けの単体プラグインではありません。Sharp Server固有の
ProjectMER・HintServiceMeowフォーク、マップデータ、Unityアセット、SNAPI-HSM、
音声ランタイムを前提としています。導入例やSCP:SLプラグイン開発資料として公開
していますが、別サーバーで利用する場合は依存機能の切り離しや設定変更が必要です。

## 主な機能

- カスタムロールとチーム／勝利条件
- カスタムアイテム、武器、防具、キーカード、SCP-914連携
- アビリティとServer Specific Settings
- 特殊イベント、襲撃、季節イベント、独自弾頭
- HSMベースのHUD、ステータス、リスポーン表示
- 近接チャットとサーバー側音声再生
- ProjectMER製カスタムマップ、Schematic、ObjectPrefab、TriggerPoint
- NPCを利用するタレット、当たり判定、演出オブジェクト

## ファミリーリポジトリ

Slafightの現行構成は、以下のSharp Server管理リポジトリで構成されています。

| Repository | 用途 |
| --- | --- |
| [Slafight_Plugin_EXILED](https://github.com/SharpServer/Slafight_Plugin_EXILED) | メインEXILEDプラグイン |
| [ProjectMER](https://github.com/SharpServer/ProjectMER) | LabAPIベースのSchematic／マップランタイム |
| [HintServiceMeow](https://github.com/SharpServer/HintServiceMeow) | HUD・Hint合成とEXILED出力 |
| [SL-CustomObjects-dev](https://github.com/SharpServer/SL-CustomObjects-dev) | Unity 2021.3.17f1製Schematic／AssetBundleソース |
| [ProjectMER-MapWorks](https://github.com/SharpServer/ProjectMER-MapWorks) | 運用中のMaps、Schematics、AssetBundle |
| [SL_References](https://github.com/SharpServer/SL_References) | 開発・ビルド・逆コンパイル用の共有参照アセンブリ |

`SL-CustomObjects-dev`のエクスポート先は`ProjectMER-MapWorks`です。Unityソースと
生成されたJSON／AssetBundleは別のGitリポジトリとして管理されます。

## 必須・運用依存

### 直接参照

- EXILED `9.14.2`
- `ProjectMER.dll`
- `HintServiceMeow-Exiled.dll`
- `SNAPI-HSM.dll`
- Harmony

### 音声・運用ランタイム

- `AudioPlayerApi.dll`
- `SCPSLAudioApi.dll`
- `NVorbis.dll`
- `ffmpeg.exe`
- `yt-dlp.exe`
- `MEROptimizerLabAPI.dll`（現行サーバー構成で併用）

正確なコンパイル参照は
[`Slafight_Plugin_EXILED.csproj`](Slafight_Plugin_EXILED/Slafight_Plugin_EXILED.csproj)
を確認してください。古いMapEditorReborn／AdvancedMERTools構成の導入例は現行
ランタイムを表していません。

## ランタイム配置

現行サーバーはポート`7777`を使用します。

```text
%APPDATA%\EXILED\Plugins\7777\
  Slafight_Plugin_EXILED.dll
  HintServiceMeow-Exiled.dll
  SNAPI-HSM.dll

%APPDATA%\EXILED\Plugins\dependencies\
  0Harmony.dll
  AudioPlayerApi.dll
  SCPSLAudioApi.dll
  ...音声・管理ライブラリ

%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\7777\
  ProjectMER.dll
  MEROptimizerLabAPI.dll

%APPDATA%\SCP Secret Laboratory\LabAPI\configs\ProjectMER\
  Maps\
  Schematics\
```

Slafightの設定は通常、次に生成されます。

```text
%APPDATA%\EXILED\Configs\Plugins\Slafight_Plugin_EXILED\7777.yml
```

## ビルド

環境変数`SL_References`を、必要なSCP:SL／EXILED／LabAPIアセンブリを格納した
各自のディレクトリへ設定します。参照アセンブリの構成は
[SL_References](https://github.com/SharpServer/SL_References)を確認してください。

```powershell
dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release
```

Releaseビルド後、`Slafight_Plugin_EXILED.dll`は
`%APPDATA%\EXILED\Plugins\7777`へ自動コピーされます。ビルドに含まれる一部の
ランタイム依存DLLも`%APPDATA%\EXILED\Plugins\dependencies`へコピーされます。

## カスタムマップとモデル

SlafightはProjectMERの`Maps`と`Schematics`を名前で参照します。データが存在しない
場合、該当モデルやギミックは生成されません。

- 運用データ:
  `%APPDATA%\SCP Secret Laboratory\LabAPI\configs\ProjectMER`
- Unityソース:
  [SL-CustomObjects-dev](https://github.com/SharpServer/SL-CustomObjects-dev)
- 運用データのGit:
  [ProjectMER-MapWorks](https://github.com/SharpServer/ProjectMER-MapWorks)

JSONを直接編集した場合はUnityソースへ自動反映されません。Unityから再エクスポート
した場合はMapWorks側の差分も確認してください。

## イベントBGM

Omega WarheadやDelta WarheadなどのBGMを有効にする場合は、Slafight設定で音声
ディレクトリを指定し、期待される名前の`.ogg`ファイル（例: `omega.ogg`、
`delta.ogg`）を配置してください。音声再生には上記の音声ランタイムが必要です。

## Wiki / GitHub Pages

プレイヤー向けのロール、アイテム、アビリティ、イベント、マップ、キー設定は
[`docs/`](docs/)にまとめています。

- GitHub Pages: `master`ブランチの`/docs`
- 管理ガイド: [`docs/internal/index.md`](docs/internal/index.md)
- 一覧データ: `docs/_data/*.yml`

## ライセンス・サポート

このプラグインはシャープ鯖固有構成を前提としており、第三者環境への無保証の
ターンキー配布ではありません。素材・音声等のクレジットは
[`docs/credits.md`](docs/credits.md)を確認してください。

- Author: `org.sharp-server.jp.scpsl`
- GitHub organization: [SharpServer](https://github.com/SharpServer)
- Original maintainer: [Slaviaaa2](https://github.com/Slaviaaa2)
