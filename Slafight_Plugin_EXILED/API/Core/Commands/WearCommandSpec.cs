using System;
using System.Collections.Generic;
using System.Globalization;
using Exiled.API.Features;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// <see cref="WearCommand"/> の key=value オプションです。
/// </summary>
internal sealed class WearCommandSpec
{
    private string slot;
    private Vector3? offset;
    private Vector3? rotation;
    private Vector3? scale;
    private string follow;
    private string show;
    private bool? ignoreInvisibility;
    private bool? replaceBody;
    private bool? hitbox;
    private float hitboxMultiplier = 1f;
    private HitboxType hitboxPart = HitboxType.Body;
    private string hitboxGroup;
    private string hitboxBlock;

    internal Color? Color { get; private set; }

    internal float? Range { get; private set; }

    internal float? Intensity { get; private set; }

    internal bool WantsHitbox => hitbox == true;

    internal static bool TryParse(IEnumerable<string> arguments, out WearCommandSpec spec, out string error)
    {
        spec = new WearCommandSpec();
        error = null;

        foreach (string argument in arguments)
        {
            int separator = argument.IndexOf('=');

            if (separator <= 0)
            {
                error = $"key=value の形ではありません: {argument}";

                return false;
            }

            if (!spec.TrySet(argument.Substring(0, separator).ToLowerInvariant(), argument.Substring(separator + 1)))
            {
                error = $"オプションを解釈できません: {argument}";

                return false;
            }
        }

        return true;
    }

    internal WearOptions ToOptions(Player player) => new WearOptions
    {
        Slot = slot ?? WearOptions.DefaultSlot,
        Target = ResolveFollow(player),
        Offset = offset ?? Vector3.zero,
        RotationOffset = rotation is { } euler ? Quaternion.Euler(euler) : null,
        Scale = scale,
        ShowToWearer = show != "others",
        Visibility = ResolveVisibility(player),
        IgnoreInvisibility = ignoreInvisibility ?? false,
        ReplaceBody = replaceBody ?? false,
        Hitboxes = WantsHitbox ? [BuildHitbox()] : [],
    };

    internal void ApplyTo(PlayerWear wear, Player player)
    {
        Quaternion? rotationOffset = rotation is { } euler ? Quaternion.Euler(euler) : null;

        wear.Follow(ResolveFollow(player), offset, rotationOffset);

        if (scale is { } value) wear.Scale = value;
        if (ignoreInvisibility is { } ignore) wear.IgnoreInvisibility = ignore;
        if (replaceBody is { } replace) wear.ReplacesBody = replace;

        if (show is not null)
        {
            wear.ShowToWearer = show != "others";
            wear.Visibility = ResolveVisibility(player);
        }

        if (hitbox == true)
            wear.AddHitbox(BuildHitbox());
        else if (hitbox == false)
            wear.ClearHitboxes();
    }

    private static bool TryParseFloat(string raw, out float value) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseVector(string raw, out Vector3 value)
    {
        value = default;
        string[] parts = raw.Split(',');

        if (parts.Length == 1 && TryParseFloat(parts[0], out float uniform))
        {
            value = Vector3.one * uniform;

            return true;
        }

        if (parts.Length != 3 ||
            !TryParseFloat(parts[0], out float x) ||
            !TryParseFloat(parts[1], out float y) ||
            !TryParseFloat(parts[2], out float z))
            return false;

        value = new Vector3(x, y, z);

        return true;
    }

    private bool TrySet(string key, string raw)
    {
        switch (key)
        {
            case "slot":
                slot = raw;

                return raw.Length > 0;
            case "offset" or "pos":
                return TryAssign<Vector3>(raw, TryParseVector, parsed => offset = parsed);
            case "rot" or "rotation":
                return TryAssign<Vector3>(raw, TryParseVector, parsed => rotation = parsed);
            case "scale":
                return TryAssign<Vector3>(raw, TryParseVector, parsed => scale = parsed);
            case "follow":
                follow = raw.ToLowerInvariant();

                return follow is "body" or "camera";
            case "show":
                show = raw.ToLowerInvariant();

                return show is "all" or "self" or "others";
            case "invis":
                return TryAssign<bool>(raw, bool.TryParse, parsed => ignoreInvisibility = parsed);
            case "body":
                return TryAssign<bool>(raw, bool.TryParse, parsed => replaceBody = parsed);
            case "hitbox":
                return TryAssign<bool>(raw, bool.TryParse, parsed => hitbox = parsed);
            case "hitmul":
                hitbox ??= true;

                return TryAssign<float>(raw, TryParseFloat, parsed => hitboxMultiplier = parsed);
            case "hitpart":
                hitbox ??= true;

                return Enum.TryParse(raw, true, out hitboxPart);
            case "hitgroup":
                hitbox ??= true;
                hitboxGroup = raw;

                return raw.Length > 0;
            case "hitblock":
                hitbox ??= true;
                hitboxBlock = raw;

                return raw.Length > 0;
            case "color":
                if (!ColorUtility.TryParseHtmlString(raw, out Color color)) return false;

                Color = color;

                return true;
            case "range":
                return TryAssign<float>(raw, TryParseFloat, parsed => Range = parsed);
            case "intensity":
                return TryAssign<float>(raw, TryParseFloat, parsed => Intensity = parsed);
            default:
                return false;
        }
    }

    private delegate bool Parser<T>(string raw, out T value);

    private static bool TryAssign<T>(string raw, Parser<T> parser, Action<T> assign)
    {
        if (!parser(raw, out T value)) return false;

        assign(value);

        return true;
    }

    private Transform ResolveFollow(Player player) => follow switch
    {
        "camera" => player.CameraTransform,
        "body" => player.Transform,
        _ => null,
    };

    private Predicate<Player> ResolveVisibility(Player player) =>
        show == "self" ? viewer => viewer.Id == player.Id : null;

    private WearHitboxOptions BuildHitbox()
    {
        string block = hitboxBlock;

        return new WearHitboxOptions
        {
            Group = hitboxGroup ?? WearHitboxOptions.DefaultGroup,
            HitboxType = hitboxPart,
            DamageMultiplier = hitboxMultiplier,
            Filter = block is null
                ? null
                : candidate => candidate.BlockName?.IndexOf(block, StringComparison.OrdinalIgnoreCase) >= 0,
        };
    }
}
