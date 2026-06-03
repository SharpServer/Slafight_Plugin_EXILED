# BaseGame Patch / Network Surface Research

調査日: 2026-06-02

対象:

- `packages/ExMod.Exiled.9.14.2/lib/net48/Assembly-CSharp-Publicized.dll`
- `ilspycmd`で展開したBaseGame公開化DLL
- 主にサーバープラグインからHarmony Patch、Mirror NetworkMessage、SyncVar、RPC、既存イベントで操作できる範囲

## 結論

BaseGameの多くの状態はサーバー権威で、`SyncVar`、`NetworkMessage`、`ClientRpc`、`TargetRpc`を通じてvanilla clientへ反映できる。特に表示、音、ワールド状態、役職同期、所持品、StatusEffect、AdminToy、Respawn/Waveは今後のプラグイン素材として使える。

一方で、クライアント専用UIの描画本文はサーバーDLL上で空実装になっているものがある。`StartScreen.Show(PlayerRoleBase)`も公開化DLL上では空で、実際のロール名描画はvanilla client側のUI/Translationに依存する。したがって「サーバーPatchだけで開始画面のロール名文字列を任意変更」は現状かなり不可寄り。

実用上は、ロール名そのものを書き換えるより、`NicknameSync.CustomPlayerInfo`、`ShownPlayerInfo`、Hint、Broadcast、Subtitle/CASSIE、ServerSpecificSettings、TextToyなどで別レイヤー表示を作る方が安全。

## 操作可能性の目安

| 判定 | 意味 |
| --- | --- |
| A | 既存API/公開メソッド/SyncVar変更で安全に使いやすい |
| B | Harmony Patchで送信直前や判定直前を変える価値が高い |
| C | Network spoofや内部payload操作で可能性はあるが壊れやすい |
| D | vanilla client側UI/Translationに閉じていてサーバーPatchでは届きにくい |

## 大きな操作面の一覧

| 領域 | 主なクラス/メッセージ | 判定 | 変えられるもの | プラグイン活用案 |
| --- | --- | --- | --- | --- |
| 開始ロール画面/ロール名 | `FpcStandardRoleBase`, `StartScreen`, `PlayerRoleBase`, `RoleTranslations`, `RoleSyncInfo` | D/C | RoleType spoof、サーバー側RoleName、別レイヤー表示 | カスタム役職名はHint/CustomInfoで補完 |
| プレイヤー頭上表示 | `NicknameSync`, `PlayerInfoArea`, `HumanRole.WriteNickname` | A/B | 表示名、CustomInfo、Role表示flag、ViewRange | 疑似ロール名、状態タグ、イベント陣営表示 |
| Hint/Broadcast | `HintDisplay`, `TextHint`, `Broadcast` | A | 個別/全体テキスト通知 | ミッション通知、カスタム開始画面代替 |
| Subtitle/CASSIE | `SubtitleMessage`, `CassieTtsPayload`, `CassieAnnouncementDispatcher` | A/B | CASSIE音声、字幕、キュー、背景音有無 | シナリオ演出、警報、カスタム字幕 |
| ServerSpecificSettings | `ServerSpecificSettingsSync`, `ServerSpecificSettingBase`, `SS*Entry` | A | クライアント設定UI、ボタン、キー、スライダー、入力 | プレイヤー選択UI、役職能力設定、イベント参加UI |
| 役職同期/可視性 | `RoleSyncInfo`, `RoleSyncInfoPack`, `FpcServerPositionDistributor`, `IObfuscatedRole`, `ICustomVisibilityRole` | B/C | receiver別RoleType、spawn data、位置可視性 | 変装、幻覚、観測者別の見え方 |
| Spectator/Overwatch | `SpectatableVisibilityManager`, `SpectatableVisibilityMessage`, `OverwatchSettingsController` | A/B | Spectator一覧から隠す、Overwatch受信設定 | 観戦制限、GM用不可視、観戦イベント |
| StatusEffect/移動/視覚 | `PlayerEffectsController`, `StatusEffectBase`, `MovementBoost`, `FogControl`, `NightVision`, `Invisible`など | A/B | Effect intensity、duration、移動/視覚/音/ダメージ補正 | バフ/デバフ、暗闇、変身状態、イベント能力 |
| Inventory/Item/Pickup | `Inventory`, `AutosyncMessage`, `ItemPickupBase`, `PickupSyncInfo` | A/B/C | 所持品、装備中Item、移動速度補正、Pickup lock/in-use、Item固有RPC | 特殊アイテム、疑似クラフト、ロック付き報酬 |
| Voice/Audio | `VoiceTransceiver`, `VoiceMessage`, `AudioMessage`, `SpeakerToy` | A/B/C | ボイス転送、受信者別channel、空間Speaker音声 | 無線拡張、幻聴、エリア放送、GM音声 |
| Respawn/Wave | `WaveManager`, `WaveUpdateMessage`, `FactionInfluenceManager`, `InfluenceUpdateMessage` | A/B | Waveタイマー、トークン、Influence、Wave選択 | 陣営ポイント制、イベント湧き、動的増援 |
| World state | `DoorVariant`, `ElevatorChamber`, `RoomLightController`, `AlphaWarheadController`, `Scp914Controller`, `TeslaGate` | A/B | ドア/ロック、エレベーター、照明色、Warhead、914、Tesla演出 | マップギミック、段階イベント、パズル |
| AdminToy/仮想オブジェクト | `AdminToyBase`, `PrimitiveObjectToy`, `LightSourceToy`, `TextToy`, `SpeakerToy`, `InvisibleInteractableToy` | A | 位置/回転/スケール、色、ライト、3Dテキスト、音源、不可視Interactable | カスタムUI、会場装飾、ミッション端末 |
| Ragdoll/Decal | `RagdollManager`, `RagdollData`, `DecalCleanupMessage` | A/B | 死体生成、死体名/Role/Scale、弾痕cleanup | 偽死体、証拠品、演出死体 |
| RoundSummary | `RoundSummary` RPC/SyncVar | B/C | 終了判定、RoundSummary表示値、画面dim | 特殊勝利条件、カスタムリザルト |
| Decont/Hazard | `DecontaminationController`, `EnvironmentalHazard`, `TantrumEnvironmentalHazard` | A/B | 除染状態、Elevator text、Hazard spawn/destroy | エリア封鎖、環境ダメージ、時間制限 |

