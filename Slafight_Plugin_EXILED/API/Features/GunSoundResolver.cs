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
/// 銃声パケットの AudioIndex を、実行時のクリップと銃の状態から音の種類へ解決する。
/// </summary>
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
    /// この音の種類を解決する。名前付きクリップとの一致を状態より優先する。どれにも当てはまらなければ null。
    /// </summary>
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
    /// 銃の現在状態 (リロード中など) から種類を判定する。
    /// </summary>
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
    /// <paramref name="firearm"/> の登録クリップを AudioIndex 付きで全件返す。
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
