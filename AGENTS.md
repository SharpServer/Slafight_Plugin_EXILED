# Repository Instructions

## Build verification

タスク完了前に、必ずソリューションを Release 構成でビルドして検証すること。

```powershell
dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release
```

# Plugin Groups
このプラグインは様々な独自フォークプラグインやアセットによって構成されるSCPSLサーバーの一部です。以下の物が本サーバーで使用されるものの一部としてあるため、これらについて作業・検証等するときは基本的に以下の情報を参照してください。

- ProjectMER -> D:\RiderWorks\ProjectMER\ProjectMER
- - Schematic等の生成・制御ツール。カスタムマップの中核
- SL-CustomObjects-dev -> D:\Unity\Projects\SL-CustomObjects-dev
- - ProjectMERで読み込むカスタムマップのアセットを作っているUnityプロジェクト。
- HintServiceMeow -> D:\RiderWorks\HintServiceMeow
- - プレイヤーに表示するHintなどのUI制御プラグイン。
- SL_References -> D:\RiderWorks\SL_References
- - アセンブリ等参照用
- 使用しているサーバーのポート番号 -> 7777
- EXILEDフォルダ -> %APPDATA%\EXILED
- - PluginsやConfigsなどEXILEDプラグイン系の配置場所。/Plugins/7777にSlafightなどがあります。
- SCPSL鯖フォルダ -> %APPDATA%\SCP Secret Laboratory
- - サーバーログやLabAPIプラグインが配置されています。ProjectMERなどが/LabAPI/plugins/7777にあります。

これらについての作業をした後、基本的にHSMやProjectMERなどのアセンブリ系である場合はReleaseビルドをしたのち、必ず運用中のサーバーフォルダにコピーするようにしてください。EXILED / LabAPIのポート番号フォルダを参照してください。もしビルド設定等で自動コピーが設定されていない場合はSlafightを参考に設定する事。設定されている場合はアイテムの更新日時が変わっているはずなのでそこを最初にチェックしてください。

また、この運用サーバーは実際にはこのローカルPCではなくリモートのVPSでホストしているため、実運用中などに起きたとユーザーが述べたりしている場合はリモートのサーバーログを要求するようにしてください。基本的にはローカル参照で大丈夫です。
SCP:SLのログは基本的に以下の種類があります：
- %APPDATA%/SCP Secret Laboratory/LocalAdminLogs/ポート番号/
- - メインのログです。第一に確認してください。
- %userprofile%\appdata\locallow\Northwood\
- - クライアント側のログです。切断された等の問題が発生した場合はこちらも参照してください
その他にもSCP Secret Laboratoryに小さなログがありますが基本的には以上で事足ります。