## 1. 開始ロール画面とロール名

関連:

- `FpcStandardRoleBase.ShowStartScreen()`
- `StartScreen.Show(PlayerRoleBase)`
- `PlayerRoleBase.RoleName`
- `RoleTranslations.ReloadNames()`
- `TranslationReader.Refresh()`
- `RoleSyncInfo`

確認した流れ:

```csharp
public virtual void ShowStartScreen()
{
    if (base.IsLocalPlayer)
    {
        StartScreen.Show(this);
    }
}
```

`StartScreen.Show(PlayerRoleBase)`は公開化DLLでは空実装:

```csharp
public static void Show(PlayerRoleBase prb)
{
}
```

`PlayerRoleBase.RoleName`はBaseGame上では次のどちらか:

- `ICustomNameRole.CustomRoleName`
- `RoleTranslations.GetRoleName(RoleTypeId)`

`RoleTranslations`は`TranslationReader.TryGet("Class_Names", roleId, out val)`と`RA_RoleManagement`から静的辞書へロードする。

`RoleSyncInfo`はnetwork上で以下を送る:

- target netId
- `RoleTypeId`
- `IPublicSpawnDataWriter`のpublic spawn data
- receiverがtarget自身なら`IPrivateSpawnDataWriter`のprivate spawn data
- 任意のロール名文字列は存在しない

結論:

- サーバー側`RoleTranslations`を書き換えても、vanilla clientの開始画面に出るロール名を直接変えられる保証はない。
- `RoleSyncInfo`にロール名文字列がないため、network payloadから任意名だけ差し込む自然な口はない。
- `FpcServerPositionDistributor.RoleSyncEvent`で`RoleTypeId`をreceiver別にspoofできるが、それは表示名だけでなくクライアント上のロール初期化そのものに影響する。人間Role間ならまだ検証余地はあるが、安全な任意文字列変更ではない。
- 実用案は開始直後にHint/Broadcast/Subtitle/CustomInfo/TextToyでカスタム役職名を提示すること。

## 2. プレイヤー頭上表示

関連:

- `NicknameSync`
- `PlayerInfoArea`
- `HumanRole.WriteNickname`

`NicknameSync`でサーバーから同期される主な要素:

- `ViewRange`
- `CustomPlayerInfo`
- `ShownPlayerInfo`
- `DisplayName`

