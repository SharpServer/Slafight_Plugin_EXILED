# 銃器サウンド差し替え: クリップ採取チェックシート

作成日: 2026-07-30

対象:

- `Slafight_Plugin_EXILED.API.Features.CItemWeapon` の `OverrideSounds` 系
- `Slafight_Plugin_EXILED.API.Features.GunSoundResolver`
- `Slafight_Plugin_EXILED.API.Enums.GunSoundKind`

## これは何

`CItemWeapon` 派生で銃の音を差し替えるとき、**どの音を何で指定すればいいか**を実機 1 周で確定させるための手順書。

指定手段は 4 つあり、評価順は次のとおり（先に当たったものが勝つ）:

| 優先 | プロパティ | キー | 実機採取が必要か |
| --- | --- | --- | --- |
| 1 | `OverrideSoundsByIndex` | `AudioIndex` | 必要 |
| 2 | `OverrideSoundsByClip` | `AudioClip` 名 | 必要 |
| 3 | `OverrideSounds` | `GunSoundKind` | **不要** |
| 4 | `OverrideSoundsByChannel` | `MixerChannel` | **不要** |

**まず 3 と 4 で足りないか検討する。** `Gunshot` / `DryFire` / `Reload` / `Unload` / `Equip` はコードから判定しているので銃種を問わず動き、採取作業が要らない。1 と 2 が要るのは「リロード中の音を 1 つずつ別クリップに分けたい」といったケースだけ。

---

## 事前準備（1 回だけ）

- [ ] 対象の `CItemWeapon` 派生に `protected override bool LogGunSoundClips => true;` を追加
- [ ] `dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release`
- [ ] `%APPDATA%\EXILED\Plugins\7777\Slafight_Plugin_EXILED.dll` のタイムスタンプが更新されたか確認
- [ ] サーバー再起動
- [ ] コンソール or `%APPDATA%\EXILED\Logs` を開いておく

> `LogGunSoundClips` を true にするだけでイベント購読が有効になる。差し替え定義がまだ 0 件でも動く。

---

## 採取手順（銃 1 丁あたり）

**必ずカスタムアイテム本体を持って行う。** バニラ銃を撃っても `Check(ev.Item)` で弾かれてログには出ない。

- [ ] 対象アイテムを自分に付与する
- [ ] **持ち替える** → 抜き出し音が鳴った時点で全件ダンプが出る（`ItemType` ごとに 1 回だけ）
- [ ] 撃つ → `Gunshot`。持ち替えでダンプが出なかった場合はここで出る
- [ ] 弾を撃ち切ってさらに撃つ → `DryFire`
- [ ] リロードする → `Reload`
- [ ] リロードキーを 1 秒以上長押しする → `Unload`
- [ ] インスペクト（武器を眺める）→ `kind=<none>`、クリップ名で拾う
- [ ] ADS を覗く / 解除する → 出ないかもしれない（後述）
- [ ] リボルバー系なら追加でコッキング / デコッキング

### 全件ダンプの見かた

持ち替えた瞬間に 1 度だけ出る。**これが本命**で、この銃に登録されている全クリップが並ぶ。

```
[GunProject90] GunCrossvec registered clips (16):
  [0] DRYFIRE (DryFire)
  [1] CrossvecFire (Gunshot)
  [2] CrossvecSilenced (Gunshot)
  ...
  [9] CrossvecMagInsert
```

- `(...)` 付き = `GunSoundKind` で判定できる音。**採取不要**、`OverrideSounds` で指定できる
- `(...)` 無し = アニメーションイベント由来。`OverrideSoundsByClip` にこの名前をそのまま書く

### 個別イベント行の見かた

音が鳴るたびに 1 行出る。

```
[GunProject90] gun sound: index=9 clip='CrossvecMagInsert' kind=Reload channel=DefaultSfx range=12
```

