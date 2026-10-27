using System;
using System.Collections.Generic;
using System.Linq;
using AudioPooling;
using Exiled.API.Features.Items;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Structs;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomFirearm.GunSounds"/> を引きやすい索引へ振り分けたものです。
/// </summary>
internal sealed class GunSoundTable
{
    private readonly Dictionary<int, GunSoundOverride> byIndex = new();

    // "FSP9 Mag In" のように空白を含む実名があるため、前後空白と大文字小文字は無視する。
    private readonly Dictionary<string, GunSoundOverride> byClip = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<GunSoundKind, GunSoundOverride> byKind = new();
    private readonly Dictionary<MixerChannel, GunSoundOverride> byChannel = new();

    private GunSoundTable(bool logClips)
    {
        LogClips = logClips;
    }

    /// <summary>差し替え定義が 1 つも無いか。</summary>
    public bool IsEmpty => byIndex.Count + byClip.Count + byKind.Count + byChannel.Count == 0;

    /// <summary>鳴った音をログへ出すか。</summary>
    public bool LogClips { get; }

    /// <summary>表を組み立て、使うクリップを先読みします。</summary>
    public static GunSoundTable Build(IReadOnlyDictionary<GunSoundSelector, GunSoundOverride> sounds, bool logClips)
    {
        GunSoundTable table = new(logClips);
        if (sounds == null)
            return table;

        foreach (KeyValuePair<GunSoundSelector, GunSoundOverride> entry in sounds)
        {
            GunSoundOverride sound = entry.Value ?? GunSoundOverride.Silent;
            GunSoundSelector selector = entry.Key;
            switch (selector.Type)
            {
                case GunSoundSelector.SelectorType.Index:
                    table.byIndex[selector.Index] = sound;
                    break;
                case GunSoundSelector.SelectorType.Clip when !string.IsNullOrWhiteSpace(selector.Clip):
                    table.byClip[selector.Clip] = sound;
                    break;
                case GunSoundSelector.SelectorType.Kind:
                    table.byKind[selector.Kind] = sound;
                    break;
                case GunSoundSelector.SelectorType.Channel:
                    table.byChannel[selector.Channel] = sound;
                    break;
            }
        }

        // 初回発砲時のデコード待ちを避ける。
        foreach (string audio in table.byIndex.Values
                     .Concat(table.byClip.Values)
                     .Concat(table.byKind.Values)
                     .Concat(table.byChannel.Values)
                     .Select(sound => sound.Audio)
                     .Where(audio => !string.IsNullOrWhiteSpace(audio))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SpeakerApi.TryLoadClip(audio);
        }

        return table;
    }

    /// <summary>
    /// AudioIndex → クリップ名 → 種類 → チャンネルの順に探します。
    /// <paramref name="key"/> は同じ定義に当たった音を同一視するための識別子です。
    /// </summary>
    public bool TryResolve(Firearm firearm, int audioIndex, MixerChannel channel, out GunSoundOverride sound, out string key)
    {
        if (byIndex.TryGetValue(audioIndex, out sound))
        {
            key = "i" + audioIndex;
            return true;
        }

        if (byClip.Count > 0
            && GunSoundResolver.GetClipName(firearm, audioIndex) is { } clipName
            && byClip.TryGetValue(clipName.Trim(), out sound))
        {
            key = "c" + clipName.Trim();
            return true;
        }

        if (byKind.Count > 0
            && GunSoundResolver.Resolve(firearm, audioIndex, channel) is { } kind
            && byKind.TryGetValue(kind, out sound))
        {
            key = kind.ToString();
            return true;
        }

        if (byChannel.TryGetValue(channel, out sound))
        {
            key = channel.ToString();
            return true;
        }

        key = null;
        return false;
    }
}
