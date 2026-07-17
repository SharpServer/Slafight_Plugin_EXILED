using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

/// <summary>
/// 「DANTE ─ 業火の指揮者」── Hypixel Skyblock の Dante 着想の多段階ボスレイド。
///
/// 設計の肝:
///  - ボス本体は EXILED <see cref="Npc"/> に <see cref="CRoleTypeId.Dante"/> を付与したもの。
///    SnowWarrior と同じく独立した勝利グループ（<see cref="API.Features.RoundVictory.Sources.DanteVictoryDefinitionSource"/>）
///    に属するので、ボス健在の間は討伐側 Chaos と 2 グループが共存しラウンドが終わらない。
///    素体は Foundation の NtfCaptain なので Chaos から確実に被弾し、NPC は AI を持たず手動制御。
///  - 見た目は "Dante" スキマティックを <see cref="WearFollower"/> で NPC に追従させるだけ
///    （ベストエフォート。資産が欠けても戦闘は止めない）。
///  - HP は完全仮想化。<see cref="Exiled.Events.Handlers.Player.Hurting"/> を握り、被弾者が
///    ボスのときは実ダメージを 0 にして HP を満タン固定。フェーズ・撃破判定は仮想 HP のみで行う。
/// </summary>
public class DanteEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.DanteBattle;
    public override int MinPlayersRequired => 1;
    public override string LocalizedName => "-=[ DANTE ─ 業火の指揮者 ]=-";
    public override string TriggerRequirement => "Manual Trigger";

    // ===== 調整値 =====
    private const string SchematicName = "Dante";
    private const string ThemeFile = "dante.ogg";
    // Npc.Spawn 時の素体ロール。直後に CRoleTypeId.Dante を被せる（Dante の素体も NtfCaptain）。
    // Dante 化までの待機中も Foundation として存在し、Dante 化後は Dante 勝利グループが面倒を見る。
    private const RoleTypeId BossRole = RoleTypeId.NtfCaptain;
    private const float PinnedHealth = 100000f;             // 実 HP は触らせない（保険で満タン固定）

    // 巨大ボス＆触手の見た目チューニング。
    // 当たり判定（HitboxScale = NPC 本体の localScale）と見た目（VisualScale = スキマティック＋触手）を
    // 分離。本体モデルは EffectType.Fade で消し、見た目はスキマティックの LocalScale で出すので、
    // HitboxScale を見た目に合わせて調整すれば「デカい見た目に当たり判定が一致」する。
    private const float HitboxScale = 2.6f;   // NPC 本体 = 当たり判定（見た目に合わせて調整する値）
    private const float VisualScale = 2.6f;   // スキマティック＆触手の見た目サイズ
    private const int TentacleCount = 8;      // 触手の本数
    private const int TentacleSegments = 6;   // 1 本あたりのカプセル節数
    private static readonly Color TentacleColor = new(0.30f, 0.85f, 0.12f, 1f); // スライムグリーン

    // 部隊 Wave（増援）。互角に戦うための staying power。無限ではなく上限ありで「いずれ自力で」。
    private const int MaxWaves = 4;           // 増援の最大回数
    private const float WaveInterval = 30f;   // 増援の間隔（秒）

    // スライム酸沼。
    private const int MaxPuddles = 8;         // 同時に存在できる沼の上限
    private const float PuddleLifetime = 6f;  // 沼の寿命（秒）
    private static readonly Color PuddleColor = new(0.20f, 0.70f, 0.05f, 0.9f);

    // 中央触手（Bacte 方式の弱点）。これを全部壊すまでコアは無敵。
    private const float WeakPointHealth = 700f; // 弱点 1 つあたりの HP
    private const float ShieldTimeout = 25f;    // 触手が地形に埋まる等で到達不能でも必ず解除（ソフトロック防止）
    private static readonly Color WeakPointColor = new(0.45f, 1f, 0.20f, 1f); // 鮮緑の触手リンク

    private static readonly Color CrimsonLight = new(0.85f, 0.06f, 0.06f);

    // 戦闘中にランダムで吐く挑発。EffectedInfo に流す。
    private static readonly string[] Taunts =
    [
        "そのちっぽけな鉛で、業火が消せるとでも?",
        "熱いだろう? これが地獄の入口だ。",
        "まだ立っているのか。見上げた根性だ ── だが無駄だ。",
        "逃げ場などない。地上ごと焼べてやろう。",
        "踊れ、踊れ。炎が貴様らを抱くまで。",
        "私の名を、灰になる前に覚えておけ。"
    ];

    // ===== ランタイム状態 =====
    private Npc? _boss;
    private SchematicObject? _skin;
    private SpeakerApi.Playback _theme;
    private CoroutineHandle _ai;
    private readonly BossBar _bossBar = new()
    {
        Title = "DANTE",
        TitleColor = "#ff1a1a",
        BarColor = "#ff3333",
    };
    private readonly List<Primitive[]> _tentacles = []; // 各要素 = 1 本の触手（節キューブの配列）
    private readonly List<SlimePuddle> _puddles = [];    // 酸の沼
    private readonly List<Npc> _weakPoints = [];         // 中央触手の弱点 NPC
    private readonly List<Primitive[]> _weakLinks = [];  // 各弱点へ伸びる触手リンク（節キューブ）
    private CoroutineHandle _waves;                         // 部隊 Wave コルーチン
    private int _wavesSpawned;
    private bool _invulnerable;                             // 中央触手が残っている間はコア無敵
    private float _shieldExpiry;                            // 無敵の強制解除時刻（Time.time 基準）
    private bool _leaping;                                  // 跳躍中は通常移動を止める
    private bool _bodyHidden;                               // 本体を Fade で隠したか（スキン有り時）
    private float _visualMul = 1f;                          // 見た目スケールの実行時倍率（断末魔の縮小用）

    private sealed class SlimePuddle
    {
        public Primitive Visual = null!;
        public Vector3 Center;
        public float Radius;
        public float Expiry; // Time.time 基準
    }

    private float _maxHp;
    private float _hp;
    private int _phase;          // 1..3
    private bool _active;
    private bool _eventsHooked;
    private Vector3 _arenaCenter;

    // ───────────────────────────────────────────────────────────
    //  入口
    // ───────────────────────────────────────────────────────────
    public override bool IsReadyToExecute() => false; // 自動抽選には乗せない（手動/管理者トリガー専用）

    protected override void OnExecute(int eventPid)
    {
        if (_active)
            return;

        _active = true;
        Round.IsLocked = true;

        // 再利用インスタンスの実行時状態をリセット。
        _leaping = false;
        _bodyHidden = false;
        _invulnerable = false;
        _visualMul = 1f;
        _wavesSpawned = 0;

        // アリーナ中心を「変換前」のプレイヤー位置から算出。
        List<Player> initial = Player.List
            .Where(p => p is not null && p.IsNotHost() && p.IsAlive)
            .ToList();
        _arenaCenter = initial.Count > 0
            ? AveragePosition(initial)
            : new Vector3(7f, 320f, -55f); // フォールバック（旧来のスポーン地点）

        // ボス NPC を召喚（load-bearing。失敗したら戦闘は成立しない）。
        if (!SpawnBoss())
        {
            Cleanup();
            return;
        }

        TryPlayTheme();
        Announce("<size=40><b><color=#ff1a1a>D A N T E</color></b></size>\n" +
                 "<size=22>業火の指揮者が目を覚ます ──</size>", 6);

        // NPC のロール適用完了を待ってから Dante 化 → 全員 Chaos 化 → 戦闘開始。
        _ai = Timing.RunCoroutine(StartBattleLoopAfterNpcReady());
    }

    // ───────────────────────────────────────────────────────────
    //  ボス実体の生成（NPC + スキン + 被弾フック）
    // ───────────────────────────────────────────────────────────
    private bool SpawnBoss()
    {
        Vector3 spawnPos = _arenaCenter + new Vector3(0f, 1f, 0f);

        _boss = Npc.Spawn("DANTE", BossRole, true, spawnPos);
        if (_boss is null)
        {
            Log.Error("[Dante] ボス NPC のスポーンに失敗。イベントを中止します。");
            return false;
        }

        _boss.MaxHealth = PinnedHealth;
        _boss.Health = PinnedHealth;
        _boss.Scale = Vector3.one * HitboxScale; // 当たり判定をデカく（localScale 実体 → サーバー側 hitbox も拡大）

        TryAttachSkin(spawnPos); // best-effort（欠けても戦う）
        HideBodyIfSkinned();     // スキンがあれば本体モデルを Fade で消す
        HookEvents();            // 被弾フック（仮想 HP）
        return true;
    }

    private void TryAttachSkin(Vector3 spawnPos)
    {
        try
        {
            if (!ObjectSpawner.TrySpawnSchematic(SchematicName, spawnPos, out SchematicObject? model) || model is null)
            {
                Log.Warn($"[Dante] スキマティック '{SchematicName}' が見つかりません。スキン無しで続行します。");
                return;
            }

            _skin = model;
            _skin.Scale = Vector3.one * VisualScale; // 見た目サイズ（当たり判定とは独立）

            // NPC の Transform へラグなし追従（repo の Wear 機構と同じ手口）。
            if (_boss?.Transform is { } bossTransform)
            {
                WearFollower follower = _skin.gameObject.AddComponent<WearFollower>();
                follower.Initialize(bossTransform);
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn($"[Dante] スキン装着に失敗（無視して続行）: {ex.Message}");
            _skin = null;
        }
    }

    /// <summary>
    /// スキマティックがある時だけ本体モデルを <see cref="EffectType.Fade"/>(255) で透明化する。
    /// Fade は見た目だけで当たり判定（hitbox）は残るので、巨大な NPC 本体を「見えない当たり判定」に
    /// できる。スキンが無い時は本体を残して最低限ボスが見えるようにする（フォールバック）。
    /// </summary>
    private void HideBodyIfSkinned()
    {
        if (_boss is null || _skin is null)
            return;

        try
        {
            _boss.EnableEffect(EffectType.Fade, 255);
            _bodyHidden = true;
        }
        catch (System.Exception ex)
        {
            Log.Warn($"[Dante] 本体の Fade に失敗（無視して続行）: {ex.Message}");
        }
    }

    private void TryPlayTheme()
    {
        try
        {
            _theme = SpeakerApi.PlayLoop(
                ThemeFile, "DanteTheme", _arenaCenter,
                isSpatial: false, maxDistance: 9_999_999f, minDistance: 0.1f, volume: 1f);
        }
        catch (System.Exception ex)
        {
            Log.Warn($"[Dante] テーマ再生に失敗（無視して続行）: {ex.Message}");
        }
    }

    // ───────────────────────────────────────────────────────────
    //  メインループ
    // ───────────────────────────────────────────────────────────
    private IEnumerator<float> StartBattleLoopAfterNpcReady()
    {
        // Npc.Spawn はロール適用を SpawnSetRoleDelay 遅延させる。完了を待ってから触る。
        yield return Timing.WaitForSeconds(Npc.SpawnSetRoleDelay + 0.1f);

        if (CancelIfOutdated() || _boss is null || !_boss.IsAlive)
        {
            Log.Warn("[Dante] ボス NPC が初期化完了前に無効化されたため、イベントを中止します。");
            Cleanup();
            yield break;
        }

        // ① ボスを Dante カスタムロール化（討伐側 Chaos とは別の独立勝利グループへ）。
        //    Npc 側の遅延ロール適用が終わった「後」に行う（CustomRoleRemover による消失を防ぐ）。
        _boss.SetRole(CRoleTypeId.Dante);
        _boss.MaxHealth = PinnedHealth;
        _boss.Health = PinnedHealth;

        // ② ボスが Dante グループに入った「後で」全員を討伐部隊（DanteSlayer）化する。
        //    こうすれば「生存勝利グループが Insurgency 1 つだけ」になる隙が生まれず、即終了しない。
        int playerCount = 0;
        foreach (Player player in Player.List.ToList())
        {
            if (player is null || !player.IsNotHost() || player.ReferenceHub == _boss.ReferenceHub)
                continue;

            player.SetRole(CRoleTypeId.DanteSlayer);
            playerCount++;
        }

        // ③ ロール変更によるテレポートが落ち着くのを待ち、ボスをプレイヤー重心へ寄せて即交戦に。
        yield return Timing.WaitForSeconds(0.1f);
        if (CancelIfOutdated() || _boss is null || !_boss.IsAlive)
        {
            Cleanup();
            yield break;
        }

        List<Player> targets = GetTargets();
        if (targets.Count > 0)
        {
            _arenaCenter = AveragePosition(targets);
            _boss.Position = _arenaCenter + new Vector3(0f, 1f, 6f);
        }

        _boss.MaxHealth = PinnedHealth;
        _boss.Health = PinnedHealth;
        _boss.Scale = Vector3.one * HitboxScale; // ロール変更で戻る可能性に備え再適用
        HideBodyIfSkinned();                     // Fade もロール変更で消える可能性があるので再適用

        // 触手を生やす（AdminToy Primitive のカプセル節）。
        CreateTentacles();

        // ④ 仮想 HP を人数でスケール → 開幕演出 → AI 開始。
        _maxHp = 2500f + 1200f * Mathf.Max(1, playerCount);
        _hp = _maxHp;
        _phase = 1;

        _bossBar.MaxValue = _maxHp;
        _bossBar.Value = _hp;
        _bossBar.Show();

        Exiled.API.Features.Cassie.MessageTranslated(
            "danger . unrecognized entity detected on the surface . all units engage .",
            "警告 ── 地上に未確認の存在を検知。全戦力で交戦せよ。",
            isNoisy: true);
        Announce("<size=30><color=#ff2a2a><b>第一幕 ─ 業火の序曲</b></color></size>\n" +
                 "<size=20>Inferno Overture</size>", 6);
        Speak("我が名はDANTE。地獄の業火を指揮する者。精々踊ってみせろ、塵共が。", 7f);

        _ai = Timing.RunCoroutine(BattleLoop());
        _waves = Timing.RunCoroutine(ReinforcementWaves());
    }

    private IEnumerator<float> BattleLoop()
    {
        const float dt = 0.1f;
        float attackTimer = 0f;
        float hpBarTimer = 0f;
        float puddleTimer = 0f;

        while (true)
        {
            if (CancelIfOutdated() || _boss is null || !_boss.IsAlive)
            {
                Cleanup();
                yield break;
            }

            if (_hp <= 0f)
            {
                yield return Timing.WaitUntilDone(Timing.RunCoroutine(Finale()));
                yield break;
            }

            // 実 HP は常に満タンへ（流れ弾の保険）。
            _boss.Health = PinnedHealth;

            UpdatePhase();

            (float speed, float interval) = _phase switch
            {
                1 => (5.0f, 2.4f),
                2 => (7.5f, 1.6f),
                _ => (10.0f, 1.0f),
            };

            // 無敵（触手ゲート）中は中央に据わってリンクを短く保つ。向きだけは追う。
            if (!_leaping && _invulnerable)
                FaceNearest();
            else if (!_leaping)
            {
                FaceNearest();
                ChaseNearest(speed * dt);
            }

            AnimateTentacles(Time.time);
            UpdateTentacleShield(Time.time); // 中央触手のリンク更新＆全滅判定

            // 酸沼の判定は 0.5 秒間隔（毎 tick のエフェクト連打を避ける）。
            puddleTimer += dt;
            if (puddleTimer >= 0.5f)
            {
                puddleTimer = 0f;
                ProcessPuddles();
            }

            attackTimer += dt;
            if (attackTimer >= interval && !_leaping)
            {
                attackTimer = 0f;
                PerformAttack();
            }

            UpdateBossBar(); // バー本体は共有マネージャーが再描画する。ここでは値だけ更新（軽量）。

            hpBarTimer += dt;
            if (hpBarTimer >= 1f)
            {
                hpBarTimer = 0f;
                if (_bodyHidden) // Fade が何かで消えても維持（毎秒）
                    _boss.EnableEffect(EffectType.Fade, 255);
            }

            yield return Timing.WaitForSeconds(dt);
        }
    }

    private void UpdatePhase()
    {
        float ratio = _hp / _maxHp;
        int desired = ratio > 0.66f ? 1 : ratio > 0.33f ? 2 : 3;
        if (desired == _phase)
            return;

        _phase = desired;
        if (_phase == 2)
        {
            Announce("<size=30><color=#ff7a00><b>第二幕 ─ 紅蓮の軍勢</b></color></size>\n" +
                     "<size=20>Crimson Legion</size>", 5);
            Speak("第二幕だ ── 紅蓮の軍勢よ、目覚めよ。逃げ惑う姿が見たい。", 6f);
            Nova(24, 11f);              // 突入の合図に放射ノヴァ
            BeginTentacleShield(3);     // 中央触手ゲート（3 本）
        }
        else if (_phase == 3)
        {
            Announce("<size=34><color=#ff0000><b>第三幕 ─ 終焉のメルトダウン</b></color></size>\n" +
                     "<size=20>FINAL MELTDOWN</size>", 6);
            Exiled.API.Features.Cassie.Message(".G3 . G3 . meltdown imminent", isNoisy: true);
            Speak("もう遊びは終わりだ。貴様らごと、全てを灰に帰してくれる！", 6f);
            foreach (Player target in GetTargets())
                target.SendWarheadExplosionEffect();
            BeginTentacleShield(5);     // 中央触手ゲート（5 本・最終）
        }
    }

    // ───────────────────────────────────────────────────────────
    //  移動
    // ───────────────────────────────────────────────────────────
    private void ChaseNearest(float step)
    {
        if (_boss is null)
            return;

        Player? target = NearestTarget();
        if (target is null)
            return;

        Vector3 next = Vector3.MoveTowards(_boss.Position, target.Position, step);

        // 激しい挙動：左右に蛇行＋上下に跳ねる（直線でなく暴れながら迫る）。
        Vector3 toTarget = target.Position - _boss.Position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Vector3 sideways = Vector3.Cross(toTarget.normalized, Vector3.up);
            float weave = Mathf.Sin(Time.time * 6f) * step * 0.8f;
            next += sideways * weave;
        }

        next.y = target.Position.y + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.6f; // 跳ねる
        _boss.Position = next;
    }

    /// <summary>最寄りのターゲットの方を向く（本体・スキン・触手の向きが揃う）。</summary>
    private void FaceNearest()
    {
        if (_boss is null)
            return;

        Player? target = NearestTarget();
        if (target is null)
            return;

        Vector3 dir = target.Position - _boss.Position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            _boss.Rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // ───────────────────────────────────────────────────────────
    //  部隊 Wave（増援）── 戦死した討伐隊を上限回数まで復活させる
    // ───────────────────────────────────────────────────────────
    private IEnumerator<float> ReinforcementWaves()
    {
        while (_wavesSpawned < MaxWaves)
        {
            yield return Timing.WaitForSeconds(WaveInterval);

            if (CancelIfOutdated() || _boss is null || !_boss.IsAlive)
                yield break;

            // 戦死した討伐隊（非ホスト・非NPCの Spectator）を増援対象に。
            // 死にたて触手 NPC を巻き込まないよう !IsNPC を入れる。
            List<Player> reinforcements = Player.List
                .Where(p => p is not null && p.IsNotHost() && p.Role.Type == RoleTypeId.Spectator)
                .ToList();

            if (reinforcements.Count == 0)
                continue; // 全員生存中なら波を温存

            _wavesSpawned++;
            int waveNo = _wavesSpawned;

            Exiled.API.Features.Cassie.MessageTranslated(
                "reinforcement squad has arrived",
                $"討伐隊 増援 第 {waveNo} 波 到着。",
                isNoisy: true);
            Announce($"<size=28><color=#39ff14><b>増援部隊 第{waveNo}波 到着！</b></color></size>\n" +
                     "<size=18>戦線を立て直せ</size>", 5);

            foreach (Player reinforcement in reinforcements)
            {
                reinforcement.SetRole(CRoleTypeId.DanteSlayer);

                int id = reinforcement.Id;
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 ring = _arenaCenter +
                               new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 12f +
                               Vector3.up * 1f;

                // ロールのスポーンテレポート後に配置（SnowWarrior と同じ手法）。
                Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
                {
                    Player? rp = Player.Get(id);
                    if (rp is { IsAlive: true })
                        rp.Position = ring;
                });
            }
        }

        Announce("<size=24><color=#ffcc00><b>これ以上の増援は無い。自力で討て。</b></color></size>", 6);
    }

    // ───────────────────────────────────────────────────────────
    //  跳躍（標的へ放物線で飛び、着地でスラム）
    // ───────────────────────────────────────────────────────────
    private IEnumerator<float> LeapAttack(Vector3 targetPos)
    {
        if (_boss is null || _leaping)
            yield break;

        _leaping = true;
        Speak("墜ちろッ！", 2.5f);

        Vector3 start = _boss.Position;
        Vector3 end = targetPos;
        const float duration = 0.75f;
        float t = 0f;

        while (t < duration)
        {
            if (CancelIfOutdated() || _boss is null || !_boss.IsAlive)
            {
                _leaping = false;
                yield break;
            }

            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            Vector3 pos = Vector3.Lerp(start, end, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * 10f; // 放物線の山
            _boss.Position = pos;

            AnimateTentacles(Time.time); // 滞空中も触手は暴れる
            yield return 0f;
        }

        if (_boss != null && _boss.IsAlive)
        {
            _boss.Position = end;

            // 着地スラム：放射ノヴァ＋至近への直接ダメージ＋画面揺れ（閃光は控えめ）。
            Nova(_phase == 3 ? 28 : 18, 12f);
            if (Random.value < 0.5f)
                FlashStorm(2);
            foreach (Player target in GetTargets())
            {
                float distance = Vector3.Distance(target.Position, end);
                if (distance < 14f)
                {
                    target.Hurt(35f, DamageType.Explosion);
                    target.SendWarheadExplosionEffect();
                }
                else if (distance < 26f)
                {
                    target.Hurt(15f, DamageType.Explosion);
                }
            }
        }

        _leaping = false;
    }

    // ───────────────────────────────────────────────────────────
    //  触手（AdminToy Primitive のカプセル節をうねらせる）
    // ───────────────────────────────────────────────────────────
    private void CreateTentacles()
    {
        DestroyTentacles();

        for (int k = 0; k < TentacleCount; k++)
        {
            var segments = new Primitive[TentacleSegments];
            for (int j = 0; j < TentacleSegments; j++)
            {
                Primitive segment = Primitive.Create(
                    PrimitiveType.Cube, Vector3.zero, Vector3.zero, Vector3.one * 0.1f, true, TentacleColor);
                segment.Collidable = false; // 弾やプレイヤーを邪魔しない
                segments[j] = segment;
            }

            _tentacles.Add(segments);
        }
    }

    private void DestroyTentacles()
    {
        foreach (Primitive[] tentacle in _tentacles)
        {
            foreach (Primitive segment in tentacle)
            {
                try
                {
                    segment.RemoveShowState();
                    segment.Destroy();
                }
                catch
                {
                    // ignored
                }
            }
        }

        _tentacles.Clear();
    }

    /// <summary>触手を時間ベースのうねりで毎フレーム再配置する。</summary>
    private void AnimateTentacles(float time)
    {
        if (_boss is null || _tentacles.Count == 0)
            return;

        Vector3 basePos = _boss.Position;
        Quaternion bossRot = _boss.Rotation;
        float scale = VisualScale * _visualMul;   // 触手は「見た目」側に追従（当たり判定とは独立）
        float rootRadius = 1.1f * scale;
        float segLen = 1.0f * (scale / 2.6f);

        for (int k = 0; k < _tentacles.Count; k++)
        {
            Primitive[] segments = _tentacles[k];
            float yaw = (360f / _tentacles.Count) * k;
            float phase = k * 1.37f;

            // 本体の向きを基準に放射状の根本を決める。
            Vector3 outward = bossRot * Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 root = basePos + outward * rootRadius + Vector3.up * (0.4f * scale);
            Vector3 dir = (outward * 0.5f + Vector3.up).normalized;
            Vector3 pos = root;

            for (int j = 0; j < segments.Length; j++)
            {
                float bend = Mathf.Sin(time * 4.5f + phase + j * 0.85f) * 24f;
                Vector3 axis = Vector3.Cross(dir, Vector3.up);
                if (axis.sqrMagnitude < 0.0001f)
                    axis = Vector3.Cross(dir, outward);
                dir = (Quaternion.AngleAxis(bend, axis.normalized) * dir).normalized;

                Vector3 next = pos + dir * segLen;
                float thickness = (0.6f * scale / 2.6f) * (1f - j * 0.08f); // 四角く太いブロック触手
                OrientSegment(segments[j], pos, next, thickness);
                pos = next;
            }
        }
    }

    /// <summary>キューブ（Z 軸を長軸として使う）を 2 点間に橋渡しする。</summary>
    private static void OrientSegment(Primitive segment, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.001f)
        {
            segment.Scale = Vector3.zero;
            return;
        }

        segment.Position = (a + b) * 0.5f;
        segment.Rotation = Quaternion.LookRotation(delta / length, Vector3.up);
        segment.Scale = new Vector3(thickness, thickness, length); // Cube: 長軸 Z = 節の長さ
    }

    // ───────────────────────────────────────────────────────────
    //  攻撃ディスパッチ
    // ───────────────────────────────────────────────────────────
    private void PerformAttack()
    {
        if (_boss is null)
            return;

        List<Player> targets = GetTargets();
        if (targets.Count == 0)
            return;

        if (Random.value < 0.3f)
            Speak(Taunts[Random.Range(0, Taunts.Length)], 4f);

        // 全フェーズで頻繁に大跳躍（激しく動く）。フェーズが上がるほど高確率。無敵中は据わるので跳ばない。
        float leapChance = _phase switch { 1 => 0.3f, 2 => 0.45f, _ => 0.55f };
        if (!_leaping && !_invulnerable && Random.value < leapChance)
        {
            Player leapTarget = targets[Random.Range(0, targets.Count)];
            Timing.RunCoroutine(LeapAttack(leapTarget.Position));
            return;
        }

        switch (_phase)
        {
            case 1:
                int roll1 = Random.Range(0, 4);
                if (roll1 == 0) AcidPuddles(targets, 2);                                    // 酸の沼
                else if (roll1 == 1) GrenadeRain(targets[Random.Range(0, targets.Count)].Position, 6, 4f);
                else if (roll1 == 2) SkyTentacleRain(targets, 2);                           // 上空から触手
                else LobAt(NearestTarget());
                break;

            case 2:
                int roll2 = Random.Range(0, 5);
                if (roll2 == 0) Nova(16, 9f);
                else if (roll2 == 1) SlimeBalls(14);                                        // 分裂粘塊
                else if (roll2 == 2) AcidPuddles(targets, 3);
                else if (roll2 == 3) SkyTentacleRain(targets, 3);                           // 上空から触手
                else StickyEngulf();                                                        // 粘着捕縛
                break;

            default: // フェーズ3: メルトダウン（毎回全部ではなく抽選で 1〜2 種に絞る）
                int roll3 = Random.Range(0, 5);
                if (roll3 == 0) Nova(24, 10f);
                else if (roll3 == 1) AcidPuddles(targets, 4);
                else if (roll3 == 2) SlimeBalls(12);
                else if (roll3 == 3) SkyTentacleRain(targets, 4);
                else { StickyEngulf(); GrenadeRain(targets[Random.Range(0, targets.Count)].Position, 8, 5f); }

                // 閃光は控えめに（たまにだけ少量）。
                if (Random.value < 0.15f)
                    FlashStorm(2);
                break;
        }
    }

    // ───────────────────────────────────────────────────────────
    //  攻撃プリミティブ
    // ───────────────────────────────────────────────────────────

    /// <summary>頭上から降り注ぐグレネードの雨。</summary>
    private void GrenadeRain(Vector3 center, int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 pos = center + new Vector3(c.x, 16f, c.y);
            Throw(ProjectileType.FragGrenade, pos, Vector3.down * 18f, 1.1f);
        }
    }

    /// <summary>ボスを中心とした放射状グレネード環。</summary>
    private void Nova(int count, float speed)
    {
        if (_boss is null)
            return;

        Vector3 origin = _boss.Position + Vector3.up * 1.2f;
        for (int i = 0; i < count; i++)
        {
            float ang = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 dir = new(Mathf.Cos(ang), 0.35f, Mathf.Sin(ang));
            Throw(ProjectileType.FragGrenade, origin, dir.normalized * speed, 2.6f);
        }
    }

    /// <summary>分裂粘塊：跳ね回る SCP-018 を「ちぎれた粘体」としてばら撒く。</summary>
    private void SlimeBalls(int count)
    {
        if (_boss is null)
            return;

        Speak("我が身を分かつ ── 喰らえ、粘塊！", 3f);
        Vector3 origin = _boss.Position + Vector3.up * 1.5f;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = new(Random.Range(-1f, 1f), Random.Range(0.2f, 0.8f), Random.Range(-1f, 1f));
            Throw(ProjectileType.Scp018, origin, dir.normalized * Random.Range(8f, 14f), 0f);
        }
    }

    // ───────────────────────────────────────────────────────────
    //  スライム系（酸の沼・粘着）
    // ───────────────────────────────────────────────────────────

    /// <summary>標的の足元に緑の酸の沼を生成（中に居ると腐食＋鈍足＋DOT）。</summary>
    private void AcidPuddles(List<Player> targets, int count)
    {
        if (targets.Count == 0)
            return;

        Speak("沼に沈め。骨まで溶かしてやる。", 3f);
        for (int i = 0; i < count; i++)
        {
            Player t = targets[Random.Range(0, targets.Count)];
            Vector2 jitter = Random.insideUnitCircle * 2.5f;
            SpawnPuddle(t.Position + new Vector3(jitter.x, 0.05f, jitter.y), Random.Range(2.4f, 3.4f));
        }
    }

    private void SpawnPuddle(Vector3 center, float radius)
    {
        // 上限を超えたら古いものから消す。
        while (_puddles.Count >= MaxPuddles)
        {
            RemovePuddle(_puddles[0]);
        }

        Primitive viz = Primitive.Create(
            PrimitiveType.Cube, center, new Vector3(90f, 0f, 0f),
            new Vector3(radius * 2f, radius * 2f, 0.12f), true, PuddleColor);
        viz.Collidable = false;

        _puddles.Add(new SlimePuddle
        {
            Visual = viz,
            Center = center,
            Radius = radius,
            Expiry = Time.time + PuddleLifetime,
        });
    }

    /// <summary>0.5 秒間隔。寿命切れ沼の除去と、沼内プレイヤーへの腐食・鈍足・DOT 付与。</summary>
    private void ProcessPuddles()
    {
        for (int i = _puddles.Count - 1; i >= 0; i--)
        {
            SlimePuddle puddle = _puddles[i];
            if (Time.time >= puddle.Expiry || puddle.Visual == null || puddle.Visual.Base == null)
            {
                RemovePuddle(puddle);
                continue;
            }

            foreach (Player target in GetTargets())
            {
                Vector3 flat = target.Position - puddle.Center;
                flat.y = 0f;
                if (flat.sqrMagnitude > puddle.Radius * puddle.Radius)
                    continue;

                // 1 秒持続で付与（0.5 秒間隔の再付与でも連打にならない）。
                target.EnableEffect(EffectType.Corroding, 1, 1f);
                target.EnableEffect(EffectType.Slowness, 40, 1f);
                target.Hurt(5f, DamageType.Poison);
            }
        }
    }

    private void RemovePuddle(SlimePuddle puddle)
    {
        _puddles.Remove(puddle);
        try
        {
            puddle.Visual?.RemoveShowState();
            puddle.Visual?.Destroy();
        }
        catch
        {
            // ignored
        }
    }

    private void DestroyPuddles()
    {
        foreach (SlimePuddle puddle in _puddles.ToList())
            RemovePuddle(puddle);

        _puddles.Clear();
    }

    /// <summary>粘着捕縛：ボス周囲のプレイヤーを鈍足化＋汚れさせ、引き寄せる（完全拘束はしない）。</summary>
    private void StickyEngulf()
    {
        if (_boss is null)
            return;

        Speak("逃がさん。粘体が貴様を捉えた。", 3f);
        Vector3 bossPos = _boss.Position;
        foreach (Player target in GetTargets())
        {
            Vector3 toBoss = bossPos - target.Position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude > 16f * 16f)
                continue;

            target.EnableEffect(EffectType.Slowness, 60, 2.5f);
            target.EnableEffect(EffectType.Stained, 1, 2.5f);
            target.Hurt(8f, DamageType.Poison);

            // 軽く引き寄せ（完全拘束は理不尽なので 2m だけ）。
            if (toBoss.magnitude > 4f)
                target.Position += toBoss.normalized * 2f;
        }
    }

    // ───────────────────────────────────────────────────────────
    //  上空からの触手降らし
    // ───────────────────────────────────────────────────────────
    private void SkyTentacleRain(List<Player> targets, int count)
    {
        if (targets.Count == 0)
            return;

        Speak("天より来たれ、我が触腕。", 3f);
        for (int i = 0; i < count; i++)
        {
            Player t = targets[Random.Range(0, targets.Count)];
            Vector2 jitter = Random.insideUnitCircle * 4f;
            Vector3 impact = t.Position + new Vector3(jitter.x, 0f, jitter.y);
            Timing.RunCoroutine(SkyTentacleStrike(impact));
        }
    }

    private IEnumerator<float> SkyTentacleStrike(Vector3 impact)
    {
        // 落下する緑の触手柱（縦長キューブ）。
        Primitive column = Primitive.Create(
            PrimitiveType.Cube, impact + Vector3.up * 32f, Vector3.zero,
            new Vector3(0.9f, 14f, 0.9f), true, TentacleColor);
        column.Collidable = false;

        Vector3 start = impact + Vector3.up * 32f;
        Vector3 end = impact + Vector3.up * 7f; // 柱の中心が地表付近に来る
        const float fall = 0.5f;

        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            if (CancelIfOutdated() || column.Base == null)
            {
                SafeDestroy(column);
                yield break;
            }

            column.Position = Vector3.Lerp(start, end, t / fall);
            yield return 0f;
        }

        if (column.Base != null)
            column.Position = end;

        // 着弾 AOE。
        foreach (Player target in GetTargets())
        {
            Vector3 flat = target.Position - impact;
            flat.y = 0f;
            if (flat.sqrMagnitude < 9f) // 半径 3m
            {
                target.Hurt(40f, DamageType.Crushed);
                target.ExplodeEffect(ProjectileType.FragGrenade);
            }
        }

        Throw(ProjectileType.FragGrenade, impact + Vector3.up * 0.5f, Vector3.zero, 0.3f);

        yield return Timing.WaitForSeconds(0.6f);
        SafeDestroy(column);
    }

    private static void SafeDestroy(Primitive primitive)
    {
        try
        {
            primitive?.RemoveShowState();
            primitive?.Destroy();
        }
        catch
        {
            // ignored
        }
    }

    // ───────────────────────────────────────────────────────────
    //  中央触手（Bacte 方式）── 壊すまでコアは無敵
    // ───────────────────────────────────────────────────────────
    private void BeginTentacleShield(int count)
    {
        if (_invulnerable || _boss is null)
            return;

        _invulnerable = true;
        _shieldExpiry = Time.time + ShieldTimeout;
        Announce("<size=30><color=#39ff14><b>コアは無敵だ！</b></color></size>\n" +
                 "<size=20>中央につながる触手を破壊せよ</size>", 6);
        Speak("無駄だ。我が核に触れたくば、まずこの触腕を引きちぎってみせろ。", 6f);
        Exiled.API.Features.Cassie.Message("core is now protected", isNoisy: false);

        // 触手はボス（壁際にいる可能性）ではなくプレイヤー重心の周囲に出す（到達性優先）。
        List<Player> around = GetTargets();
        Vector3 center = around.Count > 0 ? AveragePosition(around) : _boss.Position;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 10f + Vector3.up;

            Npc weak = Npc.Spawn("Tentacle Core", RoleTypeId.Scp0492, true, pos);
            if (weak is null)
                continue;

            int id = weak.Id;
            Vector3 spot = pos;
            Timing.CallDelayed(Npc.SpawnSetRoleDelay + 0.1f, () =>
            {
                Player? wp = Player.Get(id);
                if (wp is not { IsAlive: true })
                    return;

                wp.MaxHealth = WeakPointHealth;
                wp.Health = WeakPointHealth;
                wp.Scale = Vector3.one * 1.3f;
                wp.Position = spot;
            });

            _weakPoints.Add(weak);

            // 弱点へ伸びる触手リンク（節キューブ）。
            var link = new Primitive[4];
            for (int s = 0; s < link.Length; s++)
            {
                Primitive seg = Primitive.Create(
                    PrimitiveType.Cube, Vector3.zero, Vector3.zero, Vector3.one * 0.1f, true, WeakPointColor);
                seg.Collidable = false;
                link[s] = seg;
            }

            _weakLinks.Add(link);
        }

        // 弱点が 1 つも湧かなかった場合は無敵を解除（保険）。
        if (_weakPoints.Count == 0)
            _invulnerable = false;
    }

    /// <summary>各 tick：リンクを更新し、死んだ弱点を除去。全滅でコア露出。</summary>
    private void UpdateTentacleShield(float time)
    {
        if (!_invulnerable || _boss is null)
            return;

        // 制限時間切れ（触手が到達不能等）で強制解除しソフトロックを防ぐ。
        if (time >= _shieldExpiry)
        {
            Speak("チッ…まあいい。じきに磨り潰す。", 4f);
            EndTentacleShield();
            return;
        }

        Vector3 center = _boss.Position + Vector3.up * (1.5f * VisualScale * _visualMul);

        for (int i = _weakPoints.Count - 1; i >= 0; i--)
        {
            Npc weak = _weakPoints[i];
            Primitive[] link = _weakLinks[i];

            bool dead = weak is null || weak.ReferenceHub == null || !weak.IsAlive;
            if (dead)
            {
                foreach (Primitive seg in link)
                    SafeDestroy(seg);

                _weakPoints.RemoveAt(i);
                _weakLinks.RemoveAt(i);
                try { weak?.Destroy(); } catch { /* ignore */ }
                continue;
            }

            // リンクをコア中心から弱点へ、軽くうねらせて橋渡し。
            Vector3 a = center;
            Vector3 b = weak.Position + Vector3.up * 1f;
            for (int s = 0; s < link.Length; s++)
            {
                float f0 = s / (float)link.Length;
                float f1 = (s + 1) / (float)link.Length;
                Vector3 p0 = Vector3.Lerp(a, b, f0);
                Vector3 p1 = Vector3.Lerp(a, b, f1);
                // 横ゆれ
                Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
                float wob = Mathf.Sin(time * 5f + i * 1.3f + s) * 0.5f;
                p0 += side * wob;
                p1 += side * wob;
                OrientSegment(link[s], p0, p1, 0.35f);
            }
        }

        if (_weakPoints.Count == 0)
            EndTentacleShield();
    }

    private void EndTentacleShield()
    {
        if (!_invulnerable)
            return;

        _invulnerable = false;
        DestroyWeakPoints();
        Announce("<size=32><color=#ffd000><b>コア露出！ 今だ、叩け！</b></color></size>", 5);
        Speak("ぐっ…触腕が…ええい、忌々しい羽虫共め！", 5f);
    }

    private void DestroyWeakPoints()
    {
        foreach (Primitive[] link in _weakLinks)
            foreach (Primitive seg in link)
                SafeDestroy(seg);

        _weakLinks.Clear();

        foreach (Npc weak in _weakPoints)
        {
            try { weak?.Destroy(); } catch { /* ignore */ }
        }

        _weakPoints.Clear();
    }

    /// <summary>ボス周囲に閃光手榴弾を撒いて視界を奪う。</summary>
    private void FlashStorm(int count)
    {
        if (_boss is null)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 c = Random.insideUnitCircle * 9f;
            Vector3 pos = _boss.Position + new Vector3(c.x, 1.5f, c.y);
            Throw(ProjectileType.Scp2176, pos, Vector3.up * 2f, 1.3f);
        }
    }

    /// <summary>最寄りのターゲットへ山なりに投擲。</summary>
    private void LobAt(Player? target)
    {
        if (_boss is null || target is null)
            return;

        Vector3 origin = _boss.Position + Vector3.up * 1.5f;
        Vector3 flat = target.Position - origin;
        flat.y = 0f;
        Vector3 velocity = flat.normalized * 12f + Vector3.up * 6f;
        Throw(ProjectileType.FragGrenade, origin, velocity, 2.2f);
    }

    /// <summary>射出ヘルパー: 生成 → 信管 → 初速。</summary>
    private static void Throw(ProjectileType type, Vector3 position, Vector3 velocity, float fuse)
    {
        Projectile? proj = Projectile.CreateAndSpawn(type, position, Quaternion.identity);
        if (proj is null)
            return;

        if (fuse > 0f && proj is TimeGrenadeProjectile timed)
            timed.FuseTime = fuse;

        Rigidbody? rb = proj.GameObject != null ? proj.GameObject.GetComponent<Rigidbody>() : null;
        if (rb != null)
            rb.linearVelocity = velocity;
    }

    // ───────────────────────────────────────────────────────────
    //  撃破フィナーレ
    // ───────────────────────────────────────────────────────────
    private IEnumerator<float> Finale()
    {
        Vector3 center = _boss?.Position ?? _arenaCenter;
        SpeakerApi.TryDestroy("DanteTheme");
        _bossBar.Hide(); // バー更新を止めてから撃破演出（バーが勝利表示を上書きしないように）

        Announce("<size=36><color=#ffd000><b>DANTE 撃破</b></color></size>\n" +
                 "<size=22>業火の指揮者は沈黙した</size>", 8);
        Speak("馬鹿な…この私の業火が…消え……る…", 8f);
        Exiled.API.Features.Cassie.MessageTranslated(
            "entity neutralized . the surface is secured .",
            "対象を無力化。地上を確保した。",
            isNoisy: true);

        // 断末魔：触手を暴れさせながらボスを縮ませ、連続爆裂で派手に散らす。
        _leaping = true; // 通常移動を止めて崩れ落ちさせる
        const int waves = 5;
        for (int wave = 0; wave < waves; wave++)
        {
            if (_boss is null)
                break;

            Nova(20, 12f);
            foreach (Player target in GetTargets())
                target.SendWarheadExplosionEffect();

            // 0.45 秒を細かく刻み、触手を暴れさせつつ「見た目」を縮小（本体は不可視なのでスキン/触手側）。
            float shrinkFrom = 1f - wave / (float)waves;
            float shrinkTo = 1f - (wave + 1) / (float)waves;
            for (float e = 0f; e < 0.45f; e += Time.deltaTime)
            {
                if (_boss is null || !_boss.IsAlive)
                    break;

                _visualMul = Mathf.Max(0.02f, Mathf.Lerp(shrinkFrom, shrinkTo, e / 0.45f));
                if (_skin != null)
                    _skin.Scale = Vector3.one * (VisualScale * _visualMul);
                AnimateTentacles(Time.time);
                yield return 0f;
            }
        }

        // 生存者を全回復してご褒美に。
        foreach (Player target in GetTargets())
            target.Health = target.MaxHealth;

        Throw(ProjectileType.FragGrenade, center + Vector3.up * 1f, Vector3.zero, 0.1f);

        Cleanup(); // ボス despawn → Insurgency のみ → 討伐側（Chaos）勝利でラウンド終了
    }

    // ───────────────────────────────────────────────────────────
    //  HP 表示（汎用 BossBar に値を流すだけ。描画/再送は BossBar 管理側で行う）
    // ───────────────────────────────────────────────────────────
    private void UpdateBossBar()
    {
        _bossBar.MaxValue = _maxHp;
        _bossBar.Value = _hp;
        _bossBar.Subtitle = _phase switch
        {
            1 => "業火の序曲",
            2 => "紅蓮の軍勢",
            _ => "終焉のメルトダウン",
        };

        // 無敵中はバーの代わりに「触手を壊せ」を表示。
        _bossBar.StateText = _invulnerable
            ? $"<size=24><color=#39ff14><b>★ コア無敵 ★</b> 中央触手 残り {_weakPoints.Count}</color></size>"
            : null;
    }

    /// <summary>Dante のセリフを各プレイヤーの EffectedInfo 枠へ流す（duration で自動消去）。</summary>
    private static void Speak(string line, float duration = 5f)
    {
        string text = $"<size=22><color=#ff2a2a><b>DANTE</b></color></size>\n" +
                      $"<size=18><i><color=#ffb3b3>「{line}」</color></i></size>";

        foreach (Player player in Player.List)
        {
            if (player is null || !player.IsNotHost())
                continue;

            EffectedInfoTextProvider.Set(player, text, duration);
        }
    }

    private static void Announce(string message, ushort duration)
    {
        foreach (Player player in Player.List)
        {
            if (player is null || !player.IsNotHost())
                continue;

            player.Broadcast(duration, message, Broadcast.BroadcastFlags.Normal, true);
        }
    }

    // ───────────────────────────────────────────────────────────
    //  被弾フック（仮想 HP）
    // ───────────────────────────────────────────────────────────
    private void HookEvents()
    {
        if (_eventsHooked)
            return;

        Exiled.Events.Handlers.Player.Hurting += OnBossHurting;
        Exiled.Events.Handlers.Server.RestartingRound += OnRestarting;
        _eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!_eventsHooked)
            return;

        Exiled.Events.Handlers.Player.Hurting -= OnBossHurting;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRestarting;
        _eventsHooked = false;
    }

    private void OnRestarting() => Cleanup();

    private void OnBossHurting(HurtingEventArgs ev)
    {
        if (!_active || _boss is null || ev.Player?.ReferenceHub != _boss.ReferenceHub)
            return;

        float incoming = ev.Amount;

        // ボスは実 HP を一切失わない（フェーズ・撃破は仮想 HP で管理）。
        ev.Amount = 0f;

        // 仮想 HP の減算は「実プレイヤーの攻撃」のみ。ボス自身のグレネード巻き込みは無効。
        if (ev.Attacker is null || !ev.Attacker.IsNotHost() ||
            ev.Attacker.ReferenceHub == _boss.ReferenceHub)
            return;

        // 中央触手が残っている間はコア無敵（ダメージを仮想 HP に通さない）。
        if (_invulnerable)
        {
            if (Random.value < 0.25f)
                ev.Attacker.ShowHint("<color=#39ff14>コアは無敵だ ── 中央の触手を破壊しろ！</color>", 1.5f);
            return;
        }

        if (_hp > 0f)
            _hp = Mathf.Max(0f, _hp - incoming);
    }

    // ───────────────────────────────────────────────────────────
    //  ターゲット選定ユーティリティ
    // ───────────────────────────────────────────────────────────
    private List<Player> GetTargets()
        => Player.List
            .Where(p => p is not null && p.IsNotHost() && p.IsAlive && !p.IsScp // 中央触手(SCP)は対象外
                        && (_boss == null || p.ReferenceHub != _boss.ReferenceHub))
            .ToList();

    private Player? NearestTarget()
    {
        if (_boss is null)
            return null;

        Vector3 origin = _boss.Position;
        return GetTargets()
            .OrderBy(p => (p.Position - origin).sqrMagnitude)
            .FirstOrDefault();
    }

    private static Vector3 AveragePosition(IReadOnlyCollection<Player> players)
    {
        Vector3 sum = Vector3.zero;
        foreach (Player player in players)
            sum += player.Position;

        return sum / players.Count;
    }

    // ───────────────────────────────────────────────────────────
    //  後始末（撃破・中断・ラウンド再開で共通）
    // ───────────────────────────────────────────────────────────
    private void Cleanup()
    {
        if (!_active && _boss is null && _skin is null && _tentacles.Count == 0
            && _puddles.Count == 0 && _weakPoints.Count == 0)
            return;

        if (_ai.IsRunning)
            Timing.KillCoroutines(_ai);
        if (_waves.IsRunning)
            Timing.KillCoroutines(_waves);

        _leaping = false;
        _bodyHidden = false;
        _invulnerable = false;
        _visualMul = 1f;
        _wavesSpawned = 0;
        _bossBar.Hide();
        DestroyTentacles();
        DestroyPuddles();
        DestroyWeakPoints();
        UnhookEvents();

        try { SpeakerApi.TryDestroy("DanteTheme"); } catch { /* ignore */ }
        _theme = default;

        // EffectedInfo に残ったセリフを掃除（通常のエフェクト表示へ制御を戻す）。
        foreach (Player player in Player.List)
        {
            if (player is null || !player.IsNotHost())
                continue;

            try { EffectedInfoTextProvider.Clear(player); } catch { /* ignore */ }
        }

        if (_skin != null)
        {
            try { _skin.Destroy(); } catch { /* ignore */ }
            _skin = null;
        }

        if (_boss != null)
        {
            try { _boss.Destroy(); } catch { /* ignore */ }
            _boss = null;
        }

        Round.IsLocked = false;
        _active = false;
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() => Cleanup();
}
