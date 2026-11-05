using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// Wear の確認用コマンドです。Schematic や AdminToy を好きな形でプレイヤーへ着せます。
/// </summary>
public sealed class WearCommand : CommandBase
{
    private const int MaxListedNames = 80;

    private const string Help =
        "使い方: slc wear <操作> ...\n" +
        "  schematic <対象> <名前> [オプション]\n" +
        "  primitive <対象> <Cube|Sphere|Capsule|Cylinder|Plane|Quad> [オプション]\n" +
        "  light <対象> [オプション]\n" +
        "  text <対象> <文字列...> [オプション]\n" +
        "  capybara <対象> [オプション]\n" +
        "  target <対象> <ShootingTargetType> [オプション]\n" +
        "  edit <対象> <slot> [オプション]   着せたまま変更\n" +
        "  remove <対象> <slot|all>\n" +
        "  list [対象]\n" +
        "  names [絞り込み]   Schematic 名の一覧\n" +
        "対象: me / ID / 名前 / * (全員)。1.2.3 で複数指定。\n" +
        "オプション (key=value):\n" +
        "  slot=名前  offset=x,y,z  rot=x,y,z  scale=n か x,y,z\n" +
        "  follow=body|camera  show=all|self|others  invis=true (透明化でも隠さない)\n" +
        "  body=true (素体を隠し名札 NPC を出す)\n" +
        "  hitbox=true  hitmul=倍率  hitpart=Body|Headshot|Limb  hitgroup=名前  hitblock=ブロック名の一部\n" +
        "  color=#RRGGBB  range=n  intensity=n (primitive / light)";

    public override Type Parent => typeof(RootCommand);

    public override string Command => "wear";

    public override string[] Aliases { get; } = ["wr"];

    public override string Usage => "wear <schematic|primitive|light|text|capybara|target|edit|remove|list|names>";

    public override string Description => "Wear の確認。Schematic や AdminToy を指定の形で着せ、当たり判定や素体の代替も試せます。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string action))
        {
            response = Help;

            return false;
        }