| 項目 | 読み方 |
| --- | --- |
| `index` | `OverrideSoundsByIndex` のキー |
| `clip` | `OverrideSoundsByClip` のキー（そのままコピー） |
| `kind` | `OverrideSounds` のキー。`<none>` ならクリップ名か index で指定するしかない |
| `channel` | `Weapons`=発砲音 / `DefaultSfx`=機構音全般 / `NoDucking`=Disruptor のアクション音 |
| `range` | 判別にはあまり使えない（実測値: 1 / 5 / 12 / 15 と発砲音の 21〜144）。`channel` を見る方が確実 |

---

## 記入表

銃ごとにコピーして使う。

### `<ItemType>` / `<CItem クラス名>`

採取日:

| index | clip 名 | kind | channel | range | 鳴らした操作 | 差し替え先ファイル |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |
| | | | | | | |
| | | | | | | |

---

## 実装への落とし込み

採取が済んだら `LogGunSoundClips` を消して、対応する定義を書く。

```csharp
// kind が付いた音 → 採取不要でこう書ける
protected override IReadOnlyDictionary<GunSoundKind, GunSoundOverride> OverrideSounds
    => new Dictionary<GunSoundKind, GunSoundOverride>
    {
        [GunSoundKind.Gunshot] = "MyGun_Fire.ogg",
        [GunSoundKind.DryFire] = "MyGun_NoAmmo.ogg",
        [GunSoundKind.Reload]  = GunSoundOverride.Silent,   // リロード音を全部消す
    };

// kind=<none> の音だけクリップ名で個別指定
protected override IReadOnlyDictionary<string, GunSoundOverride> OverrideSoundsByClip
    => new Dictionary<string, GunSoundOverride>
    {
        ["CrossvecMagRemove"] = "MyGun_MagOut.ogg",
        ["CrossvecMagInsert"] = new("MyGun_MagIn.ogg", Volume: 0.7f, Voices: 1),
    };
```

- 値は `string` から暗黙変換されるので、ファイル名だけならそのまま書ける
- `Range` / `Volume` / `IsSpatial` / `Voices` / `MinInterval` は省略すると `OverrideAudioRange` などのアイテム既定値を使う
- 音声ファイルは `AudioReferences` 設定のディレクトリ配下に置く
- [ ] 書いたら `LogGunSoundClips` を消す
- [ ] リビルドして実機で鳴ることを確認

---

## 落とし穴

### `GunSoundKind.Reload` は 1 回のリロードで複数回鳴る

`Reload` は「リロード中に鳴った音すべて」にマッチする。マガジン排出・挿入・ボルトで 3〜4 回鳴るので、`[GunSoundKind.Reload] = "reload.ogg"` と書くとその回数だけ再生される。重複抑制（既定 0.04 秒）は秒単位で離れた音には効かない。

リロード全体に 1 本の音を流したいなら `Reload` を `Silent` にした上で、`OnReloading` から `SpeakerApi.Play` を呼ぶ。

### 名前付きクリップ判定が状態判定より優先される

リボルバーのリロード中にコッキング音が鳴った場合、`Reload` ではなく `RevolverCocking` になる。より具体的な方を優先する設計。

### ログに出ない音がある

`AudioModule.PlayClientside` は `sync: false` でネットワーク送信されないため、`SendingGunSound` が発火しない。**差し替えも抑制もできず、ログにも出ない。** ADS 音などがこれに該当する可能性がある（未確認）。「鳴っているのにログに出ない」場合はこれを疑う。

同様に `AudioModule._clipToIndex` に無いクリップも送信されない。

### AssetRipper の出力から名前を写す場合

AssetRipper 出力の `ExportedProject/Assets/AudioClip` にファイル名がある。ただし:

- `FSP9_Inspect 0` / `1` / `2` の**連番は実際のクリップ名の一部**。実機ダンプでも同じ名前で出るので、
  末尾の数字ごとそのままキーに書く（`CrossvecInspect 0`、`COM15 Inspect 1`、`AK Pickup 1` なども同様）
