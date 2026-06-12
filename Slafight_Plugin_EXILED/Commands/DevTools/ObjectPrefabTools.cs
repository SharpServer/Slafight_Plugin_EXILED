using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;
using Newtonsoft.Json;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

// .sl objprefab ...
public class SpawnObjectPrefab : ICommand
{
    public string Command => "objprefab";
    public string[] Aliases { get; } = ["opf", "op"];
    public string Description => "ObjectPrefabをMER風に設置・選択・編集・保存する開発用ツール";

    private const float DefaultCreateDistance = 2.5f;
    private const float LookSelectMaxDistance = 35f;
    private const float LookSelectFallbackRadius = 1.35f;

    private static readonly Dictionary<Player, ObjectPrefab> Grabbing = new();
    private static readonly Dictionary<Player, float> GrabDistance = new();
    private static readonly Dictionary<Player, Vector3> GrabLocalOffset = new();
    private static readonly Dictionary<Player, Quaternion> GrabRotationOffset = new();
    private static readonly Dictionary<Player, bool> GrabLockRotation = new();
    private static readonly Dictionary<Player, CoroutineHandle> GrabCoroutines = new();

    private static readonly Dictionary<Player, ObjectPrefab> Selected = new();

    public static void CleanupPlayer(Player player)
    {
        var grabKeys = GrabCoroutines.Keys
            .Concat(Grabbing.Keys)
            .Concat(GrabDistance.Keys)
            .Concat(GrabLocalOffset.Keys)
            .Concat(GrabRotationOffset.Keys)
            .Concat(GrabLockRotation.Keys)
            .Distinct()
            .Where(key => IsSameOrInvalidPlayer(key, player))
            .ToArray();

        foreach (var key in grabKeys)
        {
            if (key != null)
                UngrabInternal(key);
        }

        foreach (var key in Selected.Keys.Where(key => IsSameOrInvalidPlayer(key, player)).ToArray())
            Selected.Remove(key);
    }

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"You don't have permission to execute this command. Required permission: slperm.{Command}";
            return false;
        }

        var player = Player.Get(sender);
        if (player is null)
        {
            response = "Player not found.";
            return false;
        }

        if (arguments.Count == 0)
        {
            response = GetUsage();
            return false;
        }

        string sub = arguments.At(0).ToLower();

        switch (sub)
        {
            case "help":
            case "?":
                response = GetUsage();
                return true;

            case "spawn":
            case "create":
            case "cr":
                return Spawn(arguments, player, out response);

            case "save":
            case "s":
                return Save(arguments, player, out response);

            case "saveall":
            case "sa":
                return SaveAll(arguments, player, out response);

            case "load":
            case "l":
                return Load(arguments, player, out response);

            case "list":
            case "ls":
                return List(arguments, player, out response);

            case "types":
            case "prefabs":
            case "classes":
                return Types(arguments, out response);

            case "maps":
                return Maps(arguments, out response);

            case "remove":
            case "delete":
            case "del":
                return Remove(arguments, player, out response);

            case "clear":   return Clear(arguments, player, out response);
            case "tp":
            case "goto":
                return Tp(arguments, player, out response);

            case "move":
            case "addpos":
                return Move(arguments, player, out response);

            case "rot":
            case "addrot":
                return Rotate(arguments, player, out response);

            case "setpos":  return SetPos(arguments, player, out response);
            case "setrot":  return SetRot(arguments, player, out response);
            case "scale":
            case "scl":
                return Scale(arguments, player, out response);

            case "grab":    return Grab(arguments, player, out response);
            case "grabpos": return GrabPos(arguments, player, out response);
            case "ungrab":  return Ungrab(arguments, player, out response);
            case "offset":  return Offset(arguments, player, out response);
            case "grot":    return GrabRotate(arguments, player, out response);
            case "bring":   return Bring(arguments, player, out response);
            case "bringpos":return BringPos(arguments, player, out response);

            case "max":     return SetMaxRooms(arguments, player, out response);

            case "sel":
            case "select":
                return Select(arguments, player, out response);

            case "mod":
            case "modify":
            case "m":
                return Mod(arguments, player, out response);

            default:
                response = GetUsage();
                return false;
        }
    }

    private string GetUsage() =>
        "Usage:\n" +
        "  .sl objprefab create <PrefabClass> [x y z] [AutoDestroySeconds] [grab|grabpos] [Option=Value]\n" +
        "  .sl objprefab select [InstanceID|look|near|none]\n" +
        "  .sl objprefab delete [InstanceID]\n" +
        "  .sl objprefab modify <info|position|rotation|scale|max|autodestroy|bring>\n" +
        "  .sl objprefab save <MapName> / load <MapName> / saveall <MapName> [BaseMapName]\n" +
        "  .sl objprefab list / types [filter] / maps\n" +
        "  .sl objprefab clear\n" +
        "Legacy shortcuts still work:\n" +
        "  .sl objprefab move [InstanceID] <dx> <dy> <dz>\n" +
        "  .sl objprefab rot [InstanceID] <pitch> <yaw> <roll>\n" +
        "  .sl objprefab setpos [InstanceID] <x> <y> <z>\n" +
        "  .sl objprefab setrot [InstanceID] <pitch> <yaw> <roll>\n" +
        "  .sl objprefab grab [InstanceID]\n" +
        "  .sl objprefab grabpos [InstanceID]\n" +
        "  .sl objprefab ungrab\n" +
        "  .sl objprefab offset <forwardDelta> | <rightDelta> <upDelta> <forwardDelta>\n" +
        "  .sl objprefab grot <pitch> <yaw> <roll>\n" +
        "  .sl objprefab bring [InstanceID]\n" +
        "  .sl objprefab bringpos [InstanceID]\n" +
        "  .sl objprefab max [InstanceID] <count>\n";

    private static IEnumerable<Type> GetPrefabTypes()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ObjectPrefab)))
            .OrderBy(t => t.Name);
    }

    private static bool TryResolvePrefabType(string input, out Type prefabType, out string response)
    {
        prefabType = null;
        response = string.Empty;

        var exact = GetPrefabTypes()
            .FirstOrDefault(t => t.Name.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                                 t.FullName?.Equals(input, StringComparison.OrdinalIgnoreCase) == true ||
                                 t.FullName?.EndsWith("." + input, StringComparison.OrdinalIgnoreCase) == true);

        if (exact != null)
        {
            prefabType = exact;
            return true;
        }

        var matches = GetPrefabTypes()
            .Where(t => t.Name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (matches.Count == 1)
        {
            prefabType = matches[0];
            return true;
        }

        response = matches.Count > 1
            ? $"Prefab class '{input}' is ambiguous: {string.Join(", ", matches.Select(t => t.Name))}"
            : $"Prefab class '{input}' not found. Use '.sl objprefab types {input}' to search.";
        return false;
    }

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);

    private static bool TryReadVector(ArraySegment<string> args, int index, out Vector3 value)
    {
        value = Vector3.zero;
        if (args.Count < index + 3)
            return false;

        if (!TryParseFloat(args.At(index), out var x) ||
            !TryParseFloat(args.At(index + 1), out var y) ||
            !TryParseFloat(args.At(index + 2), out var z))
        {
            return false;
        }

        value = new Vector3(x, y, z);
        return true;
    }

    private static Vector3 GetLookPosition(Player player)
    {
        var origin = player.CameraTransform.position;
        var forward = player.CameraTransform.forward;
        return Physics.Raycast(origin, forward, out RaycastHit hit, LookSelectMaxDistance)
            ? hit.point
            : origin + forward * DefaultCreateDistance;
    }

    private static string FormatVector(Vector3 value)
        => $"{value.x:F2}, {value.y:F2}, {value.z:F2}";

    private static string FormatPrefab(ObjectPrefab prefab)
        => $"[{prefab.ObjectInstanceID}] {prefab.GetType().Name}";

    private bool TryResolveTarget(
        ArraySegment<string> args,
        Player player,
        int index,
        out ObjectPrefab prefab,
        out int consumed,
        out string response,
        bool allowValueStart = false)
    {
        prefab = null;
        consumed = 0;
        response = string.Empty;

        if (args.Count > index)
        {
            var id = args.At(index);
            if (id.Equals("@sel", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("selected", StringComparison.OrdinalIgnoreCase))
            {
                if (Selected.TryGetValue(player, out var selectedByToken) &&
                    InstanceManager.Get(selectedByToken.ObjectInstanceID) != null)
                {
                    prefab = selectedByToken;
                    consumed = 1;
                    return true;
                }

                response = "No prefab selected.";
                return false;
            }

            var byId = InstanceManager.Get(id) as ObjectPrefab;
            if (byId != null)
            {
                prefab = byId;
                consumed = 1;
                return true;
            }

            if (!allowValueStart || !TryParseFloat(id, out _))
            {
                response = $"Prefab {id} not found.";
                return false;
            }
        }

        if (Selected.TryGetValue(player, out var selected) && InstanceManager.Get(selected.ObjectInstanceID) != null)
        {
            prefab = selected;
            return true;
        }

        response = "No prefab selected. Use '.sl objprefab select' while looking at one, or pass an InstanceID.";
        return false;
    }

    private bool TryFindLookPrefab(Player player, out ObjectPrefab prefab, out float forwardDistance)
    {
        prefab = null;
        forwardDistance = 0f;

        var origin = player.CameraTransform.position;
        var forward = player.CameraTransform.forward.normalized;
        var bestScore = float.MaxValue;

        foreach (var candidate in InstanceManager.GetAll().OfType<ObjectPrefab>())
        {
            var toObject = candidate.Position - origin;
            var projected = Vector3.Dot(toObject, forward);
            if (projected < 0.2f || projected > LookSelectMaxDistance)
                continue;

            var closestOnRay = origin + forward * projected;
            var distanceFromRay = Vector3.Distance(candidate.Position, closestOnRay);
            var radius = Mathf.Max(LookSelectFallbackRadius, candidate.ToySearchRadius);
            if (distanceFromRay > radius)
                continue;

            var score = distanceFromRay + projected * 0.02f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            prefab = candidate;
            forwardDistance = projected;
        }

        return prefab != null;
    }

    private bool TryFindNearestPrefab(Player player, float radius, out ObjectPrefab prefab)
    {
        prefab = InstanceManager.GetAll()
            .OfType<ObjectPrefab>()
            .Where(p => Vector3.Distance(player.Position, p.Position) <= radius)
            .OrderBy(p => Vector3.Distance(player.Position, p.Position))
            .FirstOrDefault();

        return prefab != null;
    }

    private void StartGrab(Player player, ObjectPrefab prefab, bool positionOnly)
    {
        if (Grabbing.TryGetValue(player, out _))
            UngrabInternal(player);

        Grabbing[player] = prefab;
        var localOffset = Quaternion.Inverse(player.CameraTransform.rotation) *
                          (prefab.Position - player.CameraTransform.position);
        if (localOffset.sqrMagnitude < 0.01f)
            localOffset = Vector3.forward * DefaultCreateDistance;

        GrabLocalOffset[player] = localOffset;
        GrabDistance[player] = Mathf.Max(0.5f, localOffset.magnitude);
        GrabRotationOffset[player] = positionOnly
            ? Quaternion.identity
            : Quaternion.Inverse(Quaternion.Euler(0, player.CameraTransform.rotation.eulerAngles.y, 0)) * prefab.Rotation;
        GrabLockRotation[player] = positionOnly;

        var handle = Timing.RunCoroutine(GrabFollowCoroutine(player));
        GrabCoroutines[player] = handle;
    }

    // ===== spawn =====
    private bool Spawn(ArraySegment<string> args, Player player, out string response)
    {
        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab create <PrefabClass> [x y z] [AutoDestroySeconds] [grab|grabpos] [Option=Value]";
            return false;
        }

        string prefabTypeName = args.At(1);

        if (!TryResolvePrefabType(prefabTypeName, out var prefabType, out response))
            return false;

        var position = GetLookPosition(player);
        int index = 2;
        float autoDestroyTime = -1f;
        bool startGrab = false;
        bool startGrabPos = false;
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryReadVector(args, index, out var explicitPosition))
        {
            position = explicitPosition;
            index += 3;
        }

        for (; index < args.Count; index++)
        {
            var token = args.At(index);
            if (token.Equals("grab", StringComparison.OrdinalIgnoreCase))
            {
                startGrab = true;
                continue;
            }

            if (token.Equals("grabpos", StringComparison.OrdinalIgnoreCase))
            {
                startGrabPos = true;
                continue;
            }

            var splitIndex = token.IndexOf('=');
            if (splitIndex > 0)
            {
                var key = token[..splitIndex];
                var value = token[(splitIndex + 1)..];
                if (key.Equals("ad", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("autodestroy", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParseFloat(value, out autoDestroyTime))
                    {
                        response = $"AutoDestroySeconds must be a number: {value}";
                        return false;
                    }
                    continue;
                }

                options[key] = value;
                continue;
            }

            if (TryParseFloat(token, out autoDestroyTime))
                continue;

            response = $"Unknown create option '{token}'. Use grab, grabpos, AutoDestroySeconds, or Option=Value.";
            return false;
        }

        var prefab = (ObjectPrefab)Activator.CreateInstance(prefabType)!;

        prefab.Position = position;
        prefab.Rotation = Quaternion.Euler(0, player.CameraTransform.rotation.eulerAngles.y, 0);
        prefab.Scale = Vector3.one;
        prefab.AutoDestroyEnabled = autoDestroyTime > 0f;
        prefab.AutoDestroyTime = autoDestroyTime;
        prefab.MaxRooms = 1;

        if (options.Count > 0)
            prefab.ApplyOptions(options);

        prefab.Create();
        Selected[player] = prefab;

        if (startGrab || startGrabPos)
            StartGrab(player, prefab, startGrabPos);

        response = $"Created and selected {FormatPrefab(prefab)} at ({FormatVector(prefab.Position)}).";
        if (startGrab || startGrabPos)
            response += startGrabPos ? " GrabPos enabled." : " Grab enabled.";
        return true;
    }

    // ===== save =====
    private bool Save(ArraySegment<string> args, Player player, out string response)
    {
        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab save <MapName>";
            return false;
        }

        string mapName = args.At(1);

        var prefabs = InstanceManager.GetAll().Where(p => p.IsSaveable).ToList();
        if (!prefabs.Any())
        {
            response = "No ObjectPrefab instances to save.";
            return false;
        }

        var cfg = new ObjectPrefabConfig();

        foreach (var p in prefabs)
        {
            var closestRoom = Room.List
                .OrderBy(r => Vector3.Distance(r.Position, p.Position))
                .FirstOrDefault();

            if (closestRoom == null)
            {
                Log.Warn($"[Save] Skipping prefab {p.ObjectInstanceID}: No closest room found");
                continue;
            }

            var room = closestRoom;
            var roomType = room.Type;

            Quaternion inv = Quaternion.Inverse(room.Rotation);
            Vector3 localPos = inv * (p.Position - room.Position);
            Quaternion localRot = inv * p.Rotation;

            var op = p as ObjectPrefab;

            cfg.Prefabs.Add(new PrefabSaveData
            {
                PrefabType = p.GetType().FullName,
                RoomType = roomType,
                LocalPosition = localPos,
                LocalRotationEuler = localRot.eulerAngles,
                Scale = p.Scale,
                MaxRooms = op?.MaxRooms ?? 1,
                AutoDestroyTime = p.AutoDestroyTime,
                AutoDestroyEnabled = p.AutoDestroyEnabled,
                Options = p.CollectOptions(),
            });
        }

        cfg.Save(mapName);

        response = $"Saved {cfg.Prefabs.Count} prefabs to map '{mapName}'.";
        return true;
    }

    // ===== saveall =====
    // ロード済みマップのファイルデータと現在のInstanceManagerオブジェクトをマージして保存する。
    // Usage: .sl objprefab saveall <SaveMapName> [BaseMapName]
    // BaseMapName省略時は最後にロードしたマップを使用。
    private bool SaveAll(ArraySegment<string> args, Player player, out string response)
    {
        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab saveall <SaveMapName> [BaseMapName]";
            return false;
        }

        string saveMapName = args.At(1);
        string? baseMapName = args.Count >= 3 ? args.At(2) : ObjectPrefabLoader.LastLoadedMapName;

        // --- 1. 現在の InstanceManager のオブジェクトを PrefabSaveData に変換 ---
        var currentPrefabs = InstanceManager.GetAll().Where(p => p.IsSaveable).ToList();
        var currentSaveDataList = new List<PrefabSaveData>();

        foreach (var p in currentPrefabs)
        {
            var closestRoom = Room.List
                .OrderBy(r => Vector3.Distance(r.Position, p.Position))
                .FirstOrDefault();

            if (closestRoom == null)
            {
                Log.Warn($"[SaveAll] Skipping prefab {p.ObjectInstanceID}: No closest room found");
                continue;
            }

            var room = closestRoom;
            Quaternion inv = Quaternion.Inverse(room.Rotation);
            Vector3 localPos = inv * (p.Position - room.Position);
            Quaternion localRot = inv * p.Rotation;

            var op = p as ObjectPrefab;

            currentSaveDataList.Add(new PrefabSaveData
            {
                PrefabType = p.GetType().FullName,
                RoomType = room.Type,
                LocalPosition = localPos,
                LocalRotationEuler = localRot.eulerAngles,
                Scale = p.Scale,
                MaxRooms = op?.MaxRooms ?? 1,
                AutoDestroyTime = p.AutoDestroyTime,
                AutoDestroyEnabled = p.AutoDestroyEnabled,
                Options = p.CollectOptions(),
            });
        }

        // --- 2. ベースマップのファイルデータを読み込み ---
        var basePrefabs = new List<PrefabSaveData>();
        if (!string.IsNullOrEmpty(baseMapName))
        {
            var baseCfg = ObjectPrefabConfig.Load(baseMapName);
            basePrefabs = baseCfg.Prefabs;
        }

        // --- 3. マージ: ファイルのオブジェクトのうち、InstanceManager に存在しないものを追加 ---
        // 同一判定: PrefabType + RoomType + LocalPosition が近い (距離 < 0.5f)
        const float positionThreshold = 0.5f;
        int mergedFromFile = 0;

        foreach (var fileData in basePrefabs)
        {
            bool alreadyExists = currentSaveDataList.Any(c =>
                c.PrefabType == fileData.PrefabType &&
                c.RoomTypeName == fileData.RoomTypeName &&
                Vector3.Distance(c.LocalPosition, fileData.LocalPosition) < positionThreshold);

            if (!alreadyExists)
            {
                currentSaveDataList.Add(fileData);
                mergedFromFile++;
            }
        }

        // --- 4. 保存 ---
        var cfg = new ObjectPrefabConfig { Prefabs = currentSaveDataList };
        cfg.Save(saveMapName);

        response = $"Saved {currentSaveDataList.Count} prefabs to map '{saveMapName}' " +
                   $"(InstanceManager: {currentSaveDataList.Count - mergedFromFile}, " +
                   $"Merged from '{baseMapName ?? "none"}': {mergedFromFile}).";
        return true;
    }

    // ===== load =====
    private bool Load(ArraySegment<string> args, Player player, out string response)
    {
        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab load <MapName>";
            return false;
        }

        string mapName = args.At(1);
        int count = ObjectPrefabLoader.LoadMap(mapName);

        response = $"Loaded {count} prefabs from map '{mapName}'.";
        return true;
    }

    // ===== list =====
    private bool List(ArraySegment<string> args, Player player, out string response)
    {
        Selected.TryGetValue(player, out var selected);
        var all = InstanceManager.GetAll()
            .Select(p =>
            {
                var closestRoom = Room.List
                    .OrderBy(r => Vector3.Distance(r.Position, p.Position))
                    .FirstOrDefault();
                var roomName = closestRoom?.Name ?? "Unknown";
                var op = p as ObjectPrefab;
                var marker = selected != null && selected.ObjectInstanceID == p.ObjectInstanceID ? "* " : "  ";
                return $"{marker}[{p.ObjectInstanceID}] {p.GetType().Name} @ {roomName} " +
                       $"Pos({p.Position.x:F1},{p.Position.y:F1},{p.Position.z:F1}) MaxRooms:{op?.MaxRooms ?? 1}";
            })
            .ToList();

        response = all.Any()
            ? string.Join("\n", all.Take(50))
            : "No ObjectPrefab instances.";
        return true;
    }

    private bool Types(ArraySegment<string> args, out string response)
    {
        var filter = args.Count >= 2 ? args.At(1) : null;
        var types = GetPrefabTypes()
            .Select(t => t.Name)
            .Where(name => string.IsNullOrWhiteSpace(filter) ||
                           name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        response = types.Count > 0
            ? "ObjectPrefab types:\n  " + string.Join("\n  ", types)
            : $"No ObjectPrefab types matched '{filter}'.";
        return types.Count > 0;
    }

    private bool Maps(ArraySegment<string> args, out string response)
    {
        Directory.CreateDirectory(ObjectPrefabConfig.DirectoryPath);
        var maps = Directory.GetFiles(ObjectPrefabConfig.DirectoryPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name)
            .ToList();

        response = maps.Count > 0
            ? "ObjectPrefab maps:\n  " + string.Join("\n  ", maps)
            : $"No ObjectPrefab maps found in {ObjectPrefabConfig.DirectoryPath}.";
        return true;
    }

    // ===== remove =====
    private bool Remove(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        var id = prefab.ObjectInstanceID;
        prefab.Destroy();
        if (Selected.TryGetValue(player, out var selected) && selected.ObjectInstanceID == id)
            Selected.Remove(player);

        response = $"Removed prefab {id}.";
        return true;
    }

    // ===== clear =====
    private bool Clear(ArraySegment<string> args, Player player, out string response)
    {
        InstanceManager.ClearAll();
        response = "Cleared all ObjectPrefab instances.";
        return true;
    }

    // ===== tp =====
    private bool Tp(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        player.Position = prefab.Position + Vector3.up * 1.2f;
        response = $"Teleported to {FormatPrefab(prefab)}.";
        return true;
    }

    // ===== move =====
    private bool Move(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (!TryReadVector(args, valueIndex, out var delta))
        {
            response = "Usage: .sl objprefab move [InstanceID] <dx> <dy> <dz>";
            return false;
        }

        prefab.Position += delta;
        response = $"Moved {FormatPrefab(prefab)} by ({FormatVector(delta)}).";
        return true;
    }

    // ===== setpos =====
    private bool SetPos(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (!TryReadVector(args, valueIndex, out var position))
        {
            response = "Usage: .sl objprefab setpos [InstanceID] <x> <y> <z>";
            return false;
        }

        prefab.Position = position;
        response = $"Set {FormatPrefab(prefab)} position to ({FormatVector(position)}).";
        return true;
    }

    // ===== rot =====
    private bool Rotate(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (!TryReadVector(args, valueIndex, out var rotation))
        {
            response = "Usage: .sl objprefab rot [InstanceID] <pitch> <yaw> <roll>";
            return false;
        }

        var currentEuler = prefab.Rotation.eulerAngles;
        var newEuler = currentEuler + rotation;
        prefab.Rotation = Quaternion.Euler(newEuler);

        response = $"Rotated {FormatPrefab(prefab)} by ({FormatVector(rotation)}).";
        return true;
    }

    // ===== setrot =====
    private bool SetRot(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (!TryReadVector(args, valueIndex, out var rotation))
        {
            response = "Usage: .sl objprefab setrot [InstanceID] <pitch> <yaw> <roll>";
            return false;
        }

        prefab.Rotation = Quaternion.Euler(rotation);
        response = $"Set {FormatPrefab(prefab)} rotation to ({FormatVector(rotation)}).";
        return true;
    }

    private bool Scale(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (!TryReadVector(args, valueIndex, out var scale))
        {
            response = "Usage: .sl objprefab scale [InstanceID] <x> <y> <z>";
            return false;
        }

        prefab.Scale = scale;
        response = $"Set {FormatPrefab(prefab)} scale to ({FormatVector(scale)}).";
        return true;
    }

    // ===== grab =====
    private bool Grab(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        if (Grabbing.TryGetValue(player, out var current) &&
            current.ObjectInstanceID == prefab.ObjectInstanceID &&
            !GrabLockRotation.GetValueOrDefault(player))
        {
            UngrabInternal(player);
            response = $"Released {FormatPrefab(prefab)}.";
            return true;
        }

        StartGrab(player, prefab, false);
        Selected[player] = prefab;
        response = $"Now grabbing {FormatPrefab(prefab)}.";
        return true;
    }

    // ===== grabpos (位置だけ追従) =====
    private bool GrabPos(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        if (Grabbing.TryGetValue(player, out var current) &&
            current.ObjectInstanceID == prefab.ObjectInstanceID &&
            GrabLockRotation.GetValueOrDefault(player))
        {
            UngrabInternal(player);
            response = $"Released {FormatPrefab(prefab)}.";
            return true;
        }

        StartGrab(player, prefab, true);
        Selected[player] = prefab;
        response = $"Now grabbing position only for {FormatPrefab(prefab)}.";
        return true;
    }

    private bool Ungrab(ArraySegment<string> args, Player player, out string response)
    {
        if (!Grabbing.ContainsKey(player))
        {
            response = "You are not grabbing any prefab.";
            return false;
        }

        UngrabInternal(player);
        response = "Released grabbed prefab.";
        return true;
    }

    private static void UngrabInternal(Player player)
    {
        if (GrabCoroutines.TryGetValue(player, out var handle))
        {
            Timing.KillCoroutines(handle);
            GrabCoroutines.Remove(player);
        }

        Grabbing.Remove(player);
        GrabDistance.Remove(player);
        GrabLocalOffset.Remove(player);
        GrabRotationOffset.Remove(player);
        GrabLockRotation.Remove(player);
    }

    private static bool IsSameOrInvalidPlayer(Player key, Player player)
        => key == null || key.ReferenceHub == null || (player != null && key.Id == player.Id);

    private IEnumerator<float> GrabFollowCoroutine(Player player)
    {
        while (Grabbing.TryGetValue(player, out var prefab))
        {
            if (player.ReferenceHub == null || prefab == null)
                break;

            var localOffset = GrabLocalOffset.TryGetValue(player, out var offset)
                ? offset
                : Vector3.forward * (GrabDistance.TryGetValue(player, out var d) ? d : DefaultCreateDistance);
            var rotOffset = GrabRotationOffset.TryGetValue(player, out var ro) ? ro : Quaternion.identity;
            var lockRot = GrabLockRotation.TryGetValue(player, out var lr) && lr;

            prefab.Position = player.CameraTransform.position + player.CameraTransform.rotation * localOffset;

            if (!lockRot)
            {
                Quaternion playerYaw = Quaternion.Euler(0, player.CameraTransform.rotation.eulerAngles.y, 0);
                prefab.Rotation = playerYaw * rotOffset;
            }

            yield return Timing.WaitForSeconds(0.05f);
        }

        UngrabInternal(player);
    }

    // ===== offset =====
    private bool Offset(ArraySegment<string> args, Player player, out string response)
    {
        if (!Grabbing.ContainsKey(player))
        {
            response = "You are not grabbing any prefab.";
            return false;
        }

        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab offset <forwardDelta> OR .sl objprefab offset <rightDelta> <upDelta> <forwardDelta>";
            return false;
        }

        var currentOffset = GrabLocalOffset.TryGetValue(player, out var offset)
            ? offset
            : Vector3.forward * (GrabDistance.TryGetValue(player, out var dist) ? dist : DefaultCreateDistance);

        if (args.Count >= 4)
        {
            if (!TryParseFloat(args.At(1), out var right) ||
                !TryParseFloat(args.At(2), out var up) ||
                !TryParseFloat(args.At(3), out var forward))
            {
                response = "rightDelta, upDelta, forwardDelta must be numbers.";
                return false;
            }

            currentOffset += new Vector3(right, up, forward);
        }
        else if (TryParseFloat(args.At(1), out var delta))
        {
            currentOffset += Vector3.forward * delta;
        }
        else
        {
            response = "offset values must be numbers.";
            return false;
        }

        if (currentOffset.z < 0.5f)
            currentOffset.z = 0.5f;

        GrabLocalOffset[player] = currentOffset;
        GrabDistance[player] = currentOffset.magnitude;

        response = $"Grab offset updated. Right:{currentOffset.x:F2} Up:{currentOffset.y:F2} Forward:{currentOffset.z:F2}";
        return true;
    }

    // ===== grot =====
    private bool GrabRotate(ArraySegment<string> args, Player player, out string response)
    {
        if (!Grabbing.ContainsKey(player))
        {
            response = "You are not grabbing any prefab.";
            return false;
        }

        if (args.Count < 4)
        {
            response = "Usage: .sl objprefab grot <pitch> <yaw> <roll>";
            return false;
        }

        if (!TryParseFloat(args.At(1), out var pitch) ||
            !TryParseFloat(args.At(2), out var yaw) ||
            !TryParseFloat(args.At(3), out var roll))
        {
            response = "pitch, yaw, roll must be numbers.";
            return false;
        }

        GrabRotationOffset[player] = Quaternion.Euler(pitch, yaw, roll);
        GrabLockRotation[player] = false;
        response = $"Set grab rotation offset to ({pitch}, {yaw}, {roll}).";
        return true;
    }

    // ===== bring (位置+回転) =====
    private bool Bring(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        prefab.Position = player.CameraTransform.position + player.CameraTransform.forward * 2f;
        prefab.Rotation = Quaternion.Euler(0, player.CameraTransform.rotation.eulerAngles.y, 0);

        response = $"Brought {FormatPrefab(prefab)} to your front.";
        return true;
    }

    // ===== bringpos (位置だけ) =====
    private bool BringPos(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out _, out response))
            return false;

        prefab.Position = player.CameraTransform.position + player.CameraTransform.forward * 2f;
        response = $"Brought position only for {FormatPrefab(prefab)} to your front.";
        return true;
    }

    // ===== max (ID指定) =====
    private bool SetMaxRooms(ArraySegment<string> args, Player player, out string response)
    {
        if (!TryResolveTarget(args, player, 1, out var prefab, out var consumed, out response, true))
            return false;

        var valueIndex = 1 + consumed;
        if (args.Count <= valueIndex || !int.TryParse(args.At(valueIndex), out var count) || count < 0)
        {
            response = "Usage: .sl objprefab max [InstanceID] <count>";
            return false;
        }

        prefab.MaxRooms = count == 0 ? 1 : count;
        response = $"Set {FormatPrefab(prefab)} MaxRooms to {prefab.MaxRooms}.";
        return true;
    }

    // ===== sel =====
    private bool Select(ArraySegment<string> args, Player player, out string response)
    {
        if (args.Count == 1)
        {
            if (TryFindLookPrefab(player, out var looked, out var distance))
            {
                Selected[player] = looked;
                response = $"Selected {FormatPrefab(looked)} at {distance:F1}m.";
                return true;
            }

            if (Selected.TryGetValue(player, out var current))
            {
                response = $"Selected prefab: {FormatPrefab(current)} at {current.Position}. Look at another prefab and run select to switch.";
                return true;
            }

            response = "No prefab selected. Look at a prefab and run '.sl objprefab select', or pass an InstanceID.";
            return false;
        }

        var selector = args.At(1);
        if (selector.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            selector.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Selected.Remove(player);
            response = "Cleared selected prefab.";
            return true;
        }

        if (selector.Equals("look", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryFindLookPrefab(player, out var looked, out var distance))
            {
                response = "No ObjectPrefab found on your sight line.";
                return false;
            }

            Selected[player] = looked;
            response = $"Selected {FormatPrefab(looked)} at {distance:F1}m.";
            return true;
        }

        if (selector.Equals("near", StringComparison.OrdinalIgnoreCase))
        {
            var radius = 5f;
            if (args.Count >= 3 && !TryParseFloat(args.At(2), out radius))
            {
                response = "Usage: .sl objprefab select near [radius]";
                return false;
            }

            if (!TryFindNearestPrefab(player, radius, out var nearest))
            {
                response = $"No ObjectPrefab found within {radius:F1}m.";
                return false;
            }

            Selected[player] = nearest;
            response = $"Selected nearest {FormatPrefab(nearest)}.";
            return true;
        }

        var prefab = InstanceManager.Get(selector) as ObjectPrefab;
        if (prefab is null)
        {
            response = $"Prefab {selector} not found.";
            return false;
        }

        Selected[player] = prefab;
        response = $"Selected {FormatPrefab(prefab)}.";
        return true;
    }

    // ===== mod =====
    private bool Mod(ArraySegment<string> args, Player player, out string response)
    {
        if (!Selected.TryGetValue(player, out var prefab))
        {
            response = "No prefab selected. Use '.sl objprefab select' while looking at one first.";
            return false;
        }

        if (args.Count < 2)
        {
            response = "Usage: .sl objprefab modify <info|position|rotation|scale|max|autodestroy|bring>";
            return false;
        }

        string sub = args.At(1).ToLower();
        switch (sub)
        {
            case "info":
                response =
                    $"[{prefab.ObjectInstanceID}] {prefab.GetType().Name}\n" +
                    $" Pos: {prefab.Position}\n" +
                    $" Rot: {prefab.Rotation.eulerAngles}\n" +
                    $" Scale: {prefab.Scale}\n" +
                    $" MaxRooms: {prefab.MaxRooms}\n" +
                    $" ManagedSchematic: {(prefab.ManagedSchematic != null ? "yes" : "no")}\n" +
                    $" ManagedInteractables: {prefab.ManagedInteractables.Count}\n" +
                    $" AutoDestroy: {(prefab.AutoDestroyEnabled ? prefab.AutoDestroyTime.ToString() : "disabled")}";
                var options = prefab.CollectOptions();
                if (options.Count > 0)
                    response += "\n Options: " + string.Join(", ", options.Select(kv => $"{kv.Key}={kv.Value}"));
                return true;

            case "position":
            case "pos":
                return ModPosition(args, player, prefab, out response);

            case "addpos":
                return ModAddPos(args, prefab, 2, out response);

            case "rotation":
            case "rotate":
            case "rot":
                return ModRotation(args, prefab, out response);

            case "scale":
            case "scl":
                return ModScale(args, prefab, out response);

            case "setpos":
                return ModSetPos(args, prefab, out response);

            case "setrot":
                return ModSetRot(args, prefab, out response);

            case "max":
                return ModSetMaxRooms(args, prefab, out response);

            case "autodestroy":
                return ModSetAutoDestroy(args, prefab, out response);

            case "option":
            case "opt":
                return ModApplyOption(args, prefab, out response);

            case "bring":
                return ModBring(args, player, prefab, out response);

            default:
                // サブクラス固有のmodサブコマンドを試行
                if (prefab.HandleModCommand(args, out response))
                    return true;
                response = "Unknown subcommand. Use: info / position / rotation / scale / max / autodestroy / option / bring";
                return false;
        }
    }

    private bool ModPosition(ArraySegment<string> args, Player player, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 3)
        {
            response =
                "Usage:\n" +
                "  .sl objprefab modify position set <x> <y> <z>\n" +
                "  .sl objprefab modify position add <dx> <dy> <dz>\n" +
                "  .sl objprefab modify position bring\n" +
                "  .sl objprefab modify position grab";
            return false;
        }

        switch (args.At(2).ToLowerInvariant())
        {
            case "set":
                return ModSetPos(args, prefab, 3, out response);
            case "add":
                return ModAddPos(args, prefab, 3, out response);
            case "bring":
                return ModBring(args, player, prefab, out response);
            case "grab":
                StartGrab(player, prefab, true);
                response = $"Now grabbing position only for {FormatPrefab(prefab)}.";
                return true;
            default:
                if (TryReadVector(args, 2, out _))
                    return ModSetPos(args, prefab, 2, out response);

                response = "Unknown position action. Use set / add / bring / grab.";
                return false;
        }
    }

    private bool ModRotation(ArraySegment<string> args, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 3)
        {
            response =
                "Usage:\n" +
                "  .sl objprefab modify rotation set <pitch> <yaw> <roll>\n" +
                "  .sl objprefab modify rotation add <pitch> <yaw> <roll>";
            return false;
        }

        switch (args.At(2).ToLowerInvariant())
        {
            case "set":
                return ModSetRot(args, prefab, 3, out response);
            case "add":
                return ModAddRot(args, prefab, 3, out response);
            default:
                if (TryReadVector(args, 2, out _))
                    return ModSetRot(args, prefab, 2, out response);

                response = "Unknown rotation action. Use set / add.";
                return false;
        }
    }

    private bool ModScale(ArraySegment<string> args, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 3)
        {
            response =
                "Usage:\n" +
                "  .sl objprefab modify scale set <x> <y> <z>\n" +
                "  .sl objprefab modify scale add <x> <y> <z>";
            return false;
        }

        switch (args.At(2).ToLowerInvariant())
        {
            case "set":
                return ModSetScale(args, prefab, 3, out response);
            case "add":
                return ModAddScale(args, prefab, 3, out response);
            default:
                if (TryReadVector(args, 2, out _))
                    return ModSetScale(args, prefab, 2, out response);

                response = "Unknown scale action. Use set / add.";
                return false;
        }
    }

    private bool ModSetPos(ArraySegment<string> args, ObjectPrefab prefab, out string response)
        => ModSetPos(args, prefab, 2, out response);

    private bool ModSetPos(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var position))
        {
            response = "Usage: .sl objprefab modify position set <x> <y> <z>";
            return false;
        }

        prefab.Position = position;
        response = $"Set {FormatPrefab(prefab)} position to ({FormatVector(position)}).";
        return true;
    }

    private bool ModAddPos(ArraySegment<string> args, ObjectPrefab prefab, out string response)
        => ModAddPos(args, prefab, 2, out response);

    private bool ModAddPos(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var delta))
        {
            response = "Usage: .sl objprefab modify position add <dx> <dy> <dz>";
            return false;
        }

        prefab.Position += delta;
        response = $"Moved {FormatPrefab(prefab)} by ({FormatVector(delta)}). Now: {prefab.Position}";
        return true;
    }

    private bool ModSetRot(ArraySegment<string> args, ObjectPrefab prefab, out string response)
        => ModSetRot(args, prefab, 2, out response);

    private bool ModSetRot(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var rotation))
        {
            response = "Usage: .sl objprefab modify rotation set <pitch> <yaw> <roll>";
            return false;
        }

        prefab.Rotation = Quaternion.Euler(rotation);
        response = $"Set {FormatPrefab(prefab)} rotation to ({FormatVector(rotation)}).";
        return true;
    }

    private bool ModAddRot(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var rotation))
        {
            response = "Usage: .sl objprefab modify rotation add <pitch> <yaw> <roll>";
            return false;
        }

        prefab.Rotation = Quaternion.Euler(prefab.Rotation.eulerAngles + rotation);
        response = $"Rotated {FormatPrefab(prefab)} by ({FormatVector(rotation)}).";
        return true;
    }

    private bool ModSetScale(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var scale))
        {
            response = "Usage: .sl objprefab modify scale set <x> <y> <z>";
            return false;
        }

        prefab.Scale = scale;
        response = $"Set {FormatPrefab(prefab)} scale to ({FormatVector(scale)}).";
        return true;
    }

    private bool ModAddScale(ArraySegment<string> args, ObjectPrefab prefab, int index, out string response)
    {
        if (!TryReadVector(args, index, out var scale))
        {
            response = "Usage: .sl objprefab modify scale add <x> <y> <z>";
            return false;
        }

        prefab.Scale += scale;
        response = $"Added scale ({FormatVector(scale)}) to {FormatPrefab(prefab)}. Now: {prefab.Scale}";
        return true;
    }

    private bool ModSetMaxRooms(ArraySegment<string> args, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 3)
        {
            response = "Usage: .sl objprefab mod max <count>";
            return false;
        }

        if (!int.TryParse(args.At(2), out var count) || count < 0)
        {
            response = "count must be >= 0 integer.";
            return false;
        }

        prefab.MaxRooms = count == 0 ? 1 : count;
        response = $"Set MaxRooms to {prefab.MaxRooms}.";
        return true;
    }

    private bool ModSetAutoDestroy(ArraySegment<string> args, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 3)
        {
            response = "Usage: .sl objprefab mod autodestroy <seconds|-1>";
            return false;
        }

        if (!TryParseFloat(args.At(2), out var sec))
        {
            response = "seconds must be a number (or -1 to disable).";
            return false;
        }

        if (sec <= 0f)
        {
            prefab.AutoDestroyEnabled = false;
            prefab.AutoDestroyTime = -1f;
            response = "AutoDestroy disabled.";
            return true;
        }

        prefab.AutoDestroyEnabled = true;
        prefab.AutoDestroyTime = sec;
        response = $"AutoDestroy enabled: {sec} seconds.";
        return true;
    }

    private bool ModApplyOption(ArraySegment<string> args, ObjectPrefab prefab, out string response)
    {
        if (args.Count < 4)
        {
            var options = prefab.CollectOptions();
            response = options.Count > 0
                ? "Current options: " + string.Join(", ", options.Select(kv => $"{kv.Key}={kv.Value}")) +
                  "\nUsage: .sl objprefab modify option <key> <value>"
                : "Usage: .sl objprefab modify option <key> <value>";
            return false;
        }

        var key = args.At(2);
        var value = args.At(3);
        prefab.ApplyOptions(new Dictionary<string, string> { [key] = value });
        prefab.SyncManagedObjects();

        response = $"Applied option {key}={value} to {FormatPrefab(prefab)}.";
        return true;
    }

    private bool ModBring(ArraySegment<string> args, Player player, ObjectPrefab prefab, out string response)
    {
        prefab.Position = player.CameraTransform.position + player.CameraTransform.forward * 2f;
        response = "Brought selected prefab to your front (position only).";
        return true;
    }
}

