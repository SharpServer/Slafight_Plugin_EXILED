#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using AudioPooling;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Structs;
using UnityEngine;
using Item = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// CItem の Firearm 特化派生。Damage / MagazineSize / Scale を virtual で受け取り、
/// Spawn / Give 双方の経路で Firearm へ焼き付ける。
/// </summary>
/// <remarks>
/// <para>
/// MagazineSize / MaxMagazineAmmo は「アタッチメント補正後の実効容量」として扱う。
/// EXILED の MaxMagazineAmmo setter は内部の base capacity を変更するため、ここで
/// attachment modifier を逆算して、派生側が見た目通りの容量を指定できるようにする。
/// </para>
/// <para>
/// 弾薬状態（マガジン / バレル）は <see cref="OnCaptureState"/> / <see cref="OnRestoreState"/>
/// 経由でスナップショット・復元できる。CItemHybrid のモード切替で、物理アイテムを差し替えても
/// モードごとの弾数が保持される。
/// </para>
/// <para>
/// リロードは <see cref="ReloadAmmoMultiplier"/>（装填1発あたりの予備弾消費倍率）に基づき
/// 決定論的に処理する。発射前にマガジン/予備弾をスナップショットし、リロード完了時に
/// ネイティブ結果を上書きするため、アタッチメント補正に左右されず安定する。
/// </para>
/// </remarks>
public abstract class CItemWeapon : CItem
{
    // 発射前の TotalAmmo（AmmoDrain > 1 のときの手動消費計算用）
    private readonly Dictionary<ushort, int> _totalAmmoBeforeShot = new();

    // リロード開始時のスナップショット: serial → (マガジン弾, 予備弾)
    private readonly Dictionary<ushort, (int Magazine, int Reserve)> _reloadSnapshot = new();

    // 音差し替え用: (プレイヤー ID, 音の識別子) → 最後に鳴らした Time.time
    private readonly Dictionary<(int PlayerId, string SoundKey), float> _lastOverrideAudioTime = new();

    /// <summary>1 撃あたりのダメージ。負値ならバニラのダメージを使う (override 無し)。</summary>
    protected virtual float Damage => -1f;

    /// <summary>実効マガジン容量。0 なら override 無し (バニラ容量)。</summary>
    protected virtual byte MagazineSize => 0;

    /// <summary>実効最大装弾数。0 なら override 無し。デフォルトでは MagazineSize と同じ値を使う。</summary>
    protected virtual ushort MaxMagazineAmmo => MagazineSize;

    /// <summary>生成 / 付与直後の装填数。0 なら override 無し。デフォルトでは MaxMagazineAmmo と同じ値を使う。</summary>
    protected virtual ushort InitialMagazineAmmo => MaxMagazineAmmo;

    /// <summary>1 発射あたりの消費弾数。0 なら override 無し。</summary>
    protected virtual byte AmmoDrain => 0;

    /// <summary>
    /// リロードでマガジンに1発装填するごとに消費する予備弾の倍率。1 = バニラ等価。
    /// 例: 2 にすると 1 発込めるごとに予備弾を 2 発消費する（重い弾を表現）。
    /// </summary>
    protected virtual int ReloadAmmoMultiplier => 1;

    /// <summary>AmmoDrain 分の総弾数を持っていない場合に発射を止めるか。</summary>
    protected virtual bool RequireAmmoDrainAvailableToShoot => AmmoDrain > 1;

    /// <summary>Pickup の見た目スケール。Vector3.one ならバニラサイズ。</summary>
    protected virtual Vector3 Scale => Vector3.one;

    /// <summary>固定したいアタッチメント一覧。空ならアタッチメントは変更しない。</summary>
    protected virtual AttachmentName[] Attachments => [];

    /// <summary>Attachments 適用前に既存アタッチメントを消すか。</summary>
    protected virtual bool ClearAttachmentsBeforeApplying => Attachments.Length > 0;

    /// <summary>プレイヤーによるアタッチメント変更を許可するか。</summary>
    protected virtual bool AllowAttachmentChanges => true;