- `FSP9 Firing Supressed` は**原文のタイポ**（`p` が 1 つ）。そのまま書く
- dry fire は `DRYFIRE` / `noammo1` / `rev_dryfire` のように**銃をまたいで共有**されている。名前では銃を特定できないので `GunSoundKind.DryFire` を使う
- 同エクスポートの `.prefab` には MonoBehaviour のフィールドが出ていないため、**`AudioIndex` の並び順は復元できない**。index が要るなら実機ダンプ一択
- ファイル名にあっても、その銃の `_registeredClips` に無ければイベントは飛ばない
  （例: `CrossvecMagRemoveSlow` は登録されているが `Crossvec*` 系でもリストに無いものがある）。
  最終確認は必ず実機ダンプで行う

照合は大文字小文字と前後空白を無視する（`FSP9 Mag In` のように空白入りの名前がある）。

### `AudioIndex` はアセット依存

`AudioIndex` は `AudioModule._registeredClips` への挿入順で決まり、その順序は銃器プレハブの `AllSubcomponents`（`[SerializeField]`）と `_eventClips` の並びで決まる。**ゲーム更新で黙って変わりうる。** `OverrideSoundsByIndex` は最後の手段とし、可能なら `OverrideSounds`（Kind）か `OverrideSoundsByClip` を使う。

---

## 採取済み: 全銃器のクリップ一覧

採取日: 2026-07-30 / 採取に使ったもの: `GunSoundTestbench`

`(...)` 付きは `GunSoundKind` で判定できたもの。**採取不要で `OverrideSounds` から指定できる。**
無印はアニメーションイベント由来で、`OverrideSoundsByClip` か `OverrideSoundsByIndex` が必要。

### GunCOM15 (13)

```
0 noammo1 (DryFire)     1 COM15 Firing (Gunshot)  2 COM15 Suppressed (Gunshot)
3 COM15 Inspect 1       4 COM15 Equip             5 COM15 Mag In
6 COM15 Mag Out         7 COM15 Pickup            8 COM15 SlidePull
9 COM15 SlideRelease   10 COM15 Inspect 2        11 COM15 Unload
12 COM15 Inspect 3
```

### GunCOM18 (13)

```
0 noammo1 (DryFire)     1 COM15 Firing (Gunshot)  2 COM15 Suppressed (Gunshot)
3 COM15 Equip           4 COM15 Mag In            5 COM15 Mag Out
6 COM15 SlidePull       7 COM15 SlideRelease      8 Unload Bullet
9 AK Inspect 0         10 COM15 Inspect 1        11 COM15 Inspect 2
12 COM15 Inspect 3
```

### GunCom45 (8)

```
0 noammo1 (DryFire)     1 COM15 Firing (Gunshot)  2 COM15 Equip
3 COM15 Pickup          4 COM15 Mag In            5 COM15 Mag Out
6 COM15 SlidePull       7 COM15 SlideRelease
```

サプレッサー発砲音なし。

### GunFSP9 (17)

```
 0 noammo1 (DryFire)     1 FSP9 Firing (Gunshot)   2 FSP9 Firing Supressed (Gunshot)
 3 FSP9 ADS Out          4 FSP9 Equip Without fore or stock
 5 ADS up                6 FSP9_Inspect 0          7 FSP9 Mag Drop
 8 FSP9 Handle Pull      9 FSP9 Stock Extend      10 FSP9 Foregrip Fold Out
11 FSP9 Handle Release  12 FSP9 Mag In            13 COM15 Unload
14 FSP9_Inspect 1       15 FSP9_Inspect 2         16 FSP9 Bolt Release
```

### GunCrossvec (17)

```
 0 noammo1 (DryFire)     1 CrossvecFire (Gunshot)  2 CrossvecSilenced (Gunshot)
 3 CrossvecInspect 0     4 CrossvecEquip           5 CrossvecEquipStock
 6 CrossvecHandlePull    7 CrossvecHandleRelease   8 CrossvecMagRemove
 9 CrossvecMagInsert    10 CrossvecAdsDown        11 CrossvecInspect 2
12 CrossvecAdsUp        13 CrossvecInspect 1      14 CrossvecMagRemoveSlow
15 Unload Bullet        16 CrossvecBoltRelease
```