`PlayerInfoArea`:

| Flag | 値 | 用途 |
| --- | ---: | --- |
| `Nickname` | 1 | 名前 |
| `Badge` | 2 | Badge |
| `CustomInfo` | 4 | Custom info |
| `Role` | 8 | ロール |
| `UnitName` | 16 | Unit name |
| `PowerStatus` | 32 | Power status |

活用:

- `ShownPlayerInfo`から`Role`を落として`CustomInfo`を表示する。
- `CustomPlayerInfo`に疑似ロール名、イベント陣営、状態タグを入れる。
- `DisplayName`は全体の名前表示へ影響するため、カスタム名として使えるが乱用はログ/管理面に影響する。

制約:

- `ValidateCustomInfo`でrich textが制限される。使えるタグ/色に制約がある。
- 頭上表示は距離と視線条件の影響を受ける。開始画面の完全代替にはならない。

おすすめ:

- カスタム役職名の常時表示は`ShownPlayerInfo &= ~PlayerInfoArea.Role`、`ShownPlayerInfo |= PlayerInfoArea.CustomInfo`、`CustomPlayerInfo = "...";`の方向が最も安全。

## 3. Hint / Broadcast / Subtitle / CASSIE

関連:

- `Hints.HintDisplay.Show(Hint)`
- `Hints.TextHint`
- `Broadcast.TargetAddElement`
- `Broadcast.RpcAddElement`
- `SubtitleMessage`
- `CassieTtsPayload`
- `CassieAnnouncementDispatcher`

できること:

- 個別Hint: `HintDisplay.Show(new TextHint(...))`
- 全体/個別Broadcast: `RpcAddElement` / `TargetAddElement`
- 字幕のみ: `SubtitleMessage.SendToAuthenticated`
- CASSIE音声と字幕: `CassieTtsPayload`
- CASSIEキュー制御: `CassieAnnouncementDispatcher.AddToQueue`, `Cancel`, `ClearAll`

活用:

- スポーン直後のカスタムロール名表示。
- ラウンド中のミッション/フェーズ表示。
- WarheadやDecontとは独立した専用警報。
- イベント専用ナレーション。

Patch候補:

- `PlayerRoleManager.SendNewRoleInfo`後、対象プレイヤーへ`TargetAddElement`やHintを送る。

非推奨:

- サーバープラグイン側で`FpcStandardRoleBase.ShowStartScreen`を直接Patchする案。これはclient local UI向けの呼び出しで、dedicated server上のPatchではvanilla clientの描画へ届かない。

## 4. ServerSpecificSettings

関連:

- `ServerSpecificSettingsSync.DefinedSettings`
- `ServerSpecificSettingsSync.SendToPlayer/SendToAll`
- `ServerSpecificSettingBase.SendUpdate`
- `SSKeybindSetting`, `SSDropdownSetting`, `SSTwoButtonsSetting`, `SSSliderSetting`, `SSPlaintextSetting`, `SSButton`, `SSTextArea`

できること:

- vanilla clientのServer-specificタブへUIを配信できる。
- プレイヤーからサーバーへ値やボタン押下を返せる。
- `SendToPlayersConditionally`で対象別UIも可能。
- `SendLabelUpdate`/`SendHintUpdate`で動的更新できる。

活用:

- カスタム役職の能力設定。
- イベント参加/棄権ボタン。
- GM用操作パネル。
- プレイヤーごとのロードアウト選択。

注意:

- 即時HUDではなく設定タブUIなので、開始画面代替には向かない。
- SettingId衝突に注意する。

## 5. Role sync / receiver別の見え方

関連:

- `RoleSyncInfo`
- `RoleSyncInfoPack`
- `PlayerRoleManager.SendNewRoleInfo`
- `FpcServerPositionDistributor.WriteAll`
- `FpcServerPositionDistributor.RoleSyncEvent`
- `IObfuscatedRole`
- `ICustomVisibilityRole`
- `ICustomNicknameDisplayRole`

`FpcServerPositionDistributor.RoleSyncEvent`:

```csharp
Func<ReferenceHub target, ReferenceHub receiver, RoleTypeId supposedRole, NetworkWriter spoofedData, RoleTypeId>
```

できること:

- receiver別にtargetの見えるRoleTypeを変える。
- `spoofedData`にspawn dataを前置できる。
- `GetVisibleRole`は距離、Spectator、GameplayData権限、SCP、グローバル通信、visibility controllerを見て`Spectator`へ落とす。

