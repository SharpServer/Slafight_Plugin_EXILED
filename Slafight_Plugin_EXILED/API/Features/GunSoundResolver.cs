#nullable enable

using System.Collections.Generic;
using System.Linq;
using AudioPooling;
using Exiled.API.Features.Items;
using InventorySystem.Items.Firearms.Modules;
using Slafight_Plugin_EXILED.API.Enums;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// <c>SendingGunSound</c> / <c>ReceivingGunSound</c> の AudioIndex を、
/// 実行時のゲームオブジェクトから意味付きの情報へ解決する。
/// </summary>
/// <remarks>
/// <para>
/// 判定材料はすべてゲーム側の実データ:
/// </para>
/// <list type="bullet">
/// <item>
/// AudioIndex は <c>AudioModule.ServerSendToNearbyPlayers</c> に渡される
/// <c>_clipToIndex[clip]</c> の値、すなわち <c>AudioModule._registeredClips</c> のインデックス。
/// 同じリストを引き直せば元の <see cref="AudioClip"/> が取れる。
/// </item>
/// <item>
/// 取れた <see cref="AudioClip"/> を各アクションモジュールの名前付きフィールド
/// （<c>AutomaticActionModule._dryfireSound</c> 等）と参照比較して種類を決める。
/// </item>
/// <item>
/// 発砲音だけは <c>AudioModule.PlayGunshot</c> が唯一の
/// <see cref="MixerChannel.Weapons"/> 利用者なので、チャンネルで判定する。
/// </item>
/// </list>
/// <para>
/// このため AudioIndex → 音の種類 の固定表を持つ必要がない。
/// </para>
/// </remarks>
public static class GunSoundResolver
{
    /// <summary>
    /// <paramref name="firearm"/> の <paramref name="audioIndex"/> 番目に登録されている
    /// <see cref="AudioClip"/>。AudioModule が無い / 範囲外なら null。
    /// </summary>
    public static AudioClip? GetClip(Firearm? firearm, int audioIndex)
    {
        var clips = firearm?.Base?.Modules?.OfType<AudioModule>().FirstOrDefault()?._registeredClips;
        if (clips == null || audioIndex < 0 || audioIndex >= clips.Count)
            return null;

        return clips[audioIndex];
    }

    /// <summary>クリップ名。取得できなければ null。</summary>
    public static string? GetClipName(Firearm? firearm, int audioIndex)
    {
        var clip = GetClip(firearm, audioIndex);
        return clip == null ? null : clip.name;
    }

    /// <summary>
    /// この音の種類を解決する。どれにも当てはまらなければ null。
    /// </summary>
    /// <remarks>
    /// 判定順は「名前付きクリップとの参照比較」→「モジュールの現在状態」。
    /// 前者の方が具体的なので優先する。たとえばリボルバーのリロード中にコッキング音が鳴った場合、
    /// <see cref="GunSoundKind.Reload"/> ではなく
    /// <see cref="GunSoundKind.RevolverCocking"/> として扱われる。
    /// </remarks>
    public static GunSoundKind? Resolve(Firearm? firearm, int audioIndex, MixerChannel channel)
    {
        // PlayGunshot だけが Weapons チャンネルを使うので、クリップを見る必要がない。
        if (channel == MixerChannel.Weapons)
            return GunSoundKind.Gunshot;

        if (firearm?.Base?.Modules == null)
            return null;

        var clip = GetClip(firearm, audioIndex);
        if (clip != null && ResolveByClip(firearm, clip) is { } byClip)
            return byClip;

        return ResolveByModuleState(firearm);
    }

    /// <summary>
    /// 銃の現在状態から種類を判定する。
    /// アニメーションイベント由来でゲーム側にも名前が無い音（マガジン・ボルト等）を、
    /// 「今リロード中か」といった状態でまとめて分類するために使う。
    /// </summary>
    /// <remarks>
    /// リロード / アンロード / 抜き出しの状態はいずれもサーバー側で更新される
    /// （<c>ServerTryReload</c> が <c>IsReloading</c> を立て、アニメーションイベントの
    /// <c>StopReloadingAndUnloading</c> が下ろす）。音の送出も同じアニメーションイベント経由なので、
    /// 音が飛ぶ瞬間の状態を見れば分類できる。
    /// </remarks>
    private static GunSoundKind? ResolveByModuleState(Firearm firearm)
    {
        foreach (var module in firearm.Base.Modules)
        {
            if (module is not IReloaderModule reloader)
                continue;

            if (reloader.IsReloading)
                return GunSoundKind.Reload;
            if (reloader.IsUnloading)
                return GunSoundKind.Unload;
        }

        // 抜き出し中は Reload / Unload より弱い判定として扱う。
        foreach (var module in firearm.Base.Modules)
        {
            if (module is IEquipperModule { IsEquipped: false })
                return GunSoundKind.Equip;
        }

        return null;
    }