### GunE11SR (19)

```
 0 noammo1 (DryFire)     1 E11SR Firing (Gunshot)  2 E11SR_Silenced (Gunshot)
 3 AK Inspect 0          4 E11SR Inspect 0         5 E11SR Mag Out
 6 E11SR Handle HalfPull 7 CrossvecInspect 2       8 E11SR Equip
 9 E11SR Handle Pull    10 E11SR Drum In          11 E11SR Mag In
12 E11SR Handle Release 13 E11SR Drum Tap         14 E11SR Empty Gun
15 E11SR Inspect 1      16 E11SR Bolt Return      17 Shotgun ADS Out
18 E11SR Inspect 2
```

### GunAK (22)

```
 0 noammo1 (DryFire)     1 AK Firing (Gunshot)     2 AK Suppressed (Gunshot)
 3 AK Pickup 1           4 AK Inspect 0            5 AK Inspect 1
 6 Equip No Charge       7 BananaRemoval           8 DrumRemove
 9 AK Inspect Pull      10 BananaCharging         11 AK Pickup 2
12 DrumInsert           13 BananaEjection         14 BananaInsertion
15 DrumDrop             16 AK Inspect Release     17 BananaImpact
18 Unload Bullet        19 DrumCharging           20 AK Inspect 2
21 ADS down
```

### GunA7 (9)

```
0 rev_dryfire (DryFire)  1 A7Fire (Gunshot)        2 PickupFirst
3 A7Draw                 4 A7ADSExit               5 RemoveMag
6 PickupClose            7 InsertMag               8 Rechamber
```

### GunFRMG0 (20)

```
 0 noammo1 (DryFire)     1 Fire (Gunshot)          2 FireSilenced (Gunshot)
 3 ADS in                4 Inspect AR 1            5 Equip
 6 Inspect BC 1          7 Remove AR               8 Remove BC
 9 Rechamber            10 Insert AR              11 Insert BC
12 Inspect BC 3         13 Tap AR                 14 Tap BC
15 Inspect AR 3         16 Unload Bullet          17 Inspect AR 2
18 Inspect BC 2         19 DrumDrop
```

`AR` = アサルトライフル用マガジン、`BC` = ドラムマガジン。

### GunLogicer (11)

```
0 noammo1 (DryFire)      1 Log Firing (Gunshot)    2 Log Handling Reload Start
3 Log Charging Handle    4 Log Lid Open            5 Log Box Unload
6 Log Box Load           7 Log Lid Close           8 Log Inspect 53
9 Log Inspect 15        10 Log Inspect 25
```

サプレッサー発砲音なし。

### GunShotgun (14)

```
 0 Shotgun Firing 2nd (Gunshot)  1 Shotgun Firing 1st (Gunshot)
 2 rev_dryfire (DryFire)         3 Shotgun Reload Transition
 4 Shotgun Reload 1st Shell      5 Shotgun Reload 2nd Shell
 6 Shotgun PumpIn                7 Shotgun PumpOut
 8 Shotgun ADS Out               9 Shotgun Pocket Searching 3
10 Shotgun Equip 2nd time       11 Shotgun Inspect 0
12 Shotgun Inspect 1            13 Shotgun Inspect 2
```

**index 0 と 1 の並びが逆**（0 が 2 発目、1 が 1 発目）。2 連射すると 1 → 0 の順で飛ぶ。

### GunRevolver (22)

```
 0 rev_dryfire (DryFire)          1 rev_double_action (RevolverDoubleAction)
 2 rev_cock (RevolverCocking)     3 rev_decock (RevolverDecocking)
 4 rev_fire (Gunshot)             5 rev_fire_buckshot (Gunshot)
 6 rev_inspect_startnormal        7 rev_draw
 8 rev_roulette                   9 rev_draw_pickup
10 rev_fancy                     11 rev_load
12 rev_reload_rare               13 rev_reload_marauder
14 rev_reload                    15 rev_draw_rare
16 rev_unload                    17 rev_inspect_startspin
18 rev_inspect_midsud            19 rev_inspect_opencyl
20 rev_inspect_end               21 rev_inspect_closecyl
```