制約:

- `SendRole(receiver, hub, ...)`は`receiver == hub`の場合、targetRoleを実ロールへ戻す。
- `PlayerRoleManager.SendNewRoleInfo`は直接`RoleSyncInfo`を送るため、自己への初期送信ではspoofが効く余地がある。ただし周期配信で戻る可能性があり、自己画面への安定spoofは別途検証が必要。
- RoleTypeを変えると、クライアントのrole prefab初期化やspawn data読み取りが変わる。任意文字列表示目的で使うには危険。

活用:

- 変装/幻覚: 特定プレイヤーからだけ別ロールに見せる。
- 遠距離/暗闇でSpectator扱いにして姿を消す。
- 管理者/GMだけ本来ロールを見えるようにする。

Patch候補:

- `FpcServerPositionDistributor.GetVisibleRole` postfixで条件追加。
- `FpcServerPositionDistributor.RoleSyncEvent`購読でreceiver別spoof。
- `PlayerRoleManager.SendNewRoleInfo`周辺で初期同期時だけ別データ送信。ただし互換性リスク高。

## 6. Spectator / Overwatch

関連:

- `SpectatableVisibilityManager.SetHidden`
- `SpectatableVisibilityMessages.SpectatableVisibilityMessage`
- `OverwatchSettingsController.OverwatchSettingsInfoMessage`

できること:

- Spectator一覧から特定プレイヤーを隠す。
- 新規参加者にもhidden状態が同期される。
- Overwatch側のvoice channel、spatial audio、player info、hitreg log設定を受け取る。

活用:

- GM/イベント役職を観戦対象から隠す。
- 死亡後も特定陣営だけ見せない観戦制御。
- Overwatch用の監視/実況モード。

## 7. StatusEffect / 移動 / 視覚 / ダメージ

関連:

- `PlayerEffectsController.ChangeState<T>`
- `PlayerEffectsController.ServerSyncEffect`
- `PlayerEffectsController.ServerSendPulse<T>`
- `StatusEffectBase.ServerSetState`
- Effect interfaces: `IMovementSpeedModifier`, `IStaminaModifier`, `IDamageModifierEffect`, `IFpcCollisionModifier`, `IAmbientBoostingEffect`, `ISoundtrackMutingEffect`, `ICustomDisplayName`, `IHitmarkerPreventer`

できること:

- Effect intensityとdurationを同期できる。
- SyncListでEffect強度がclientへ流れる。
- Pulse対応EffectはTargetRpcで演出を発火できる。
- 移動速度、スタミナ、ジャンプ、当たり判定、視覚、音楽、ダメージを既存Effect経由で変えられる。

候補Effect:

- `MovementBoost`, `Slowness`, `Exhausted`, `Invigorated`
- `Invisible`, `SpawnProtected`, `FogControl`, `NightVision`, `Blindness`, `Flashed`
- `SoundtrackMute`, `SilentWalk`, `HeavyFooted`, `Ghostly`
- `DamageReduction`, `BodyshotReduction`, `Burned`, `Vitality`

活用:

- カスタム役職能力の土台。
- エリア効果やフェーズ制デバフ。
- 暗闇・幻覚・視界制限イベント。
- ダメージ軽減/増加の陣営バフ。

Patch候補:

- `StatusEffectBase.ServerSetState`系の呼び出し前後。
- ダメージ計算側の`IDamageModifierEffect`収集処理。
- 移動補正収集処理。

## 8. Inventory / Item / Pickup

関連:

- `Inventory`
- `Inventory.ServerSelectItem`
- `Inventory.ServerSendItems`
- `Inventory.TargetRefreshItems`
- `Inventory.TargetRefreshAmmo`
- `AutosyncMessage`
- `AutosyncRpc`
- `ItemPickupBase`
- `PickupSyncInfo`

できること:

- 所持品リストとammoをTargetRpcで更新。
- 現在装備中Itemは`CurItem` SyncVar。
- 所持品由来の移動/スタミナ補正は`Inventory.RefreshModifiers`で同期。
- `AutosyncMessage`はItemType/Serialと任意payloadを持ち、item template/instanceのclient handlerに届く。
- Pickupは`Info` SyncVarでItemId、Serial、Weight、Locked、InUseを同期する。