    // ==== 銃器サウンドの上書き ====
    //
    // 差し替え対象の指定には、実行時にゲームから読み出せる情報だけを使う:
    //   1. GunSoundKind  … ゲーム側の名前付きクリップフィールドと参照比較して決める意味的な種類
    //                       （GunSoundResolver 参照）。発砲音だけは MixerChannel.Weapons で判定。
    //   2. AudioClip 名   … AudioModule._registeredClips[AudioIndex].name。
    //   3. MixerChannel  … 機構音を丸ごと扱いたいとき用（DefaultSfx = 機構音全般）。
    //   4. AudioIndex     … 最後の逃げ道。
    // AudioIndex の意味を並べた固定表は持たないので、銃種が追加されても壊れない。

    /// <summary>
    /// バニラの銃器サウンドを抑制 / 差し替えするか。
    /// <see cref="OverrideAudio"/> や各 <c>OverrideSoundsBy*</c>、
    /// <see cref="LogGunSoundClips"/> のいずれかが設定されていれば自動的に true になる。
    /// </summary>
    protected virtual bool OverrideGunSounds =>
        !string.IsNullOrWhiteSpace(OverrideAudio)
        || OverrideSounds.Count > 0
        || LogGunSoundClips;

    /// <summary>
    /// 発砲音の代わりに鳴らす音声ファイル名（<c>AudioReferences</c> ディレクトリ配下）。
    /// <see cref="OverrideAudioKinds"/> の各種類へ同じクリップを割り当てる簡易記法。
    /// </summary>
    protected virtual string? OverrideAudio => null;

    /// <summary>
    /// <see cref="OverrideAudio"/> を適用する音の種類。既定は発砲音のみ。
    /// サプレッサーの有無や弾種でクリップが変わっても発砲音として判定される。
    /// </summary>
    protected virtual GunSoundKind[] OverrideAudioKinds => [GunSoundKind.Gunshot];

    /// <summary>
    /// 差し替え定義。<see cref="OverrideAudio"/> より優先される。
    /// </summary>
    /// <remarks>
    /// <para>
    /// キーは <see cref="GunSoundSelector"/> で、<see cref="GunSoundKind"/> / クリップ名 /
    /// <see cref="MixerChannel"/> / AudioIndex のどれからでも暗黙変換されるため、
    /// <b>1 つの辞書に混ぜて書ける</b>。値も <c>string</c> から暗黙変換されるので、
    /// ファイル名だけならそのまま文字列で書ける。
    /// バニラ音を消すだけなら <see cref="GunSoundOverride.Silent"/> を指定する。
    /// </para>
    /// <para>
    /// 同じ音に複数の指定が当たる場合は AudioIndex → クリップ名 → <see cref="GunSoundKind"/> →
    /// <see cref="MixerChannel"/> の順で、より具体的な方が勝つ。
    /// </para>
    /// <para>
    /// <see cref="GunSoundKind"/> は銃種に依存せず判定できるので優先して使う。
    /// マガジン挿入やボルト操作のようにゲーム側が名前を持たない音は
    /// <see cref="GunSoundClips"/> のクリップ名で指定する。
    /// </para>
    /// <example>
    /// <code>
    /// protected override IReadOnlyDictionary&lt;GunSoundSelector, GunSoundOverride&gt; OverrideSounds => new Dictionary&lt;GunSoundSelector, GunSoundOverride&gt;
    /// {
    ///     [GunSoundKind.Gunshot]             = new("MyGun_Fire.ogg", Range: 25f),
    ///     [GunSoundKind.DryFire]             = "MyGun_NoAmmo.ogg",
    ///     [GunSoundClips.Crossvec.MagInsert] = "MyGun_MagIn.ogg",
    ///     [MixerChannel.DefaultSfx]          = GunSoundOverride.Silent,
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    protected virtual IReadOnlyDictionary<GunSoundSelector, GunSoundOverride> OverrideSounds
        => EmptySoundOverrides;

    /// <summary>
    /// この CItem の銃が鳴らす音の AudioIndex / クリップ名 / 種類 / チャンネルをログへ出す。
    /// <see cref="OverrideSoundsByClip"/> や <see cref="OverrideSoundsByIndex"/> に
    /// 書く値を調べるための開発用スイッチ。
    /// </summary>
    protected virtual bool LogGunSoundClips => false;

    /// <summary>差し替え音の既定可聴距離。</summary>
    protected virtual float OverrideAudioRange => 15f;

    /// <summary>差し替え音の既定音量。</summary>
    protected virtual float OverrideAudioVolume => 1f;

    /// <summary>差し替え音を 3D 空間音として鳴らすか（既定値）。</summary>
    protected virtual bool OverrideAudioIsSpatial => true;

