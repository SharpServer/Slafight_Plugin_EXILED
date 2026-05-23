using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Permissions.Extensions;
using MEC;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class HitboxCommand : ICommand
{
    private const float DefaultDuration = 300f;
    private const float PrimitiveRedrawInterval = 0.1f;
    private const float DefaultLookRange = 80f;
    private const float DefaultNearRadius = 8f;
    private const int DefaultMaxCollidersPerSession = 64;
    private const int DefaultAreaMaxCollidersPerSession = 128;
    private const int HardMaxCollidersPerSession = 256;
    private const float PrimitiveLineWidth = 0.035f;
    private const float PrimitiveVisibilityRefreshInterval = 0.5f;

    private static readonly Dictionary<int, HitboxDrawSession> Sessions = new();
    private static int _nextSessionId = 1;

    public string Command => "hitbox";
    public string[] Aliases { get; } = ["hb", "collider", "colliders"];
    public string Description => "Draw/clear collider hitboxes with Primitive toys: look/near/area/name/player/probe/clear/list.";

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
                "  look [duration|on|off] [range] [self|all]\n" +
                "  near [radius] [duration|on|off] [self|all] [nameFilter]\n" +
                "  area [radius] [duration|on|off] [self|all] [max]\n" +
                "  name <nameFilter> [duration|on|off] [self|all] [max]\n" +
                "  player [target|all] [duration|on|off] [self|all]\n" +
                "  probe [range]\n" +
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
            "probe" or "inspect" or "lookinfo" => ProbeLook(arguments, executor, out response),
            "near" or "around" or "radius" => DrawNear(arguments, executor, out response),
            "area" or "all" or "allnear" or "range" => DrawArea(arguments, executor, out response),
            "name" or "find" or "search" => DrawName(arguments, executor, out response),
            "player" or "players" or "pl" => DrawPlayer(arguments, executor, out response),
            _ => UnknownAction(action, out response),
        };
    }

    private static bool ProbeLook(ArraySegment<string> arguments, Player executor, out string response)
    {
        float range = ParseFloat(arguments, 1, DefaultLookRange, 1f, 500f);
        if (!TryRaycastLook(executor, range, out RaycastHit hit, out response))
            return false;

        Collider collider = hit.collider;
        Bounds bounds = collider.bounds;
        response =
            "Hitbox probe\n" +
            $"  Name: {GetColliderName(collider)}\n" +
            $"  Type: {collider.GetType().FullName}\n" +
            $"  Layer: {collider.gameObject.layer}, Trigger={collider.isTrigger}, Enabled={collider.enabled}\n" +
            $"  Distance: {hit.distance:0.###}m\n" +
            $"  Bounds center: {FormatVector(bounds.center)}\n" +
            $"  Bounds size: {FormatVector(bounds.size)}\n" +
            $"  Finite bounds: {IsFinite(bounds.center) && IsFinite(bounds.size)}";
        return true;
    }

    private static bool DrawLook(ArraySegment<string> arguments, Player executor, out string response)
    {
        if (IsOffArgument(arguments, 1))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 1, DefaultDuration);
        float range = ParseFloat(arguments, 2, DefaultLookRange, 1f, 500f);
        bool visibleToAll = ParseVisibility(arguments, 3);

        if (!TryRaycastLook(executor, range, out RaycastHit hit, out response))
            return false;

        Collider[] colliders = GetHitCollider(hit.collider).ToArray();
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

        if (IsOffArgument(arguments, 1) || IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        string filter = arguments.Count >= 5 ? string.Join(" ", arguments.Skip(4)) : string.Empty;

        IEnumerable<Collider> Resolve() => GetNearbyColliders(executor, executor.Position, radius, filter, DefaultMaxCollidersPerSession);
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

    private static bool DrawArea(ArraySegment<string> arguments, Player executor, out string response)
    {
        float radius = ParseFloat(arguments, 1, DefaultNearRadius, 0.25f, 100f);

        if (IsOffArgument(arguments, 1) || IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        int max = ParseInt(arguments, 4, DefaultAreaMaxCollidersPerSession, 1, HardMaxCollidersPerSession);

        IEnumerable<Collider> Resolve() => GetNearbyColliders(executor, executor.Position, radius, string.Empty, max);
        Collider[] initial = Resolve().ToArray();
        if (initial.Length == 0)
        {
            response = $"No colliders found within {radius:0.#}m.";
            return false;
        }

        int id = StartSession(
            executor,
            $"area:{radius:0.#}m:max{max}",
            Resolve,
            duration,
            visibleToAll,
            Color.yellow,
            max);

        response = StartedMessage(id, "area", initial.Length, duration, visibleToAll);
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

        if (IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        int max = ParseInt(arguments, 4, DefaultMaxCollidersPerSession, 1, HardMaxCollidersPerSession);

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

        if (IsOffArgument(arguments, 1) || IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);

        if (targetArg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            IEnumerable<Collider> ResolveAll() => Player.List
                .Where(p => p.GameObject != null)
                .SelectMany(p => GetPlayerColliders(p))
                .Take(DefaultMaxCollidersPerSession);

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

            response = $"Cleared {count} hitbox draw session(s).";
            return true;
        }

        if (int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sessionId))
        {
            bool stopped = StopSession(sessionId);
            response = stopped
                ? $"Cleared hitbox draw session #{sessionId}."
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

        response = $"Cleared {removed} hitbox draw session(s) owned by {executor.Nickname}.";
        return true;
    }

    private static bool ClearOwnerSessions(Player executor, out string response)
    {
        int removed = ClearOwnerSessions(executor, null);

        response = $"Cleared {removed} hitbox draw session(s) owned by {executor.Nickname}.";
        return true;
    }

    private static int ClearOwnerSessions(Player executor, string labelPrefix)
    {
        int removed = 0;
        foreach (int id in Sessions
                     .Where(pair => pair.Value.OwnerUserId == executor.UserId &&
                                    (labelPrefix == null || pair.Value.Label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase)))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            if (StopSession(id))
                removed++;
        }

        return removed;
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
        Color color,
        int maxColliders = DefaultMaxCollidersPerSession)
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
            color,
            maxColliders);

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
            Collider[] colliders = session.ResolveColliders()
                .Where(IsDrawableCollider)
                .Take(session.MaxColliders)
                .ToArray();

            count = RenderPrimitiveHitboxes(session, colliders);
            RefreshPrimitiveVisibilityIfNeeded(session);

            session.LastDrawCount = count;
            yield return Timing.WaitForSeconds(PrimitiveRedrawInterval);
        }

        StopSession(session.Id);
    }

    private static bool StopSession(int id)
    {
        if (!Sessions.TryGetValue(id, out var session))
            return false;

        Timing.KillCoroutines(session.Handle);
        ClearPrimitiveLines(session);
        Sessions.Remove(id);
        return true;
    }

    private static bool TryRaycastLook(Player executor, float range, out RaycastHit hit, out string response)
    {
        hit = default;
        if (executor.CameraTransform == null)
        {
            response = "Camera transform not found.";
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
                executor.CameraTransform.position,
                executor.CameraTransform.forward,
                range,
                ~0,
                QueryTriggerInteraction.Collide)
            .OrderBy(h => h.distance)
            .ToArray();

        foreach (RaycastHit candidate in hits)
        {
            if (ShouldIgnoreLookCollider(executor, candidate.collider))
                continue;

            hit = candidate;
            response = string.Empty;
            return true;
        }

        if (hits.Length == 0)
        {
            response = $"No collider found in front of {executor.Nickname} within {range:0.#}m.";
            return false;
        }

        response = $"Only ignored colliders were found in front of {executor.Nickname} within {range:0.#}m.";
        return false;
    }

    private static bool ShouldIgnoreLookCollider(Player executor, Collider collider)
    {
        if (collider == null)
            return true;

        if (IsSessionPrimitiveCollider(collider))
            return true;

        if (executor?.GameObject != null &&
            (collider.gameObject == executor.GameObject ||
             collider.transform.IsChildOf(executor.GameObject.transform) ||
             executor.GameObject.transform.IsChildOf(collider.transform)))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldIgnoreAreaCollider(Player executor, Collider collider)
    {
        if (collider == null)
            return true;

        if (IsSessionPrimitiveCollider(collider))
            return true;

        return IsPlayerCollider(executor, collider);
    }

    private static bool IsPlayerCollider(Player player, Collider collider)
    {
        if (player?.GameObject == null || collider == null)
            return false;

        Transform playerTransform = player.GameObject.transform;
        return collider.gameObject == player.GameObject ||
               collider.transform.IsChildOf(playerTransform) ||
               playerTransform.IsChildOf(collider.transform);
    }

    private static IEnumerable<Collider> GetHitCollider(Collider hitCollider)
    {
        if (!IsDrawableCollider(hitCollider))
            return [];

        return [hitCollider];
    }

    private static int RenderPrimitiveHitboxes(HitboxDrawSession session, Collider[] colliders)
    {
        var segments = new List<(Vector3 start, Vector3 end)>();
        var renderedPlayers = new HashSet<int>();

        foreach (Collider collider in colliders)
        {
            if (TryGetColliderPlayer(collider, out Player player))
            {
                if (renderedPlayers.Add(player.Id))
                    AddPlayerCollisionSegments(player, segments);

                continue;
            }

            AddBoundsSegments(collider.bounds, segments);
        }

        segments = segments
            .Where(s => IsValidSegment(s.start, s.end))
            .Take(session.MaxColliders * 12)
            .ToList();

        EnsurePrimitiveLineCount(session, segments.Count);

        for (int i = 0; i < segments.Count; i++)
            UpdatePrimitiveLine(session.PrimitiveLines[i], segments[i].start, segments[i].end, session.Color);

        for (int i = segments.Count; i < session.PrimitiveLines.Count; i++)
        {
            Primitive primitive = session.PrimitiveLines[i];
            primitive.Scale = Vector3.zero;
        }

        return colliders.Length;
    }

    private static bool TryGetColliderPlayer(Collider collider, out Player player)
    {
        player = null;
        if (collider == null)
            return false;

        player = Player.Get(collider.gameObject);
        if (player != null)
            return true;

        foreach (Player candidate in Player.List)
        {
            if (candidate?.GameObject == null)
                continue;

            Transform playerTransform = candidate.GameObject.transform;
            if (collider.transform.IsChildOf(playerTransform) || playerTransform.IsChildOf(collider.transform))
            {
                player = candidate;
                return true;
            }
        }

        return false;
    }

    private static void RefreshPrimitiveVisibilityIfNeeded(HitboxDrawSession session)
    {
        if (session.VisibleToAll || session.SelfViewer == null)
            return;

        if (Time.time < session.NextVisibilityRefreshTime)
            return;

        session.NextVisibilityRefreshTime = Time.time + PrimitiveVisibilityRefreshInterval;
        foreach (Primitive primitive in session.PrimitiveLines)
        {
            if (primitive?.Base?.netIdentity == null)
                continue;

            primitive.ApplyShowState();
        }
    }

    private static void AddPlayerCollisionSegments(Player player, List<(Vector3 start, Vector3 end)> segments)
    {
        if (TryAddCharacterControllerSegments(player, segments))
            return;

        AddFallbackPlayerCapsuleSegments(player, segments);
    }

    private static bool TryAddCharacterControllerSegments(Player player, List<(Vector3 start, Vector3 end)> segments)
    {
        if (player?.ReferenceHub?.roleManager?.CurrentRole is not IFpcRole fpcRole)
            return false;

        CharacterController controller = fpcRole.FpcModule?.CharController;
        if (controller == null || !controller.enabled)
            return false;

        Transform transform = controller.transform;
        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = controller.height * Mathf.Abs(scale.y);

        if (radius <= 0.001f || height <= 0.001f)
            return false;

        height = Mathf.Max(height, radius * 2f);
        Vector3 center = transform.TransformPoint(controller.center);
        AddCapsuleSegments(
            center,
            transform.up,
            transform.right,
            transform.forward,
            height,
            radius,
            segments);

        return true;
    }

    private static void AddFallbackPlayerCapsuleSegments(Player player, List<(Vector3 start, Vector3 end)> segments)
    {
        Vector3 center = player.Position + Vector3.up * 0.4f;
        AddCapsuleSegments(center, Vector3.up, Vector3.right, Vector3.forward, 1.8f, 0.35f, segments);
    }

    private static void AddCapsuleSegments(
        Vector3 center,
        Vector3 up,
        Vector3 right,
        Vector3 forward,
        float height,
        float radius,
        List<(Vector3 start, Vector3 end)> segments)
    {
        int sides = 8;
        float cylinderHalfHeight = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 bottom = center - up * cylinderHalfHeight;
        Vector3 top = center + up * cylinderHalfHeight;

        var bottomRing = new Vector3[sides];
        var topRing = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            Vector3 offset = (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            bottomRing[i] = bottom + offset;
            topRing[i] = top + offset;
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            segments.Add((bottomRing[i], bottomRing[next]));
            segments.Add((topRing[i], topRing[next]));
        }

        for (int i = 0; i < sides; i += 2)
            segments.Add((bottomRing[i], topRing[i]));

        segments.Add((top - right * radius, top + right * radius));
        segments.Add((top + forward * radius, top - forward * radius));
        segments.Add((bottom - right * radius, bottom + right * radius));
        segments.Add((bottom + forward * radius, bottom - forward * radius));
    }

    private static void EnsurePrimitiveLineCount(HitboxDrawSession session, int count)
    {
        while (session.PrimitiveLines.Count < count)
        {
            Primitive primitive = Primitive.Create(
                PrimitiveType.Cube,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                true,
                session.Color);

            primitive.Collidable = false;
            if (!session.VisibleToAll && session.SelfViewer != null)
            {
                primitive.InitShowState(new NetworkShowState
                {
                    OwnerId = session.SelfViewer.Id,
                    ShowToOwner = true,
                    SpectatorVisibility = SpectatorVisibility.Show,
                });
            }

            session.PrimitiveLines.Add(primitive);
        }
    }

    private static void UpdatePrimitiveLine(Primitive primitive, Vector3 start, Vector3 end, Color color)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.001f || !IsFinite(delta))
        {
            primitive.Scale = Vector3.zero;
            return;
        }

        primitive.Position = Vector3.Lerp(start, end, 0.5f);
        primitive.Rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        primitive.Scale = new Vector3(PrimitiveLineWidth, PrimitiveLineWidth, length);
        primitive.Color = color;
        primitive.Collidable = false;
    }

    private static void ClearPrimitiveLines(HitboxDrawSession session)
    {
        foreach (Primitive primitive in session.PrimitiveLines)
        {
            try
            {
                primitive.RemoveShowState();
                primitive.Destroy();
            }
            catch (Exception ex)
            {
                Log.Debug($"Hitbox primitive destroy failed: {ex.Message}");
            }
        }

        session.PrimitiveLines.Clear();
    }

    private static bool IsSessionPrimitiveCollider(Collider collider)
    {
        GameObject gameObject = collider?.gameObject;
        if (gameObject == null)
            return true;

        foreach (HitboxDrawSession session in Sessions.Values)
        {
            foreach (Primitive primitive in session.PrimitiveLines)
            {
                if (primitive?.Base == null)
                    continue;

                Transform primitiveTransform = primitive.Base.gameObject.transform;
                if (gameObject == primitive.Base.gameObject || collider.transform.IsChildOf(primitiveTransform))
                    return true;
            }
        }

        return false;
    }

    private static void AddBoundsSegments(Bounds bounds, List<(Vector3 start, Vector3 end)> segments)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        Vector3 p000 = c + new Vector3(-e.x, -e.y, -e.z);
        Vector3 p100 = c + new Vector3(e.x, -e.y, -e.z);
        Vector3 p110 = c + new Vector3(e.x, -e.y, e.z);
        Vector3 p010 = c + new Vector3(-e.x, -e.y, e.z);
        Vector3 p001 = c + new Vector3(-e.x, e.y, -e.z);
        Vector3 p101 = c + new Vector3(e.x, e.y, -e.z);
        Vector3 p111 = c + new Vector3(e.x, e.y, e.z);
        Vector3 p011 = c + new Vector3(-e.x, e.y, e.z);

        segments.Add((p000, p100));
        segments.Add((p100, p110));
        segments.Add((p110, p010));
        segments.Add((p010, p000));
        segments.Add((p001, p101));
        segments.Add((p101, p111));
        segments.Add((p111, p011));
        segments.Add((p011, p001));
        segments.Add((p000, p001));
        segments.Add((p100, p101));
        segments.Add((p110, p111));
        segments.Add((p010, p011));
    }

    private static IEnumerable<Collider> GetNearbyColliders(Player executor, Vector3 origin, float radius, string filter, int max)
    {
        return Physics.OverlapSphere(origin, radius, ~0, QueryTriggerInteraction.Collide)
            .Where(c => !ShouldIgnoreAreaCollider(executor, c))
            .Where(c => string.IsNullOrWhiteSpace(filter) || ColliderNameMatches(c, filter))
            .Where(IsDrawableCollider)
            .Distinct()
            .OrderBy(c => Vector3.SqrMagnitude(c.bounds.center - origin))
            .Take(max);
    }

    private static IEnumerable<Collider> GetNamedColliders(string filter, int max)
    {
        return UnityObject.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .Where(c => !IsSessionPrimitiveCollider(c))
            .Where(c => ColliderNameMatches(c, filter))
            .Where(IsDrawableCollider)
            .Distinct()
            .Take(max);
    }

    private static IEnumerable<Collider> GetPlayerColliders(Player player)
    {
        if (player?.GameObject == null)
            return [];

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
           collider.gameObject.activeInHierarchy &&
           IsFinite(collider.bounds.center) &&
           IsFinite(collider.bounds.size);

    private static bool IsValidSegment(Vector3 start, Vector3 end)
        => IsFinite(start) &&
           IsFinite(end) &&
           Vector3.SqrMagnitude(end - start) > 0.000001f;

    private static bool IsFinite(Vector3 value)
        => !float.IsNaN(value.x) &&
           !float.IsNaN(value.y) &&
           !float.IsNaN(value.z) &&
           !float.IsInfinity(value.x) &&
           !float.IsInfinity(value.y) &&
           !float.IsInfinity(value.z);

    private static bool IsPlayerStillAvailable(Player player)
        => player != null && Player.List.Contains(player);

    private static bool IsOffArgument(ArraySegment<string> arguments, int index)
        => arguments.Count > index &&
           (arguments.At(index).Equals("off", StringComparison.OrdinalIgnoreCase) ||
            arguments.At(index).Equals("stop", StringComparison.OrdinalIgnoreCase) ||
            arguments.At(index).Equals("clear", StringComparison.OrdinalIgnoreCase));

    private static string GetColliderName(Collider collider)
    {
        if (collider == null)
            return "null";

        string root = collider.transform.root != null ? collider.transform.root.name : "no-root";
        return $"{root}/{collider.gameObject.name}/{collider.GetType().Name}";
    }

    private static string FormatVector(Vector3 value)
        => $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";

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
            Color color,
            int maxColliders)
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
            MaxColliders = maxColliders;
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
        public int MaxColliders { get; }
        public List<Primitive> PrimitiveLines { get; } = [];
        public CoroutineHandle Handle { get; set; }
        public int LastDrawCount { get; set; }
        public float NextVisibilityRefreshTime { get; set; }
        public bool IsExpired => !float.IsPositiveInfinity(EndTime) && Time.time >= EndTime;
    }
}