活用:

- 特殊アイテム能力。
- Pickupをロックして条件達成まで拾えなくする。
- Item autosyncで既存アイテム演出や状態を拡張。
- 重量/移動補正を利用したイベント装備。

注意:

- `AutosyncMessage`のpayload形式はItem実装依存。汎用の任意UIチャンネルとして使うと壊れやすい。
- クライアント側Item templateが対応していないpayloadは無視/例外リスクがある。

## 9. Voice / Audio

関連:

- `VoiceTransceiver`
- `VoiceMessage`
- `VoiceChatChannel`
- `AudioMessage`
- `AudioTransceiver`
- `SpeakerToy`
- `SpeakerToyPlaybackBase`

`VoiceTransceiver.ServerReceiveMessage`は、送信者検証、rate limit、mute、`ValidateSend`、受信者ごとの`ValidateReceive`を通した後、receiver別に`VoiceMessage`を送る。

できること:

- 既存イベント`PlayerSendingVoiceMessage`/`PlayerReceivingVoiceMessage`で抑止や書き換え。
- receiverごとにchannelを変える。
- `VoiceTransceiver.OnVoiceMessageReceiving`で送信直前を観測。
- `SpeakerToy`は`ControllerId`一致の`AudioMessage`をOpus decodeして再生する。
- `SpeakerToy`は空間/非空間、volume、min/max distanceをSyncVarで持つ。

活用:

- エリア限定放送。
- 特定プレイヤーにだけ聞こえる幻聴。
- 無線チャンネルの拡張。
- `SpeakerToy` + `AudioMessage`によるマップ内音源。

注意:

- 音声payloadはOpus前提。
- voiceは帯域とrate limitの影響が大きい。
- `VoiceMessage`のSpeaker偽装は検証で落ちる可能性が高い。受信者側channel変更の方が安全。

## 10. Respawn / Wave / Influence

関連:

- `WaveManager`
- `WaveUpdateMessage`
- `FactionInfluenceManager`
- `InfluenceUpdateMessage`
- `RespawnTokensManager`

できること:

- Wave timer、pause、force pause、tokensを同期。
- `WaveManager.InitiateRespawn`でWave選択。
- `FactionInfluenceManager.Set/Add/Remove`で陣営Influenceを同期。
- `WaveManager.OnWaveTrigger`や`OnWaveSpawned`でフック可能。

活用:

- 陣営ポイントによる増援制御。
- SCP/Chaos/NTF以外のイベント進行にWave UIを転用。
- 目標達成で湧き間隔を短縮/延長。
- 参加人数や勝敗状況に応じた動的スポーン。

注意:

- Wave indexは`WaveManager.Waves`の順序に依存する。独自Waveを差し込むならclient側の認識と同期が必要で高リスク。
- 標準Waveのパラメータ変更に留める方が安全。

## 11. World state

### Door / Elevator

関連:

- `DoorVariant`
- `DoorVariant.NetworkTargetState`
- `DoorVariant.NetworkActiveLocks`
- `DoorVariant.ServerChangeLock`
- `ElevatorChamber`

できること:

- ドア開閉、ロック理由、DoorId。
- Elevatorのgroup、destination、waypoint。
- `ServerInteract`やpermission判定をPatchして、特殊鍵/条件を実装。

活用:

- パズルロック。
- フェーズ進行で解放されるルート。
- 特定ロールだけ通れる疑似アクセス制御。

### RoomLight

関連:

- `RoomLightController.LightsEnabled`
- `RoomLightController.OverrideColor`
- `ServerFlickerLights`

できること:

- 部屋のライトON/OFF。
- 色override。
- flicker。
- `RoomLightController.IsInDarkenedRoom`が`FpcStandardRoleBase.InDarkness`へ影響する。

活用:

- 停電、暗闇イベント。
- 陣営色の部屋演出。
- 視界/InsufficientLightingと連動したギミック。

### Warhead

関連:

- `AlphaWarheadController`
- `AlphaWarheadSyncInfo`
- `SubtitleMessage`
- `RpcShow`系

できること:

- countdown開始/停止/強制時間変更/即時準備。
- Warhead lock。
- 自動Warheadやbroadcast。
- Warhead字幕。

活用:

- フェーズタイマー。
- 脱出イベント。
- 偽Warhead警報。

### SCP-914

関連:

