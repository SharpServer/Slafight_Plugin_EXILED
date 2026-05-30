using System;
using System.Collections;
using System.Collections.Generic;
using Exiled.API.Features;
using RueI.API;
using RueI.API.Elements;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

public static class RueiHintExtensions
{
    private const string RueiPlusTagName = "RueiPlus";
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<int, Dictionary<string, string>> DynamicTexts = new();
    private static readonly Dictionary<string, uint> TagVersions = new();
    private static readonly HashSet<string> ActiveDynamicTags = new();
    
    public static void ShowRuei(this Player player, string info, string tagName, float displayTimeInSeconds = 5f, int hintpos = 200)
        => player.ShowRuei(info, new Tag(tagName), displayTimeInSeconds, hintpos);

    public static void ShowRuei(this Player player, string info, Tag tag, float displayTimeInSeconds = 5f, int hintpos = 200)
    {
        if (!IsValid(player) || tag == null)
            return;

        try
        {
            var display = RueDisplay.Get(player.ReferenceHub);
            var key = MakeKey(player.Id, tag);
            uint version;

            lock (SyncRoot)
            {
                version = NextVersion(key);
            }

            try { display.Remove(tag); }
            catch
            {
                // ignored
            }

            if (string.IsNullOrEmpty(info))
                return;

            display.Show(tag, new BasicElement(hintpos, info), displayTimeInSeconds);

            if (displayTimeInSeconds > 0f)
                player.ReferenceHub.StartCoroutine(RemoveTagAfterDelay(player, tag, displayTimeInSeconds, version));
        }
        catch (Exception ex)
        {
            Log.Debug($"ShowRuei failed: {ex.Message}");
        }
    }

    public static void ShowRueiPlus(this Player player, string info, float displayTimeInSeconds = 5f, int hintpos = 200)
        => player.ShowRuei(info, RueiPlusTagName, displayTimeInSeconds, hintpos);

    public static void ClearRueiPlus(this Player player)
        => player.ClearRuei(RueiPlusTagName);

    public static void ClearRuei(this Player player, string tagName)
    {
        if (!IsValid(player) || string.IsNullOrEmpty(tagName))
            return;

        try
        {
            var tag = new Tag(tagName);
            lock (SyncRoot)
            {
                NextVersion(MakeKey(player.Id, tag));
                if (DynamicTexts.TryGetValue(player.Id, out var texts))
                    texts.Remove(tagName);
                ActiveDynamicTags.Remove(MakeKey(player.Id, tagName));
            }

            RueDisplay.Get(player.ReferenceHub).Remove(tag);
        }
        catch
        {
            // ignored
        }
    }

    public static void SetDynamicRuei(this Player player, string tagName, string text, int hintpos = 500)
    {
        if (!IsValid(player) || string.IsNullOrEmpty(tagName))
            return;

        lock (SyncRoot)
        {
            if (!DynamicTexts.TryGetValue(player.Id, out var texts))
            {
                texts = new Dictionary<string, string>(StringComparer.Ordinal);
                DynamicTexts[player.Id] = texts;
            }

            texts[tagName] = text ?? string.Empty;
        }

        EnsureDynamicRuei(player, tagName, hintpos);
    }

    public static void ClearDynamicRuei(this Player player, string tagName)
        => player.ClearRuei(tagName);

    public static void ClearAllDynamicRuei(this Player player)
    {
        if (!IsValid(player))
            return;

        List<string> tags;
        lock (SyncRoot)
        {
            tags = DynamicTexts.TryGetValue(player.Id, out var texts)
                ? [..texts.Keys]
                : [];
        }

        foreach (var tag in tags)
            player.ClearDynamicRuei(tag);
    }

    private static void EnsureDynamicRuei(Player player, string tagName, int hintpos)
    {
        var activeKey = MakeKey(player.Id, tagName);
        lock (SyncRoot)
        {
            if (ActiveDynamicTags.Contains(activeKey))
                return;

            ActiveDynamicTags.Add(activeKey);
        }

        try
        {
            var display = RueDisplay.Get(player.ReferenceHub);
            var tag = new Tag(tagName);

            try { display.Remove(tag); }
            catch { }

            display.Show(tag, new DynamicElement(hintpos, hub => GetDynamicText(hub, tagName)));
        }
        catch (Exception ex)
        {
            lock (SyncRoot)
                ActiveDynamicTags.Remove(activeKey);

            Log.Debug($"EnsureDynamicRuei failed: {ex.Message}");
        }
    }

    private static string GetDynamicText(ReferenceHub hub, string tagName)
    {
        var player = Player.Get(hub);
        if (player == null)
            return string.Empty;

        lock (SyncRoot)
        {
            return DynamicTexts.TryGetValue(player.Id, out var texts) &&
                   texts.TryGetValue(tagName, out var text)
                ? text
                : string.Empty;
        }
    }

    private static IEnumerator RemoveTagAfterDelay(Player player, Tag tag, float delaySeconds, uint version)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (!IsValid(player))
            yield break;

        var key = MakeKey(player.Id, tag);
        lock (SyncRoot)
        {
            if (!TagVersions.TryGetValue(key, out var current) || current != version)
                yield break;
        }

        try
        {
            RueDisplay.Get(player.ReferenceHub).Remove(tag);
        }
        catch (Exception ex)
        {
            Log.Debug($"Failed to remove RueI element: {ex.Message}");
        }
    }

    private static bool IsValid(Player player)
    {
        try
        {
            return player != null &&
                   player.IsConnected &&
                   !player.IsHost &&
                   !player.IsNPC &&
                   player.ReferenceHub != null &&
                   player.ReferenceHub.connectionToClient != null;
        }
        catch
        {
            return false;
        }
    }

    private static uint NextVersion(string key)
    {
        TagVersions.TryGetValue(key, out var version);
        version++;
        TagVersions[key] = version;
        return version;
    }

    private static string MakeKey(int playerId, Tag tag)
        => MakeKey(playerId, tag.Id ?? tag.GetHashCode().ToString());

    private static string MakeKey(int playerId, string tagName)
        => playerId + ":" + tagName;
}