    private static bool IsDisruptorActionClip(DisruptorAudioModule module, AudioClip clip)
        => module._singleShotAudio.ClipActionNormal == clip
           || module._singleShotAudio.ClipActionLast == clip
           || module._rapidFireAudio.ClipActionNormal == clip
           || module._rapidFireAudio.ClipActionLast == clip;

    /// <summary>この銃に登録されている全クリップの (AudioIndex, クリップ名, 判定できた種類)。</summary>
    public readonly struct ClipEntry(int index, string name, GunSoundKind? kind)
    {
        public int Index { get; } = index;
        public string Name { get; } = name;
        public GunSoundKind? Kind { get; } = kind;

        public override string ToString()
            => $"[{Index}] {Name}" + (Kind.HasValue ? $" ({Kind})" : string.Empty);
    }

    /// <summary>
    /// <paramref name="firearm"/> の <c>AudioModule._registeredClips</c> を全件ダンプする。
    /// AudioIndex とクリップ名の対応をその場で調べられるので、
    /// <c>OverrideSoundsByClip</c> / <c>OverrideSoundsByIndex</c> に書く値を撃たずに確認できる。
    /// </summary>
    public static IReadOnlyList<ClipEntry> DumpClips(Firearm? firearm)
    {
        var clips = firearm?.Base?.Modules?.OfType<AudioModule>().FirstOrDefault()?._registeredClips;
        if (clips == null)
            return [];

        var entries = new List<ClipEntry>(clips.Count);
        for (int index = 0; index < clips.Count; index++)
        {
            var clip = clips[index];
            entries.Add(new ClipEntry(
                index,
                clip == null ? "<null>" : clip.name,
                clip == null ? null : ResolveByClip(firearm!, clip)));
        }

        return entries;
    }

    /// <summary>
    /// ゲーム側の名前付きクリップフィールドとの参照比較だけで種類を判定する。
    /// 状態にもチャンネルにも依存しないので、<see cref="DumpClips"/> からも使える。
    /// </summary>
    private static GunSoundKind? ResolveByClip(Firearm firearm, AudioClip clip)
    {
        if (firearm.Base?.Modules == null)
            return null;

        foreach (var module in firearm.Base.Modules)
        {
            switch (module)
            {
                case AutomaticActionModule automatic:
                    if (automatic._dryfireSound == clip)
                        return GunSoundKind.DryFire;
                    if (IsAutomaticGunshotClip(automatic, clip))
                        return GunSoundKind.Gunshot;
                    break;

                case DoubleActionModule doubleAction:
                    if (doubleAction._dryFireClip == clip)
                        return GunSoundKind.DryFire;
                    if (doubleAction._doubleActionClip == clip)
                        return GunSoundKind.RevolverDoubleAction;
                    if (doubleAction._cockingClip == clip)
                        return GunSoundKind.RevolverCocking;
                    if (doubleAction._decockingClip == clip)
                        return GunSoundKind.RevolverDecocking;
                    if (Contains(doubleAction._fireClips, clip))
                        return GunSoundKind.Gunshot;
                    break;

                case PumpActionModule pump:
                    if (pump._dryFireClip == clip)
                        return GunSoundKind.DryFire;
                    if (Contains(pump._shotClipPerBarrelIndex, clip))
                        return GunSoundKind.Gunshot;
                    break;

                case DisruptorAudioModule disruptor:
                    if (IsDisruptorActionClip(disruptor, clip))
                        return GunSoundKind.DisruptorAction;
                    if (disruptor._singleShotAudio.ClipFiringNormal == clip
                        || disruptor._singleShotAudio.ClipFiringLast == clip
                        || disruptor._rapidFireAudio.ClipFiringNormal == clip
                        || disruptor._rapidFireAudio.ClipFiringLast == clip)
                        return GunSoundKind.Gunshot;
                    break;
            }
        }

        return null;
    }

    private static bool IsAutomaticGunshotClip(AutomaticActionModule module, AudioClip clip)
    {
        var definitions = module._gunshotSounds;
        if (definitions == null)
            return false;

        foreach (var definition in definitions)
        {
            if (Contains(definition.RandomSounds, clip))
                return true;
        }

        return false;
    }

    private static bool Contains(AudioClip[]? clips, AudioClip clip)
    {
        if (clips == null)
            return false;

        foreach (var candidate in clips)
        {
            if (candidate == clip)
                return true;
        }

        return false;
    }
}