- `Scp914Controller`
- `Scp914KnobSetting`
- `Scp914Upgrader`

できること:

- knob同期。
- activate/upgrade sequence。
- door close/openやsound RPC。
- `Scp914Events`で変更/起動を制御。

活用:

- 独自レシピ。
- 914をイベントクラフト台にする。
- 特定ラウンドだけ挙動変更。

### Tesla / Hazard / Decont

関連:

- `TeslaGate`
- `TeslaHitMsg`
- `EnvironmentalHazard`
- `DecontaminationController`

できること:

- Tesla animation/idle/instant burst。
- Decontamination状態、Elevator locked text、time offset。
- Hazard spawn/destroy。

活用:

- エリアトラップ。
- カスタム除染シナリオ。
- マップ内危険区域の動的生成。

## 12. AdminToy / カスタムオブジェクト

関連:

- `AdminToyBase`
- `PrimitiveObjectToy`
- `LightSourceToy`
- `TextToy`
- `SpeakerToy`
- `InvisibleInteractableToy`
- `Scp079CameraToy`
- `WaypointToy`

`AdminToyBase`共通SyncVar:

- Position
- Rotation
- Scale
- MovementSmoothing
- IsStatic

Toy別:

- `PrimitiveObjectToy`: primitive type、color、visible/collidable flags
- `LightSourceToy`: intensity、range、color、shadow、light type、spot angle
- `TextToy`: text format、display size、arguments
- `SpeakerToy`: controller id、spatial、volume、distance
- `InvisibleInteractableToy`: collider shape、interaction duration、locked

活用:

- 3D Textによるロール名/任務表示。
- カスタム端末/ボタン。
- イベント会場の装飾。
- 位置付き音源。
- 見えないインタラクト判定。

おすすめ:

- 「開始画面ロール名」を直接変えるより、spawn地点前に`TextToy`や`PrimitiveObjectToy`で明示する方がBaseGame互換性が高い。

## 13. Ragdoll / Decal

関連:

- `RagdollManager.ServerCreateRagdoll`
- `RagdollData`
- `BasicRagdoll`
- `DecalCleanupMessage`

できること:

- 任意RoleTypeのragdollを生成。
- nickname、damage handler、position、rotation、scale、serialを指定。
- death時のragdoll生成をイベント/Patchで差し替え。
- decal poolのcleanup messageを送る。

活用:

- 偽死体。
- 証拠品/調査イベント。
- 大型演出や怪異表現。
- ラウンド負荷対策としてdecals/ragdolls cleanup。

注意:

- ragdoll prefabはRoleTypeに紐づく。
- 大量生成はclient負荷が高い。

## 14. RoundSummary / 勝敗

関連:

- `RoundSummary`
- `RoundSummary.ForceEnd`
- `RoundSummary.RpcShowRoundSummary`
- `RoundSummary.RpcDimScreen`
- `RoundSummary.ExtraTargets`
- `RoundSummary.TargetCount`

できること:

- 終了判定へ介入。
- 追加target数やtarget countをSyncVarで反映。
- RoundSummary表示RPCの値をPatchで差し替える。
- dim/undimをRPCで制御。

活用:

- カスタム勝利条件。
- イベント用リザルト。
- 第三陣営の疑似勝利表示。

注意:

- client UI本文は公開化DLL上で空の箇所があり、表示内容を完全に自由化できるとは限らない。
- 勝敗処理は他プラグインと競合しやすい。

## 15. 推奨プラグイン案

| 案 | 使うBaseGame面 | 実装難度 | メモ |
| --- | --- | --- | --- |
| 疑似カスタムロール名表示 | `NicknameSync.CustomPlayerInfo`, Hint, Broadcast | 低 | 開始画面変更より堅い |
| 観測者別の変装 | `RoleSyncEvent`, `GetVisibleRole`, `ShownPlayerInfo` | 高 | client role初期化リスクあり |
| イベント任務UI | Hint, ServerSpecificSettings, TextToy | 低-中 | 設定タブとHUDを併用 |
| フェーズ制マップギミック | Door, Elevator, RoomLight, Warhead, Tesla | 中 | SyncVar中心で堅い |
| 役職能力バフ/デバフ | StatusEffect, Damage/Movement modifiers | 中 | 既存Effectの組み合わせが安全 |
| 陣営ポイント増援 | FactionInfluence, WaveManager, RespawnTokens | 中 | vanilla wave UIを活用 |
| 空間音声演出 | SpeakerToy, AudioMessage, Cassie | 中-高 | Opus payload管理が必要 |
| カスタム端末 | InvisibleInteractableToy, TextToy, PrimitiveObjectToy | 中 | ミッション端末やショップに向く |
| 証拠/偽死体システム | RagdollManager, RagdollData | 中 | 調査/人狼系イベント向き |

