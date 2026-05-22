using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class HitboxCommand : ICommand
{
    private const float DefaultDuration = 300f;
    private const float RedrawInterval = 1f;
    private const float DrawDuration = 1.25f;
    private const float DefaultLookRange = 80f;
    private const float DefaultNearRadius = 8f;
    private const int MaxCollidersPerSession = 256;

    private static readonly Dictionary<int, HitboxDrawSession> Sessions = new();
    private static int _nextSessionId = 1;

    public string Command => "hitbox";
    public string[] Aliases { get; } = ["hb", "collider", "colliders"];
    public string Description => "Draw/clear collider hitboxes: look/near/name/player/clear/list.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission($"slperm.{Command}"))
        {
            response = $"Permission denied. Required: slperm.{Command}";
            return false;
        }

        if (arguments.Count == 0 || arguments.At(0).Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            response =
                "Usage: sl hitbox <action> [...]\n" +
                "  look [duration|on] [range] [self|all]\n" +
                "  near [radius] [duration|on] [self|all] [nameFilter]\n" +
                "  name <nameFilter> [duration|on] [self|all] [max]\n" +
                "  player [target|all] [duration|on] [self|all]\n" +
                "  list\n" +
                "  clear [all|sessionId]\n" +
                "Default visibility is self. Use duration `on` to draw until cleared.";
            return false;
        }

        string action = arguments.At(0).ToLowerInvariant();
        if (action == "clear" || action == "stop" || action == "off")
            return Clear(arguments, sender, out response);

        if (action == "list" || action == "status")
            return ListSessions(sender, out response);

        if (!CommandTools.TryGetExecutor(sender, out var executor, out response))
            return false;

        return action switch
        {
            "look" or "ray" or "target" => DrawLook(arguments, executor, out response),
            "near" or "around" or "radius" => DrawNear(arguments, executor, out response),
            "name" or "find" or "search" => DrawName(arguments, executor, out response),
            "player" or "players" or "pl" => DrawPlayer(arguments, executor, out response),
            _ => UnknownAction(action, out response),
        };
    }

    private static bool DrawLook(ArraySegment<string> arguments, Player executor, out string response)
    {
        float duration = ParseDuration(arguments, 1, DefaultDuration);
        float range = ParseFloat(arguments, 2, DefaultLookRange, 1f, 500f);
        bool visibleToAll = ParseVisibility(arguments, 3);

        if (executor.CameraTransform == null ||
            !Physics.Raycast(executor.CameraTransform.position, executor.CameraTransform.forward, out RaycastHit hit, range,
                ~0, QueryTriggerInteraction.Collide))
        {
            response = $"No collider found in front of {executor.Nickname} within {range:0.#}m.";
            return false;
        }

        Collider[] colliders = GetObjectColliders(hit.collider).ToArray();
        if (colliders.Length == 0)
        {
            response = $"Hit object has no enabled colliders: {GetColliderName(hit.collider)}";
            return false;
        }

        int id = StartSession(
            executor,
            $"look:{GetColliderName(hit.collider)}",
            () => colliders,
            duration,
            visibleToAll,
            Color.cyan);

        response = StartedMessage(id, "look", colliders.Length, duration, visibleToAll);
        return true;
    }

    private static bool DrawNear(ArraySegment<string> arguments, Player executor, out string response)
    {
        float radius = ParseFloat(arguments, 1, DefaultNearRadius, 0.25f, 100f);
        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        string filter = arguments.Count >= 5 ? string.Join(" ", arguments.Skip(4)) : string.Empty;

        IEnumerable<Collider> Resolve() => GetNearbyColliders(executor.Position, radius, filter);
        Collider[] initial = Resolve().ToArray();
        if (initial.Length == 0)
        {
            response = string.IsNullOrWhiteSpace(filter)
                ? $"No colliders found within {radius:0.#}m."
                : $"No colliders found within {radius:0.#}m matching `{filter}`.";
            return false;
        }

        int id = StartSession(
            executor,
            string.IsNullOrWhiteSpace(filter) ? $"near:{radius:0.#}m" : $"near:{radius:0.#}m:{filter}",
            Resolve,
            duration,
            visibleToAll,
            Color.yellow);

        response = StartedMessage(id, "near", initial.Length, duration, visibleToAll);
        return true;
    }

    private static bool DrawName(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (arguments.Count < 2)
        {
            response = "Usage: sl hitbox name <nameFilter> [duration|on] [self|all] [max]";
            return false;
        }

        string filter = arguments.At(1);
        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        int max = ParseInt(arguments, 4, MaxCollidersPerSession, 1, 1000);

        IEnumerable<Collider> Resolve() => GetNamedColliders(filter, max);
        Collider[] initial = Resolve().ToArray();
        if (initial.Length == 0)
        {
            response = $"No enabled colliders matching `{filter}`.";
            return false;
        }

        int id = StartSession(
            executor,
            $"name:{filter}",
            Resolve,
            duration,
            visibleToAll,
            Color.magenta);

        response = StartedMessage(id, "name", initial.Length, duration, visibleToAll);
        return true;
    }

    private static bool DrawPlayer(ArraySegment<string> arguments, Player executor, out string response)
    {
        string targetArg = arguments.Count >= 2 ? arguments.At(1) : "@me";
        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);

        if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            IEnumerable<Collider> ResolveAll() => Player.List
                .Where(p => p.GameObject != null)
                .SelectMany(p => GetPlayerColliders(p))
                .Take(MaxCollidersPerSession);

            Collider[] initialAll = ResolveAll().ToArray();
            if (initialAll.Length == 0)
            {
                response = "No player colliders found.";
                return false;
            }

            int allId = StartSession(executor, "players:all", ResolveAll, duration, visibleToAll, Color.green);
            response = StartedMessage(allId, "player all", initialAll.Length, duration, visibleToAll);
            return true;
        }

        if (!CommandTools.TryResolvePlayer(targetArg, executor, out var target, out response))
            return false;

        IEnumerable<Collider> ResolveTarget() => GetPlayerColliders(target);
        Collider[] initial = ResolveTarget().ToArray();
        if (initial.Length == 0)
        {
            response = $"No player colliders found for {target.Nickname}.";
            return false;
        }

        int id = StartSession(
            executor,
            $"player:{target.Nickname}",
            ResolveTarget,
            duration,
            visibleToAll,
            Color.green);

        response = StartedMessage(id, "player", initial.Length, duration, visibleToAll);
        return true;
    }

    private static bool Clear(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        string mode = arguments.Count >= 2 ? arguments.At(1) : string.Empty;

        if (mode.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            int count = Sessions.Count;
            foreach (int id in Sessions.Keys.ToArray())
                StopSession(id);

            response = $"Cleared {count} hitbox draw session(s). Existing lines will disappear within {DrawDuration:0.##}s.";
            return true;
        }

        if (int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sessionId))
        {
            bool stopped = StopSession(sessionId);
            response = stopped
                ? $"Cleared hitbox draw session #{sessionId}. Existing lines will disappear within {DrawDuration:0.##}s."
                : $"Hitbox draw session not found: #{sessionId}";
            return stopped;
        }

        Player executor = Player.Get(sender);
        if (executor == null)
        {
            response = "Usage from server console: sl hitbox clear all|<sessionId>";
            return false;
        }

        int removed = 0;
        foreach (int id in Sessions
                     .Where(pair => pair.Value.OwnerUserId == executor.UserId)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            if (StopSession(id))
                removed++;
        }

        response = $"Cleared {removed} hitbox draw session(s) owned by {executor.Nickname}. Existing lines will disappear within {DrawDuration:0.##}s.";
        return true;
    }

    private static bool ListSessions(ICommandSender sender, out string response)
    {
        if (Sessions.Count == 0)
        {
            response = "No active hitbox draw sessions.";
            return true;
        }

        Player executor = Player.Get(sender);
        bool canSeeAll = executor == null;
        IEnumerable<HitboxDrawSession> sessions = Sessions.Values;
        if (!canSeeAll)
            sessions = sessions.Where(s => s.OwnerUserId == executor.UserId || s.VisibleToAll);

        response = "Active hitbox draw sessions:\n" + string.Join("\n", sessions
            .OrderBy(s => s.Id)
            .Select(s =>
                $"  #{s.Id} {s.Label} owner={s.OwnerName} visible={(s.VisibleToAll ? "all" : "self")} last={s.LastDrawCount} remaining={FormatRemaining(s)}"));
        return true;
    }

    private static bool UnknownAction(string action, out string response)
    {
        response = $"Unknown hitbox action: {action}. Use `sl hitbox help`.";
        return false;
    }

    private static int StartSession(
        Player owner,
        string label,
        Func<IEnumerable<Collider>> resolveColliders,
        float duration,
        bool visibleToAll,
        Color color)
    {
        int id = _nextSessionId++;
        var session = new HitboxDrawSession(
            id,
            owner.UserId,
            owner.Nickname,
            label,
            resolveColliders,
            duration <= 0f ? float.PositiveInfinity : Time.time + duration,
            visibleToAll,
            visibleToAll ? null : owner,
            color);

        Sessions[id] = session;
        session.Handle = Timing.RunCoroutine(DrawLoop(session));
        return id;
    }

    private static IEnumerator<float> DrawLoop(HitboxDrawSession session)
    {
        while (Sessions.ContainsKey(session.Id) && !session.IsExpired)
        {
            if (!session.VisibleToAll && !IsPlayerStillAvailable(session.SelfViewer))
                break;

            int count = 0;
            IEnumerable<Player> viewers = session.VisibleToAll ? null : new[] { session.SelfViewer };

            foreach (Collider collider in session.ResolveColliders()
                         .Where(IsDrawableCollider)
                         .Take(MaxCollidersPerSession)
                         .ToArray())
            {
                try
                {
                    Draw.Collider(collider, session.Color, DrawDuration, viewers);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Debug($"Hitbox draw failed for {GetColliderName(collider)}: {ex.Message}");
                }
            }

            session.LastDrawCount = count;
            yield return Timing.WaitForSeconds(RedrawInterval);
        }

        StopSession(session.Id);
    }

    private static bool StopSession(int id)
    {
        if (!Sessions.TryGetValue(id, out var session))
            return false;

        Timing.KillCoroutines(session.Handle);
        Sessions.Remove(id);
        return true;
    }

    private static IEnumerable<Collider> GetObjectColliders(Collider hitCollider)
    {
        if (hitCollider == null)
            return Enumerable.Empty<Collider>();

        Transform root = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.transform
            : hitCollider.transform;

        return root.GetComponentsInChildren<Collider>(false)
            .Where(IsDrawableCollider)
            .Distinct()
            .Take(MaxCollidersPerSession);
    }

    private static IEnumerable<Collider> GetNearbyColliders(Vector3 origin, float radius, string filter)
    {
        return Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Collide)
            .Where(c => string.IsNullOrWhiteSpace(filter) || ColliderNameMatches(c, filter))
            .Where(IsDrawableCollider)
            .Distinct()
            .OrderBy(c => Vector3.SqrMagnitude(c.bounds.center - origin))
            .Take(MaxCollidersPerSession);
    }

    private static IEnumerable<Collider> GetNamedColliders(string filter, int max)
    {
        return UnityObject.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c => ColliderNameMatches(c, filter))
            .Where(IsDrawableCollider)
            .Distinct()
            .Take(max);
    }

    private static IEnumerable<Collider> GetPlayerColliders(Player player)
    {
        if (player?.GameObject == null)
            return Enumerable.Empty<Collider>();

        return player.GameObject.GetComponentsInChildren<Collider>(false)
            .Where(IsDrawableCollider)
            .Distinct();
    }

    private static bool ColliderNameMatches(Collider collider, string filter)
    {
        if (collider == null || string.IsNullOrWhiteSpace(filter))
            return false;

        return GetColliderName(collider).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               collider.gameObject.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               collider.transform.root.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDrawableCollider(Collider collider)
        => collider != null &&
           collider.enabled &&
           collider.gameObject != null &&
           collider.gameObject.activeInHierarchy;

    private static bool IsPlayerStillAvailable(Player player)
        => player != null && Player.List.Contains(player);

    private static string GetColliderName(Collider collider)
    {
        if (collider == null)
            return "null";

        string root = collider.transform.root != null ? collider.transform.root.name : "no-root";
        return $"{root}/{collider.gameObject.name}/{collider.GetType().Name}";
    }

    private static float ParseDuration(ArraySegment<string> arguments, int index, float fallback)
    {
        if (arguments.Count <= index)
            return fallback;

        string value = arguments.At(index);
        if (value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("forever", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("inf", StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        return ParseFloat(arguments, index, fallback, 1f, 3600f);
    }

    private static float ParseFloat(ArraySegment<string> arguments, int index, float fallback, float min, float max)
    {
        if (arguments.Count <= index ||
            !float.TryParse(arguments.At(index), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return fallback;
        }

        return Mathf.Clamp(value, min, max);
    }

    private static int ParseInt(ArraySegment<string> arguments, int index, int fallback, int min, int max)
    {
        if (arguments.Count <= index ||
            !int.TryParse(arguments.At(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return fallback;
        }

        return Mathf.Clamp(value, min, max);
    }

    private static bool ParseVisibility(ArraySegment<string> arguments, int index)
    {
        if (arguments.Count <= index)
            return false;

        string value = arguments.At(index);
        return value.Equals("all", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("public", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("everyone", StringComparison.OrdinalIgnoreCase);
    }

    private static string StartedMessage(int id, string mode, int colliders, float duration, bool visibleToAll)
        => $"Started hitbox draw #{id} ({mode}) colliders={colliders} visible={(visibleToAll ? "all" : "self")} duration={(duration <= 0f ? "until clear" : $"{duration:0.#}s")}. Use `sl hitbox clear {id}` or `sl hitbox clear`.";

    private static string FormatRemaining(HitboxDrawSession session)
        => float.IsPositiveInfinity(session.EndTime)
            ? "until clear"
            : $"{Mathf.Max(0f, session.EndTime - Time.time):0.#}s";

    private sealed class HitboxDrawSession
    {
        public HitboxDrawSession(
            int id,
            string ownerUserId,
            string ownerName,
            string label,
            Func<IEnumerable<Collider>> resolveColliders,
            float endTime,
            bool visibleToAll,
            Player selfViewer,
            Color color)
        {
            Id = id;
            OwnerUserId = ownerUserId;
            OwnerName = ownerName;
            Label = label;
            ResolveColliders = resolveColliders;
            EndTime = endTime;
            VisibleToAll = visibleToAll;
            SelfViewer = selfViewer;
            Color = color;
        }

        public int Id { get; }
        public string OwnerUserId { get; }
        public string OwnerName { get; }
        public string Label { get; }
        public Func<IEnumerable<Collider>> ResolveColliders { get; }
        public float EndTime { get; }
        public bool VisibleToAll { get; }
        public Player SelfViewer { get; }
        public Color Color { get; }
        public CoroutineHandle Handle { get; set; }
        public int LastDrawCount { get; set; }
        public bool IsExpired => !float.IsPositiveInfinity(EndTime) && Time.time >= EndTime;
    }
}