        switch (action.ToLowerInvariant())
        {
            case "schematic" or "sch":
                return WearSchematic(out response);
            case "primitive" or "prim":
                return WearPrimitive(out response);
            case "light":
                return WearToy(2, out response, (_, position, spec) => CreateLight(position, spec));
            case "text":
                return WearText(out response);
            case "capybara" or "capy":
                return WearToy(2, out response, (_, position, _) => Capybara.Create(null, position, null, null, false));
            case "target":
                return WearShootingTarget(out response);
            case "edit":
                return Edit(out response);
            case "remove" or "rm":
                return Remove(out response);
            case "list" or "ls":
                return List(out response);
            case "names":
                return Names(out response);
            default:
                response = Help;

                return false;
        }
    }

    private static string Format(Vector3 value) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", value.x, value.y, value.z);

    private static string DisplayName(PlayerWear wear) =>
        wear.Schematic != null ? $"Schematic {wear.Schematic.Name}" : wear.GameObject.name;

    private bool WearSchematic(out string response)
    {
        if (!TryGetArgument(2, out string name))
        {
            response = "使い方: slc wear schematic <対象> <名前> [オプション]";

            return false;
        }

        return WearEach(3, out response, (player, spec) => player.Wear(name, spec.ToOptions(player)));
    }

    private bool WearPrimitive(out string response)
    {
        if (!TryGetArgument(2, out string raw) || !Enum.TryParse(raw, true, out PrimitiveType type))
        {
            response = "使い方: slc wear primitive <対象> <Cube|Sphere|Capsule|Cylinder|Plane|Quad> [オプション]";

            return false;
        }

        return WearToy(3, out response, (_, position, spec) =>
            Primitive.Create(type, PrimitiveFlags.Visible, position, null, null, true, spec.Color ?? Color.white));
    }

    private bool WearText(out string response)
    {
        int index = 2;
        List<string> words = [];

        while (TryGetArgument(index, out string word) && !word.Contains('='))
        {
            words.Add(word);
            index++;
        }

        if (words.Count == 0)
        {
            response = "使い方: slc wear text <対象> <文字列...> [オプション]";

            return false;
        }

        string text = string.Join(" ", words);

        return WearToy(index, out response, (_, position, _) => Text.Create(position, text));
    }

    private bool WearShootingTarget(out string response)
    {
        if (!TryGetArgument(2, out string raw) || !Enum.TryParse(raw, true, out ShootingTargetType type))
        {
            string names = string.Join("|", Enum.GetNames(typeof(ShootingTargetType)));
            response = $"使い方: slc wear target <対象> <{names}> [オプション]";

            return false;
        }

        return WearToy(3, out response, (_, position, _) => ShootingTargetToy.Create(type, position));
    }

    private static AdminToy CreateLight(Vector3 position, WearCommandSpec spec)
    {
        Light light = Light.Create(position, null, null, true, spec.Color ?? Color.white);

        if (spec.Range is { } range) light.Range = range;
        if (spec.Intensity is { } intensity) light.Intensity = intensity;

        return light;
    }

    private bool WearToy(int optionIndex, out string response, Func<Player, Vector3, WearCommandSpec, AdminToy> create) =>
        WearEach(optionIndex, out response, (player, spec) =>
            player.Wear(create(player, player.Position, spec), spec.ToOptions(player)));

    private bool WearEach(int optionIndex, out string response, Func<Player, WearCommandSpec, PlayerWear> wear)
    {
        if (!TryGetTargets(1, out List<Player> targets, out response)) return false;

        if (!WearCommandSpec.TryParse(Arguments.Skip(optionIndex), out WearCommandSpec spec, out response)) return false;

        StringBuilder builder = new StringBuilder();
        bool succeeded = false;

        foreach (Player target in targets)
        {
            PlayerWear result = wear(target, spec);

            if (result is null)
            {
                builder.AppendLine($"{target.Nickname}: 着せられませんでした。");

                continue;
            }

            succeeded = true;
            builder.AppendLine($"{target.Nickname}: {DisplayName(result)} を {result.Slot} に着せました。");

            if (spec.WantsHitbox && result.Hitboxes.Count == 0)
                builder.AppendLine("  当たり判定は Schematic にしか付けられません。");
        }

        response = builder.ToString().TrimEnd();

        return succeeded;
    }

    private bool Edit(out string response)
    {
        if (!TryGetTargets(1, out List<Player> targets, out response)) return false;

        if (!TryGetArgument(2, out string slot))
        {
            response = "使い方: slc wear edit <対象> <slot> [オプション]";

            return false;
        }

        if (!WearCommandSpec.TryParse(Arguments.Skip(3), out WearCommandSpec spec, out response)) return false;

        int edited = 0;

        foreach (Player target in targets)
        {
            if (!target.TryGetWear(slot, out PlayerWear wear)) continue;

            spec.ApplyTo(wear, target);
            edited++;
        }

        response = $"{edited} 人分の {slot} を変更しました。";

        return edited > 0;
    }

    private bool Remove(out string response)
    {
        if (!TryGetTargets(1, out List<Player> targets, out response)) return false;

        if (!TryGetArgument(2, out string slot))
        {
            response = "使い方: slc wear remove <対象> <slot|all>";

            return false;
        }

        int removed = 0;

        foreach (Player target in targets)
        {
            if (slot.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                removed += target.GetWears().Count;
                target.RemoveWears();
            }
            else if (target.RemoveWear(slot))
            {
                removed++;
            }
        }

        response = $"{removed} 個外しました。";

        return removed > 0;
    }

    private bool List(out string response)
    {
        List<Player> targets;

        if (TryGetArgument(1, out _))
        {
            if (!TryGetTargets(1, out targets, out response)) return false;
        }
        else
        {
            targets = Player.List.Where(player => player.HasWear()).ToList();
        }

        StringBuilder builder = new StringBuilder();

        foreach (Player target in targets)
        {
            Describe(builder, target);
        }

        response = builder.Length == 0 ? "何も着ていません。" : builder.ToString().TrimEnd();

        return true;
    }

    private static void Describe(StringBuilder builder, Player target)
    {
        builder.AppendLine($"{target.Nickname} ({target.Id})");

        if (WearManager.GetNameplateNpc(target) is { } nameplate)
            builder.AppendLine($"  名札 NPC: {nameplate.Id} / {nameplate.Role.Type} / {nameplate.CustomInfo?.Length ?? 0} 文字");

        foreach (PlayerWear wear in target.GetWears())
        {
            builder.AppendLine(
                $"  [{wear.Slot}] {DisplayName(wear)} offset={Format(wear.Offset)} " +
                $"rot={Format(wear.RotationOffset.eulerAngles)} scale={Format(wear.Scale)} follow={wear.Target.name} " +
                $"self={wear.ShowToWearer} invis={wear.IgnoreInvisibility} body={wear.ReplacesBody}");

            foreach (SchematicCustomHitbox hitbox in wear.Hitboxes)
            {
                int enabled = hitbox.Colliders.Count(collider => collider != null && collider.enabled);

                builder.AppendLine(
                    $"    hitbox {hitbox.Group}: collider {enabled}/{hitbox.Colliders.Count} " +
                    $"x{hitbox.DamageMultiplier:0.##} netId={hitbox.NetworkId}");
            }
        }
    }

    private bool Names(out string response)
    {
        if (!Directory.Exists(global::ProjectMER.ProjectMER.SchematicsDir))
        {
            response = "Schematic のフォルダが見つかりません。";

            return false;
        }

        TryGetArgument(1, out string filter);

        List<string> names = Directory.GetDirectories(global::ProjectMER.ProjectMER.SchematicsDir)
            .Select(Path.GetFileName)
            .Where(name => filter is null || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string listed = string.Join(", ", names.Take(MaxListedNames));
        string more = names.Count > MaxListedNames ? $" ほか {names.Count - MaxListedNames} 件" : string.Empty;

        response = $"Schematic {names.Count} 件: {listed}{more}";

        return true;
    }

    private bool TryGetTargets(int index, out List<Player> targets, out string response)
    {
        targets = [];
        response = null;

        if (!TryGetArgument(index, out string raw))
            raw = "me";

        if (raw is "*" or "all")
        {
            targets = Player.List.Where(player => player.IsSafePlayer()).ToList();
        }
        else
        {
            foreach (string part in raw.Split('.'))
            {
                Player player = part is "me" or "@me" or "self" ? Sender :
                    int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? Player.Get(id) :
                    Player.Get(part);

                if (player is not null && !targets.Contains(player))
                    targets.Add(player);
            }
        }

        if (targets.Count > 0) return true;

        response = $"対象が見つかりません: {raw}";

        return false;
    }
}