## 16. 高リスク/避けたい操作

| 操作 | リスク |
| --- | --- |
| `RoleSyncInfo`で任意RoleTypeを自己にspoof | 周期配信で戻る、spawn data不一致、client role初期化が壊れる |
| `AutosyncMessage`を未知payloadで送る | Item固有readerで例外/無視される |
| Translation辞書を書き換えてclient UI変更を期待する | サーバー側辞書とclient側UI/translationは別 |
| RoundSummary RPC値を過度に偽装 | 他プラグイン/終了処理と競合 |
| VoiceMessage Speaker偽装 | server receive側検証に引っかかる可能性が高い |
| 大量AdminToy/Ragdoll/Decal生成 | client負荷とnetwork負荷が高い |

## 17. 次に実装検証するなら

優先順:

1. `NicknameSync.CustomPlayerInfo` + `ShownPlayerInfo`で疑似ロール名を表示できるか実機確認。
2. `PlayerRoleManager.SendNewRoleInfo`後のHint/Broadcast/Subtitle表示タイミングを確認。
3. `FpcServerPositionDistributor.RoleSyncEvent`でreceiver別RoleType spoofを限定条件で試験。
4. `TextToy`/`InvisibleInteractableToy`を使ったイベントUI/端末のPoC。
5. `RoomLightController.OverrideColor`とStatusEffectを組み合わせたフェーズ演出PoC。

## 参考にした主要BaseGame実装

| 領域 | ファイル |
| --- | --- |
| Start screen | `StartScreen.cs`, `PlayerRoles.FirstPersonControl/FpcStandardRoleBase.cs` |
| Role name/translation | `PlayerRoles/PlayerRoleBase.cs`, `PlayerRoles/RoleTranslations.cs`, `TranslationReader.cs` |
| Role sync | `PlayerRoles/RoleSyncInfo.cs`, `PlayerRoles/PlayerRoleManager.cs`, `PlayerRoles.FirstPersonControl.NetworkMessages/FpcServerPositionDistributor.cs` |
| Nameplate | `NicknameSync.cs`, `PlayerInfoArea.cs`, `PlayerRoles/HumanRole.cs` |
| Hints/Broadcast | `Hints/HintDisplay.cs`, `Hints/TextHint.cs`, `Broadcast.cs` |
| CASSIE/Subtitle | `Cassie/CassieTtsPayload.cs`, `Cassie/CassieAnnouncementDispatcher.cs`, `Subtitles/SubtitleMessage.cs` |
| Server settings | `UserSettings.ServerSpecific/ServerSpecificSettingsSync.cs`, `ServerSpecificSettingBase.cs` |
| Effects | `PlayerEffectsController.cs`, `CustomPlayerEffects/StatusEffectBase.cs` |
| Inventory | `InventorySystem/Inventory.cs`, `InventorySystem.Items.Autosync/AutosyncMessage.cs`, `InventorySystem.Items.Pickups/ItemPickupBase.cs` |
| Voice/Audio | `VoiceChat.Networking/VoiceTransceiver.cs`, `AudioTransceiver.cs`, `AudioMessage.cs`, `AdminToys/SpeakerToy.cs` |
| Respawn | `Respawning/WaveManager.cs`, `WaveUpdateMessage.cs`, `FactionInfluenceManager.cs` |
| World | `Interactables.Interobjects.DoorUtils/DoorVariant.cs`, `RoomLightController.cs`, `AlphaWarheadController.cs`, `Scp914/Scp914Controller.cs`, `TeslaGate.cs` |
| AdminToy | `AdminToys/AdminToyBase.cs`, `PrimitiveObjectToy.cs`, `LightSourceToy.cs`, `TextToy.cs`, `InvisibleInteractableToy.cs` |
| Ragdoll/Decal | `PlayerRoles.Ragdolls/RagdollManager.cs`, `RagdollData.cs`, `CommandSystem.Commands.RemoteAdmin.Cleanup/DecalCleanupMessage.cs` |