    /// <summary>
    /// 音の種類ごとに、プレイヤー 1 人あたり確保するスピーカー数（既定値）。
    /// 連射時に音が重なる分だけ必要。
    /// </summary>
    protected virtual int OverrideAudioVoices => 2;

    /// <summary>
    /// 同じ音の再生要求がこの間隔より短く連続した場合、同一イベントとみなして捨てる（既定値）。
    /// </summary>
    protected virtual float OverrideAudioMinInterval => 0.04f;

    /// <summary>基底側でリロード弾薬処理を行うか（容量上書き or 倍率カスタム時に有効）。</summary>
    private bool ManagesReloadAmmo => MaxMagazineAmmo > 0 || ReloadAmmoMultiplier > 1;

    // ==== Spawn / Give 経路: Item を作って MagazineSize / Scale を焼き付ける ====

    protected override Pickup? CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(BaseItem);
        if (item == null) return null;

        ApplyFirearmCustomization(item);
        var pickup = item.CreatePickup(position);
        if (pickup == null)
            return null;

        if (Scale != Vector3.one)
            pickup.Scale = Scale;

        return pickup;
    }

    protected override void CustomizeItem(Item item)
    {
        ApplyFirearmCustomization(item);
        base.CustomizeItem(item);
    }

    /// <summary>
    /// Item ベースで適用できる Firearm カスタマイズ。
    /// さらに特殊な処理が必要な場合だけ override する。
    /// </summary>
    protected virtual void ApplyFirearmCustomization(Item item)
    {
        if (item is not Firearm firearm)
            return;

        ApplyFirearmAttachments(firearm);
        ApplyFirearmStats(firearm);
    }

    /// <summary>最大装弾数 / 初期装填数 / 弾薬消費を Firearm に適用する。</summary>
    protected virtual void ApplyFirearmStats(Firearm firearm)
    {
        int effectiveMax = MaxMagazineAmmo;
        if (effectiveMax > 0)
            ApplyEffectiveMaxMagazineAmmo(firearm, effectiveMax);

        if (InitialMagazineAmmo > 0)
        {
            int initialAmmo = effectiveMax > 0
                ? Math.Min(InitialMagazineAmmo, effectiveMax)
                : InitialMagazineAmmo;
            firearm.MagazineAmmo = initialAmmo;
        }

        if (AmmoDrain > 0)
            firearm.AmmoDrain = AmmoDrain;
    }

    /// <summary>アタッチメント設定を Firearm に適用する。</summary>
    protected virtual void ApplyFirearmAttachments(Firearm firearm)
    {
        var attachments = Attachments;
        if (attachments.Length == 0)
            return;

        if (ClearAttachmentsBeforeApplying)
            firearm.ClearAttachments();

        foreach (var attachment in attachments)
            firearm.AddAttachment(attachment);
    }

    // ==== 状態キャプチャ / 復元（CItemHybrid のモード切替で使用） ====

    /// <summary>
    /// マガジン側合計弾数（マガジン弾 + 薬室弾）/ バレル弾のスナップショット。
    /// 薬室（chamber）弾を合算して保持するのは、復元後の <see cref="OnModeActivated"/> で
    /// ボルトをサイクルさせると 1 発が薬室へ移るため、合計で持っておかないと切替ごとに
    /// 薬室分の 1 発を失うため。
    /// </summary>
    private sealed record WeaponAmmoState(int MagazineTotal, int Barrel);

    protected override object? OnCaptureState(Item item)
    {
        if (item is not Firearm firearm)
            return null;

        return new WeaponAmmoState(firearm.MagazineAmmo + GetChambered(firearm), firearm.BarrelAmmo);
    }

    protected override void OnRestoreState(Item item, object? state)
    {
        if (item is not Firearm firearm || state is not WeaponAmmoState ammo)
            return;

        firearm.MagazineAmmo = ammo.MagazineTotal;
        firearm.BarrelAmmo   = ammo.Barrel;
    }

    /// <summary>
    /// モード切替で手持ちになった直後。強制持ち替えだと自動火器が未 cock のまま残り
    /// 発射不能になるため、ボルトを 1 回サイクルさせて cock + 薬室装填 + 同期する。
    /// （ServerCycleAction はマガジンから 1 発を薬室へ移すので合計弾数は維持される。）
    /// </summary>
    protected override void OnModeActivated(Item item)
    {
        if (item is not Firearm firearm)
            return;

        GetAutomaticModule(firearm)?.ServerCycleAction();
    }

    // ==== ダメージ override ====

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (Damage >= 0f)
            ev.Amount = Damage;
    }

    protected override void OnShooting(ShootingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (ShouldBlockShooting(ev))
        {
            ev.IsAllowed = false;
            return;
        }

        if (AmmoDrain > 1)
            _totalAmmoBeforeShot[ev.Firearm.Serial] = ev.Firearm.TotalAmmo;
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        ApplyManualAmmoDrain(ev.Firearm);
    }

    // ==== リロード（決定論的な弾薬取り回し） ====

    public override void RegisterEvents()
    {
        Player.ReloadingWeapon += OnInternalReloading;
        Player.ReloadedWeapon  += OnInternalReloaded;
        Exiled.Events.Handlers.Item.ChangingAttachments += OnInternalChangingAttachments;

        if (OverrideGunSounds)
        {
            PreloadOverrideAudioClips();

            Player.SendingGunSound   += OnInternalSendingGunSound;
            Player.ReceivingGunSound += OnInternalReceivingGunSound;
            Player.Left              += OnInternalPlayerLeft;

            // 派生側が OnWaitingForPlayers で base を呼ばない場合でもスピーカーを残さないよう、
            // ラウンド間の掃除は基底が直接購読して行う。
            Server.WaitingForPlayers += ClearOverrideAudioSpeakers;
        }

        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.ReloadingWeapon -= OnInternalReloading;
        Player.ReloadedWeapon  -= OnInternalReloaded;
        Exiled.Events.Handlers.Item.ChangingAttachments -= OnInternalChangingAttachments;

        if (OverrideGunSounds)
        {
            Player.SendingGunSound   -= OnInternalSendingGunSound;
            Player.ReceivingGunSound -= OnInternalReceivingGunSound;
            Player.Left              -= OnInternalPlayerLeft;
            Server.WaitingForPlayers -= ClearOverrideAudioSpeakers;
            ClearOverrideAudioSpeakers();
        }

        base.UnregisterEvents();
    }

    private void OnInternalChangingAttachments(ChangingAttachmentsEventArgs ev)
    {
        if (AllowAttachmentChanges) return;
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
    }

    private void OnInternalReloading(ReloadingWeaponEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        if (ShouldBlockReloading(ev))
        {
            // これ以上装填できない（容量満タン or 倍率コスト分の予備弾不足）
            ev.IsAllowed = false;
            return;
        }

        if (ManagesReloadAmmo)
        {
            // ネイティブリロードが弾薬を動かす前にスナップショットしておく。
            _reloadSnapshot[ev.Firearm.Serial] =
                (ev.Firearm.MagazineAmmo, ev.Player.GetAmmo(ev.Firearm.AmmoType));
        }

        OnReloading(ev);
    }

    private void OnInternalReloaded(ReloadedWeaponEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        // スナップショットがあれば決定論的に上書きする（ネイティブ結果は破棄）。
        if (_reloadSnapshot.Remove(ev.Firearm.Serial, out var snapshot))
        {
            var firearm   = ev.Firearm;
            var ammoType  = firearm.AmmoType;
            int effMax    = GetConfiguredMaxMagazineAmmo(firearm);
            int chambered = GetChambered(firearm);
            int multiplier = Math.Max(1, ReloadAmmoMultiplier);

            int preMag    = snapshot.Magazine;
            int reserve   = snapshot.Reserve;

            // チャンバー分を除いたマガジン装填可能数
            int loadable   = Math.Max(0, effMax - preMag - chambered);
            int affordable = reserve / multiplier;
            int loaded     = Math.Min(loadable, affordable);

            firearm.MagazineAmmo = preMag + loaded;
            ev.Player.SetAmmo(ammoType, (ushort)Math.Max(0, reserve - (loaded * multiplier)));
        }

        OnReloaded(ev);
    }

    /// <summary>派生がリロード開始タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloading(ReloadingWeaponEventArgs ev) { }

    /// <summary>派生がリロード完了タイミングをフックしたい場合用。</summary>
    protected virtual void OnReloaded(ReloadedWeaponEventArgs ev) { }

    /// <summary>
    /// これ以上装填できないときリロードを止めるか。
    /// 容量満タン、または倍率コスト分の予備弾を持っていない場合に止める。
    /// </summary>
    protected virtual bool ShouldBlockReloading(ReloadingWeaponEventArgs ev)
    {
        if (!ManagesReloadAmmo) return false;

        var firearm    = ev.Firearm;
        int effMax     = GetConfiguredMaxMagazineAmmo(firearm);
        int chambered  = GetChambered(firearm);
        int loadable   = Math.Max(0, effMax - firearm.MagazineAmmo - chambered);
        int affordable = ev.Player.GetAmmo(firearm.AmmoType) / Math.Max(1, ReloadAmmoMultiplier);

        return Math.Min(loadable, affordable) <= 0;
    }

    // ==== 銃器サウンドの上書き ====

    private static readonly IReadOnlyDictionary<GunSoundSelector, GunSoundOverride> EmptySoundOverrides =
        new Dictionary<GunSoundSelector, GunSoundOverride>();

    private sealed record ResolvedGunSoundOverrides(
        Dictionary<int, GunSoundOverride> ByIndex,
        Dictionary<string, GunSoundOverride> ByClip,
        Dictionary<GunSoundKind, GunSoundOverride> ByKind,
        Dictionary<MixerChannel, GunSoundOverride> ByChannel);

    /// <summary>
    /// 解決済みの差し替え定義。virtual プロパティは毎回 Dictionary を作り直す実装になりがちなので、
    /// 発砲ごとの再構築を避けるため初回アクセス時に 1 度だけ組み立てて保持する。
    /// </summary>
    private ResolvedGunSoundOverrides? _resolvedSoundOverrides;

    private ResolvedGunSoundOverrides ResolvedSoundOverrides
        => _resolvedSoundOverrides ??= BuildSoundOverrides();

    /// <summary>
    /// 1 つの <see cref="OverrideSounds"/> 辞書を、解決時に引きやすい 4 つの索引へ振り分ける。
    /// </summary>
    private ResolvedGunSoundOverrides BuildSoundOverrides()
    {
        var resolved = new ResolvedGunSoundOverrides(
            new Dictionary<int, GunSoundOverride>(),
            // クリップ名は Unity のアセット名。大文字小文字と前後の空白は無視して引けるようにする
            // （"FSP9 Mag In" のように空白を含む名前が実在するため、定義側と実行時名の
            //   両方に同じ正規化を掛ける）。
            new Dictionary<string, GunSoundOverride>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<GunSoundKind, GunSoundOverride>(),
            new Dictionary<MixerChannel, GunSoundOverride>());

        // OverrideAudio は簡易記法なので先に展開し、明示指定の方で上書きさせる。
        if (!string.IsNullOrWhiteSpace(OverrideAudio))
        {
            var shortcut = new GunSoundOverride(OverrideAudio);
            foreach (var kind in OverrideAudioKinds)
                resolved.ByKind[kind] = shortcut;
        }

        foreach (var entry in OverrideSounds)
        {
            switch (entry.Key.Type)
            {
                case GunSoundSelector.SelectorType.Index:
                    resolved.ByIndex[entry.Key.Index] = entry.Value;
                    break;

                case GunSoundSelector.SelectorType.Clip:
                    if (!string.IsNullOrWhiteSpace(entry.Key.Clip))
                        resolved.ByClip[NormalizeClipName(entry.Key.Clip!)] = entry.Value;
                    break;

                case GunSoundSelector.SelectorType.Kind:
                    resolved.ByKind[entry.Key.Kind] = entry.Value;
                    break;

                case GunSoundSelector.SelectorType.Channel:
                    resolved.ByChannel[entry.Key.Channel] = entry.Value;
                    break;
            }
        }

        return resolved;
    }

    /// <summary>この CItem の差し替え音スピーカー名の接頭辞。音の識別子とプレイヤー ID を後ろに付ける。</summary>
    private string OverrideAudioSpeakerPrefix => UniqueKey + "_GunSound_";

    /// <summary>クリップ名の照合用正規化。定義側と実行時名の両方に同じものを掛ける。</summary>
    private static string NormalizeClipName(string clipName) => clipName.Trim();

    /// <summary>
    /// <see cref="LogGunSoundClips"/> 用に、この銃の AudioIndex ↔ クリップ名の全対応表を
    /// 1 度だけログへ出す。個別の音が鳴るのを待たずに一覧が得られる。
    /// </summary>
    private void LogClipTableOnce(Firearm firearm)
    {
        if (!_clipTableLogged.Add(firearm.Type))
            return;

        var entries = GunSoundResolver.DumpClips(firearm);
        if (entries.Count == 0)
        {
            Exiled.API.Features.Log.Warn($"[{GetType().Name}] {firearm.Type}: AudioModule のクリップ一覧を取得できませんでした。");
            return;
        }

        Exiled.API.Features.Log.Info(
            $"[{GetType().Name}] {firearm.Type} registered clips ({entries.Count}):\n  " +
            string.Join("\n  ", entries));
    }

    /// <summary>クリップ一覧を既にログへ出した ItemType。</summary>
    private readonly HashSet<ItemType> _clipTableLogged = [];

    /// <summary>差し替えに使う全クリップを先読みする。初回再生時のデコード遅延を避けるため。</summary>
    private void PreloadOverrideAudioClips()
    {
        var resolved = ResolvedSoundOverrides;
        foreach (var soundOverride in resolved.ByIndex.Values
                     .Concat(resolved.ByClip.Values)
                     .Concat(resolved.ByKind.Values)
                     .Concat(resolved.ByChannel.Values))
        {
            if (!string.IsNullOrWhiteSpace(soundOverride.Audio))
                SpeakerApi.TryLoadClip(soundOverride.Audio!);
        }
    }

    /// <summary>
    /// この音に対する差し替え定義を解決する。
    /// AudioIndex → クリップ名 → 種類 → ミキサーチャンネルの順に探し、
    /// 無ければ false（バニラのまま）。
    /// </summary>
    private bool TryResolveSoundOverride(
        Firearm firearm,
        int audioIndex,
        MixerChannel channel,
        out GunSoundOverride? soundOverride,
        out string soundKey)
    {
        var resolved = ResolvedSoundOverrides;

        if (resolved.ByIndex.TryGetValue(audioIndex, out soundOverride))
        {
            soundKey = "i" + audioIndex;
            return true;
        }

        if (resolved.ByClip.Count > 0
            && GunSoundResolver.GetClipName(firearm, audioIndex) is { } clipName
            && resolved.ByClip.TryGetValue(NormalizeClipName(clipName), out soundOverride))
        {
            soundKey = "c" + clipName;
            return true;
        }

        if (resolved.ByKind.Count > 0
            && GunSoundResolver.Resolve(firearm, audioIndex, channel) is { } kind
            && resolved.ByKind.TryGetValue(kind, out soundOverride))
        {
            soundKey = kind.ToString();
            return true;
        }

        if (resolved.ByChannel.TryGetValue(channel, out soundOverride))
        {
            soundKey = channel.ToString();
            return true;
        }

        soundOverride = null;
        soundKey = string.Empty;
        return false;
    }

    private void OnInternalSendingGunSound(SendingGunSoundEventArgs ev)
    {
        if (!Check(ev.Item)) return;

        if (LogGunSoundClips)
        {
            LogClipTableOnce(ev.Firearm);
            Exiled.API.Features.Log.Info(
                $"[{GetType().Name}] {ev.Firearm.Type} sound: index={ev.AudioIndex}" +
                $" clip='{GunSoundResolver.GetClipName(ev.Firearm, ev.AudioIndex) ?? "<unknown>"}'" +
                $" kind={GunSoundResolver.Resolve(ev.Firearm, ev.AudioIndex, ev.MixerChannel)?.ToString() ?? "<none>"}" +
                $" channel={ev.MixerChannel} range={ev.Range}");
        }

        if (!TryResolveSoundOverride(ev.Firearm, ev.AudioIndex, ev.MixerChannel, out var soundOverride, out string soundKey))
            return;

        // 定義があるならバニラ音は常に抑制する。Audio が null なら「消すだけ」。
        ev.IsAllowed = false;

        if (ev.Player == null || soundOverride!.Audio is not { } audio || string.IsNullOrWhiteSpace(audio))
            return;

        // 同じ音・同じプレイヤーの連続要求だけを弾く。別種類の音は互いに干渉しない。
        var dedupeKey = (ev.Player.Id, soundKey);
        float minInterval = soundOverride.MinInterval ?? OverrideAudioMinInterval;
        if (_lastOverrideAudioTime.TryGetValue(dedupeKey, out float last)
            && Time.time - last < minInterval)
            return;

        _lastOverrideAudioTime[dedupeKey] = Time.time;

        // スピーカーは鳴らすたびに作らず、音の種類 × プレイヤーごとに Voices 個を使い回す。
        SpeakerApi.PlayOneShot(
            audio,
            $"{OverrideAudioSpeakerPrefix}{soundKey}_{ev.Player.Id}",
            ev.SendingPosition,
            voices: soundOverride.Voices ?? OverrideAudioVoices,
            isSpatial: soundOverride.IsSpatial ?? OverrideAudioIsSpatial,
            maxDistance: soundOverride.Range ?? OverrideAudioRange,
            volume: soundOverride.Volume ?? OverrideAudioVolume);
    }

    private void OnInternalReceivingGunSound(ReceivingGunSoundEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        if (!TryResolveSoundOverride(ev.Firearm, ev.AudioIndex, ev.MixerChannel, out _, out _)) return;
        ev.IsAllowed = false;
    }

    private void OnInternalPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;

        int playerId = ev.Player.Id;
        string suffix = "_" + playerId;
        string prefix = OverrideAudioSpeakerPrefix;

        foreach (string name in SpeakerApi.GetAudioPlayerNames()
                     .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                 && n.EndsWith(suffix, StringComparison.Ordinal))
                     .ToArray())
        {
            SpeakerApi.TryDestroy(name);
        }

        foreach (var key in _lastOverrideAudioTime.Keys.Where(k => k.PlayerId == playerId).ToArray())
            _lastOverrideAudioTime.Remove(key);
    }

    /// <summary>この CItem が確保した差し替え音スピーカーを全て破棄する。</summary>
    private void ClearOverrideAudioSpeakers()
    {
        string prefix = OverrideAudioSpeakerPrefix;
        foreach (string name in SpeakerApi.GetAudioPlayerNames()
                     .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            SpeakerApi.TryDestroy(name);
        }

        _lastOverrideAudioTime.Clear();
    }

    /// <summary>発射を基底側で止めるか。</summary>
    protected virtual bool ShouldBlockShooting(ShootingEventArgs ev)
        => RequireAmmoDrainAvailableToShoot
           && AmmoDrain > 1
           && ev.Firearm.TotalAmmo < AmmoDrain;

    private void ApplyManualAmmoDrain(Firearm firearm)
    {
        if (AmmoDrain <= 1) return;
        if (!_totalAmmoBeforeShot.Remove(firearm.Serial, out int beforeShotTotal)) return;

        int consumedByGame = Math.Max(0, beforeShotTotal - firearm.TotalAmmo);
        int remainingDrain = AmmoDrain - consumedByGame;
        if (remainingDrain <= 0) return;

        DrainStoredAmmo(firearm, ref remainingDrain);
    }

    private static void DrainStoredAmmo(Firearm firearm, ref int amount)
    {
        if (amount <= 0) return;

        int magazineDrain = Math.Min(firearm.MagazineAmmo, amount);
        if (magazineDrain > 0)
        {
            firearm.MagazineAmmo -= magazineDrain;
            amount -= magazineDrain;
        }

        int barrelDrain = Math.Min(firearm.BarrelAmmo, amount);
        if (barrelDrain > 0)
        {
            firearm.BarrelAmmo -= barrelDrain;
            amount -= barrelDrain;
        }
    }

    private int GetConfiguredMaxMagazineAmmo(Firearm firearm)
        => MaxMagazineAmmo > 0 ? MaxMagazineAmmo : firearm.MaxMagazineAmmo;

    /// <summary>銃の AutomaticActionModule。無ければ null（リボルバー/ParticleDisruptor 等）。</summary>
    private static AutomaticActionModule? GetAutomaticModule(Firearm firearm)
        => firearm.Base.Modules.OfType<AutomaticActionModule>().FirstOrDefault();

    /// <summary>チャンバー（薬室）に装填済みの弾数。AutomaticActionModule が無ければ 0。</summary>
    private static int GetChambered(Firearm firearm)
        => GetAutomaticModule(firearm)?.AmmoStored ?? 0;

    private static void ApplyEffectiveMaxMagazineAmmo(Firearm firearm, int effectiveMax)
    {
        int attachmentModifier = GetMagazineCapacityModifier(firearm);
        int baseCapacity = Math.Max(0, effectiveMax - attachmentModifier);
        firearm.MaxMagazineAmmo = baseCapacity;
    }

    private static int GetMagazineCapacityModifier(Firearm firearm)
    {
        try
        {
            return (int)firearm.Base.AttachmentsValue(AttachmentParam.MagazineCapacityModifier);
        }
        catch
        {
            return 0;
        }
    }
}