### GunSCP127 (7)

```
0 DRYFIRE (DryFire)      1 GUNSHOT BOTH LAYERS (Gunshot)  2 GENERIC
3 INSPECT                4 EQUIP                          5 PULL CHARGING
6 SLAP CHARGING
```

### ParticleDisruptor (15)

```
 0 3x_reload             1 3x_inspect0_2           2 3x_pickup
 3 3x_inspect2_6         4 3x_inspect6_8           5 3x_inspect8_12
 6 3x_draw               7 3x_single_action (DisruptorAction)
 8 3x_single_action_last (DisruptorAction)         9 3x_single_shot (Gunshot)
10 3x_single_shot_last (Gunshot)                  11 3x_rapid_action (DisruptorAction)
12 3x_rapid_action_last (DisruptorAction)         13 3x_rapid_shot (Gunshot)
14 3x_rapid_shot_last (Gunshot)
```

`3x_reload` は実測で `kind=Reload`（`channel=DefaultSfx range=1`）。

---

## 実測から分かったこと

### クリップは銃をまたいで共有されている

`clip` 名は銃を特定しない。実例:

| クリップ | 使っている銃 |
| --- | --- |
| `noammo1` | COM15 / COM18 / Com45 / FSP9 / Crossvec / E11SR / AK / FRMG0 / Logicer |
| `rev_dryfire` | Revolver / A7 / Shotgun |
| `COM15 Firing` `COM15 Suppressed` `COM15 Mag In` 等 | COM15 / COM18 / Com45 |
| `COM15 Unload` | COM15 / FSP9 |
| `AK Inspect 0` | AK / COM18 / E11SR |
| `CrossvecInspect 2` | Crossvec / E11SR |
| `Shotgun ADS Out` | Shotgun / E11SR |
| `Unload Bullet` | COM18 / Crossvec / AK / FRMG0 |
| `ADS up` / `ADS down` | FSP9 / AK |

`OverrideSoundsByClip` は `Check(ev.Item)` でその CItem のアイテムに限定されるため実害はないが、
**「クリップ名から銃を判定する」ことはできない**。dry fire を狙うなら必ず `GunSoundKind.DryFire` を使う。

### `Equip` は「抜き出し中に鳴った音すべて」

抜き出しアニメーションが複数音を持つ銃では、それら全部が `Equip` になる。実測:

- Shotgun: `Shotgun Reload Transition` / `Shotgun PumpIn` / `Shotgun PumpOut`
- Crossvec: `CrossvecEquipStock` に加えて `CrossvecAdsDown`
- Logicer: `Log Handling Reload Start`

同じ `Shotgun PumpIn` でも、**戦闘中のポンプ操作は `kind=<none>`** になる。同じクリップが状況で
分類を変えるので、抜き出し音を 1 つだけ狙うなら `OverrideSoundsByClip` を使うこと。

### 発砲音の `range` は銃ごとに違う

`FinalGunshotRange`（アタッチメント補正込み）がそのまま出る。実測: FSP9 サプレッサー付 21 /
Crossvec サプレッサー付 21 / COM15 サプレッサー付 24 / E11SR サプレッサー付 31.5 /
COM18 60 / SCP127 60 / AK 90 / A7 90 / Revolver 90 / Shotgun 100 / Disruptor 100 /
Logicer 120 / FRMG0 144。機構音は 1 / 5 / 12、Disruptor のアクション音は 15。

### インスペクト音に Kind は無い

全銃で `Inspect` 系はゲーム側に名前付きフィールドが無く、`kind=<none>`。クリップ名で指定する。
なお `COM15 Inspect 1` のようにインデックスが飛び飛びに配置されている銃が多い。