public static class ObjectPrefabLoader
{
    /// <summary>
    /// 最後にロードしたマップ名。saveall で参照される。
    /// </summary>
    public static string? LastLoadedMapName { get; private set; }

    /// <summary>
    /// 指定マップ名のObjectPrefabマップファイルを読み込み、
    /// RoomType + Local座標 + MaxRooms に従ってPrefabをスポーンします。
    /// 既存のObjectPrefabは全クリアされます。
    /// </summary>
    public static int LoadMap(string mapName)
    {
        LastLoadedMapName = mapName;
        var cfg = ObjectPrefabConfig.Load(mapName);
        InstanceManager.ClearAll();
        int totalSpawned = 0;

        foreach (var data in cfg.Prefabs)
        {
            var type = Type.GetType(data.PrefabType) ??
                       Assembly.GetExecutingAssembly().GetTypes()
                           .FirstOrDefault(t => t.FullName == data.PrefabType || t.Name == data.PrefabType);

            if (type == null || !type.IsSubclassOf(typeof(ObjectPrefab)))
            {
                Log.Warn($"[ObjectPrefabLoader] Type '{data.PrefabType}' not found or not ObjectPrefab.");
                continue;
            }

            var roomsOfType = Room.List
                .Where(r => r.Type == data.RoomType)
                .ToList();

            if (!roomsOfType.Any())
            {
                Log.Warn($"[ObjectPrefabLoader] No rooms of type {data.RoomType} found for prefab '{data.PrefabType}'.");
                continue;
            }

            int maxRoomsFromData = data.MaxRooms;
            if (maxRoomsFromData <= 0)
                maxRoomsFromData = roomsOfType.Count;

            int maxRooms = Mathf.Min(maxRoomsFromData, roomsOfType.Count);

            roomsOfType = roomsOfType.OrderBy(_ => UnityEngine.Random.value).ToList();

            for (int i = 0; i < maxRooms; i++)
            {
                var room = roomsOfType[i];

                Quaternion roomRot = room.Rotation;
                Vector3 worldPos = room.Position + roomRot * data.LocalPosition;
                Quaternion worldRot = roomRot * Quaternion.Euler(data.LocalRotationEuler);

                var prefab = (ObjectPrefab)Activator.CreateInstance(type)!;
                prefab.Position = worldPos;
                prefab.Rotation = worldRot;
                prefab.Scale = data.Scale;
                prefab.AutoDestroyEnabled = data.AutoDestroyEnabled;
                prefab.AutoDestroyTime = data.AutoDestroyTime;
                prefab.MaxRooms = data.MaxRooms <= 0 ? 1 : data.MaxRooms;

                if (data.Options != null && data.Options.Count > 0)
                    prefab.ApplyOptions(data.Options);

                prefab.Create();
                totalSpawned++;
            }
        }

        Log.Info($"[ObjectPrefabLoader] Loaded map '{mapName}' ({totalSpawned} prefabs spawned).");
        return totalSpawned;
    }
}
public class ObjectPrefabConfig
{
    public List<PrefabSaveData> Prefabs { get; set; } = [];

    public static string DirectoryPath =>
        Path.Combine(Paths.Configs, "Slafight_Plugin_Exiled", "Maps");

    public static string GetFilePath(string mapName)
        => Path.Combine(DirectoryPath, $"{mapName}.json");

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.None,
    };

    public static ObjectPrefabConfig Load(string mapName)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = GetFilePath(mapName);

            if (!File.Exists(path))
                return new ObjectPrefabConfig();

            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ObjectPrefabConfig>(json, JsonSettings)
                   ?? new ObjectPrefabConfig();
        }
        catch (Exception e)
        {
            Log.Error($"[ObjectPrefabConfig] Load({mapName}) failed: {e}");
            return new ObjectPrefabConfig();
        }
    }

    public void Save(string mapName)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = GetFilePath(mapName);

            var json = JsonConvert.SerializeObject(this, JsonSettings);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            Log.Error($"[ObjectPrefabConfig] Save({mapName}) failed: {e}");
        }
    }
}
