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
    private const int PrimitiveSegmentsPerColliderBudget = 48;

    private static readonly Dictionary<int, HitboxDrawSession> Sessions = new();
    private static readonly Dictionary<string, HitboxDrawPreferences> Preferences = new();
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
                "  color [#RRGGBB|name|reset]\n" +
                "  playermode [simple|full]\n" +
                "  options\n" +
                "  probe [range]\n" +
                "  list\n" +
                "  clear [all|sessionId]\n" +
                "Default visibility is self. Use duration `on` to draw until cleared. Color names: red/green/blue/yellow/cyan/magenta/white/black.";
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
            "color" or "colour" or "linecolor" => SetColorPreference(arguments, executor, out response),
            "playermode" or "playerstyle" or "playerhitbox" or "simplifyplayers" => SetPlayerModePreference(arguments, executor, out response),
            "options" or "prefs" or "settings" => ShowPreferences(executor, out response),
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
        HitboxDrawPreferences preferences = GetPreferences(executor);
        Color defaultColor = Color.cyan;
        Color color = ParseColor(arguments, preferences.LineColor ?? defaultColor);
        bool simplifyPlayers = ParseSimplifyPlayers(arguments, preferences.SimplifyPlayers);

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
            color,
            defaultColor,
            simplifyPlayers: simplifyPlayers);

        response = StartedMessage(id, "look", colliders.Length, duration, visibleToAll, color, simplifyPlayers);
        return true;
    }

    private static bool DrawNear(ArraySegment<string> arguments, Player executor, out string response)
    {
        float radius = ParseFloat(arguments, 1, DefaultNearRadius, 0.25f, 100f);

        if (IsOffArgument(arguments, 1) || IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        HitboxDrawPreferences preferences = GetPreferences(executor);
        Color defaultColor = Color.yellow;
        Color color = ParseColor(arguments, preferences.LineColor ?? defaultColor);
        bool simplifyPlayers = ParseSimplifyPlayers(arguments, preferences.SimplifyPlayers);
        string filter = GetOptionStrippedText(arguments, 4);

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
            color,
            defaultColor,
            simplifyPlayers: simplifyPlayers);

        response = StartedMessage(id, "near", initial.Length, duration, visibleToAll, color, simplifyPlayers);
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
        HitboxDrawPreferences preferences = GetPreferences(executor);
        Color defaultColor = Color.yellow;
        Color color = ParseColor(arguments, preferences.LineColor ?? defaultColor);
        bool simplifyPlayers = ParseSimplifyPlayers(arguments, preferences.SimplifyPlayers);

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
            color,
            defaultColor,
            max,
            simplifyPlayers);

        response = StartedMessage(id, "area", initial.Length, duration, visibleToAll, color, simplifyPlayers);
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
        HitboxDrawPreferences preferences = GetPreferences(executor);
        Color defaultColor = Color.magenta;
        Color color = ParseColor(arguments, preferences.LineColor ?? defaultColor);
        bool simplifyPlayers = ParseSimplifyPlayers(arguments, preferences.SimplifyPlayers);

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
            color,
            defaultColor,
            simplifyPlayers: simplifyPlayers);

        response = StartedMessage(id, "name", initial.Length, duration, visibleToAll, color, simplifyPlayers);
        return true;
    }

    private static bool DrawPlayer(ArraySegment<string> arguments, Player executor, out string response)
    {
        string targetArg = arguments.Count >= 2 ? arguments.At(1) : "@me";

        if (IsOffArgument(arguments, 1) || IsOffArgument(arguments, 2))
            return ClearOwnerSessions(executor, out response);

        float duration = ParseDuration(arguments, 2, DefaultDuration);
        bool visibleToAll = ParseVisibility(arguments, 3);
        HitboxDrawPreferences preferences = GetPreferences(executor);
        Color defaultColor = Color.green;
        Color color = ParseColor(arguments, preferences.LineColor ?? defaultColor);
        bool simplifyPlayers = ParseSimplifyPlayers(arguments, preferences.SimplifyPlayers);

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

            int allId = StartSession(executor, "players:all", ResolveAll, duration, visibleToAll, color, defaultColor, simplifyPlayers: simplifyPlayers);
            response = StartedMessage(allId, "player all", initialAll.Length, duration, visibleToAll, color, simplifyPlayers);
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
            color,
            defaultColor,
            simplifyPlayers: simplifyPlayers);

        response = StartedMessage(id, "player", initial.Length, duration, visibleToAll, color, simplifyPlayers);
        return true;
    }

    private static bool SetColorPreference(ArraySegment<string> arguments, Player executor, out string response)
    {
        HitboxDrawPreferences preferences = GetPreferences(executor);
        if (arguments.Count < 2)
        {
            response = preferences.LineColor.HasValue
                ? $"Hitbox line color: {FormatColor(preferences.LineColor.Value)}. Usage: sl hitbox color #RRGGBB|name|reset"
                : "Hitbox line color: mode default. Usage: sl hitbox color #RRGGBB|name|reset";
            return true;
        }

        string value = arguments.At(1);
        if (value.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("mode", StringComparison.OrdinalIgnoreCase))
        {
            preferences.LineColor = null;
            int affected = ApplyPreferencesToOwnerSessions(executor, preferences);
            response = $"Hitbox line color reset to per-mode defaults. Updated {affected} active session(s).";
            return true;
        }

        if (!TryParseColor(value, out Color color))
        {
            response = $"Invalid hitbox color: {value}. Use #RRGGBB, #RRGGBBAA, or red/green/blue/yellow/cyan/magenta/white/black/gray.";
            return false;
        }

        preferences.LineColor = color;
        int updated = ApplyPreferencesToOwnerSessions(executor, preferences);
        response = $"Hitbox line color set to {FormatColor(color)}. Updated {updated} active session(s).";
        return true;
    }

    private static bool SetPlayerModePreference(ArraySegment<string> arguments, Player executor, out string response)
    {
        HitboxDrawPreferences preferences = GetPreferences(executor);
        if (arguments.Count < 2)
        {
            response = $"Hitbox player mode: {(preferences.SimplifyPlayers ? "simple" : "full")}. Usage: sl hitbox playermode simple|full";
            return true;
        }

        string value = arguments.At(1);
        if (!TryParseSimplifyPlayers(value, out bool simplifyPlayers))
        {
            response = $"Invalid hitbox player mode: {value}. Use simple or full.";
            return false;
        }

        preferences.SimplifyPlayers = simplifyPlayers;
        int updated = ApplyPreferencesToOwnerSessions(executor, preferences);
        response = $"Hitbox player mode set to {(simplifyPlayers ? "simple" : "full")}. Updated {updated} active session(s).";
        return true;
    }

    private static bool ShowPreferences(Player executor, out string response)
    {
        HitboxDrawPreferences preferences = GetPreferences(executor);
        response =
            "Hitbox options\n" +
            $"  Color: {(preferences.LineColor.HasValue ? FormatColor(preferences.LineColor.Value) : "mode default")}\n" +
            $"  Player mode: {(preferences.SimplifyPlayers ? "simple" : "full")}";
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

    private static int ClearOwnerSessions(Player executor, string? labelPrefix)
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
                $"  #{s.Id} {s.Label} owner={s.OwnerName} visible={(s.VisibleToAll ? "all" : "self")} color={FormatColor(s.Color)} players={(s.SimplifyPlayers ? "simple" : "full")} last={s.LastDrawCount} remaining={FormatRemaining(s)}"));
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
        Color defaultColor,
        int maxColliders = DefaultMaxCollidersPerSession,
        bool simplifyPlayers = true)
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
            defaultColor,
            maxColliders,
            simplifyPlayers);

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

            RedrawSession(session);
            yield return Timing.WaitForSeconds(PrimitiveRedrawInterval);
        }

        StopSession(session.Id);
    }

    private static void RedrawSession(HitboxDrawSession session)
    {
        Collider[] colliders = session.ResolveColliders()
            .Where(IsDrawableCollider)
            .Take(session.MaxColliders)
            .ToArray();

        session.LastDrawCount = RenderPrimitiveHitboxes(session, colliders);
        RefreshPrimitiveVisibilityIfNeeded(session);
    }

    private static int ApplyPreferencesToOwnerSessions(Player owner, HitboxDrawPreferences preferences)
    {
        if (owner == null)
            return 0;

        int updated = 0;
        foreach (HitboxDrawSession session in Sessions.Values
                     .Where(s => s.OwnerUserId == owner.UserId)
                     .ToArray())
        {
            session.Color = preferences.LineColor ?? session.DefaultColor;
            session.SimplifyPlayers = preferences.SimplifyPlayers;
            RedrawSession(session);
            updated++;
        }

        return updated;
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

    private static bool ShouldIgnoreLookCollider(Player? executor, Collider collider)
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

    private static bool IsPlayerCollider(Player? player, Collider collider)
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
            if (session.SimplifyPlayers && TryGetColliderPlayer(collider, out Player player))
            {
                if (renderedPlayers.Add(player.Id))
                    AddPlayerCollisionSegments(player, segments);

                continue;
            }

            AddColliderSegments(collider, segments);
        }

        segments = segments
            .Where(s => IsValidSegment(s.start, s.end))
            .Take(session.MaxColliders * PrimitiveSegmentsPerColliderBudget)
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

    private static bool TryAddCharacterControllerSegments(Player? player, List<(Vector3 start, Vector3 end)> segments)
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

    private static void AddColliderSegments(Collider collider, List<(Vector3 start, Vector3 end)> segments)
    {
        switch (collider)
        {
            case BoxCollider box:
                AddBoxColliderSegments(box, segments);
                break;
            case CapsuleCollider capsule:
                AddCapsuleColliderSegments(capsule, segments);
                break;
            case SphereCollider sphere:
                AddSphereColliderSegments(sphere, segments);
                break;
            case CharacterController controller:
                AddCharacterControllerSegments(controller, segments);
                break;
            case MeshCollider mesh when mesh.sharedMesh != null:
                AddLocalBoundsSegments(mesh.transform, mesh.sharedMesh.bounds, segments);
                break;
            default:
                AddBoundsSegments(collider.bounds, segments);
                break;
        }
    }

    private static void AddBoxColliderSegments(BoxCollider box, List<(Vector3 start, Vector3 end)> segments)
        => AddLocalBoundsSegments(box.transform, new Bounds(box.center, box.size), segments);

    private static void AddLocalBoundsSegments(Transform transform, Bounds localBounds, List<(Vector3 start, Vector3 end)> segments)
    {
        Vector3 c = localBounds.center;
        Vector3 e = localBounds.extents;

        Vector3 p000 = transform.TransformPoint(c + new Vector3(-e.x, -e.y, -e.z));
        Vector3 p100 = transform.TransformPoint(c + new Vector3(e.x, -e.y, -e.z));
        Vector3 p110 = transform.TransformPoint(c + new Vector3(e.x, -e.y, e.z));
        Vector3 p010 = transform.TransformPoint(c + new Vector3(-e.x, -e.y, e.z));
        Vector3 p001 = transform.TransformPoint(c + new Vector3(-e.x, e.y, -e.z));
        Vector3 p101 = transform.TransformPoint(c + new Vector3(e.x, e.y, -e.z));
        Vector3 p111 = transform.TransformPoint(c + new Vector3(e.x, e.y, e.z));
        Vector3 p011 = transform.TransformPoint(c + new Vector3(-e.x, e.y, e.z));

        AddBoxEdges(p000, p100, p110, p010, p001, p101, p111, p011, segments);
    }

    private static void AddCapsuleColliderSegments(CapsuleCollider capsule, List<(Vector3 start, Vector3 end)> segments)
    {
        Transform transform = capsule.transform;
        Vector3 scale = Abs(transform.lossyScale);
        Vector3 center = transform.TransformPoint(capsule.center);
        GetCapsuleAxes(transform, capsule.direction, out Vector3 axis, out Vector3 right, out Vector3 forward);

        float axisScale = capsule.direction switch
        {
            0 => scale.x,
            1 => scale.y,
            _ => scale.z,
        };

        float radialScale = capsule.direction switch
        {
            0 => Mathf.Max(scale.y, scale.z),
            1 => Mathf.Max(scale.x, scale.z),
            _ => Mathf.Max(scale.x, scale.y),
        };

        float radius = capsule.radius * radialScale;
        float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
        AddCapsuleSegments(center, axis, right, forward, height, radius, segments);
    }

    private static void AddCharacterControllerSegments(CharacterController controller, List<(Vector3 start, Vector3 end)> segments)
    {
        Transform transform = controller.transform;
        Vector3 scale = Abs(transform.lossyScale);
        Vector3 center = transform.TransformPoint(controller.center);
        float radius = controller.radius * Mathf.Max(scale.x, scale.z);
        float height = Mathf.Max(controller.height * scale.y, radius * 2f);

        AddCapsuleSegments(center, transform.up, transform.right, transform.forward, height, radius, segments);
    }

    private static void AddSphereColliderSegments(SphereCollider sphere, List<(Vector3 start, Vector3 end)> segments)
    {
        Transform transform = sphere.transform;
        Vector3 scale = Abs(transform.lossyScale);
        Vector3 center = transform.TransformPoint(sphere.center);
        Vector3 right = transform.right * sphere.radius * scale.x;
        Vector3 up = transform.up * sphere.radius * scale.y;
        Vector3 forward = transform.forward * sphere.radius * scale.z;

        AddEllipseSegments(center, right, up, segments);
        AddEllipseSegments(center, right, forward, segments);
        AddEllipseSegments(center, forward, up, segments);
    }

    private static void GetCapsuleAxes(Transform transform, int direction, out Vector3 axis, out Vector3 right, out Vector3 forward)
    {
        switch (direction)
        {
            case 0:
                axis = transform.right;
                right = transform.up;
                forward = transform.forward;
                break;
            case 2:
                axis = transform.forward;
                right = transform.right;
                forward = transform.up;
                break;
            default:
                axis = transform.up;
                right = transform.right;
                forward = transform.forward;
                break;
        }
    }

    private static void AddEllipseSegments(
        Vector3 center,
        Vector3 axisA,
        Vector3 axisB,
        List<(Vector3 start, Vector3 end)> segments)
    {
        const int sides = 16;
        Vector3 previous = center + axisA;
        for (int i = 1; i <= sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            Vector3 current = center + axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle);
            segments.Add((previous, current));
            previous = current;
        }
    }

    private static Vector3 Abs(Vector3 value)
        => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

    private static void AddBoxEdges(
        Vector3 p000,
        Vector3 p100,
        Vector3 p110,
        Vector3 p010,
        Vector3 p001,
        Vector3 p101,
        Vector3 p111,
        Vector3 p011,
        List<(Vector3 start, Vector3 end)> segments)
    {
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

    private static bool IsSessionPrimitiveCollider(Collider? collider)
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

        AddBoxEdges(p000, p100, p110, p010, p001, p101, p111, p011, segments);
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

    private static IEnumerable<Collider> GetPlayerColliders(Player? player)
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

    private static Color ParseColor(ArraySegment<string> arguments, Color fallback)
    {
        foreach (string optionValue in GetOptionValues(arguments, "color", "colour", "c", "linecolor"))
        {
            if (TryParseColor(optionValue, out Color color))
                return color;
        }

        return fallback;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();
        if (value[0] == '#')
            return TryParseHexColor(value, out color);

        return value.ToLowerInvariant() switch
        {
            "red" => SetColor(Color.red, out color),
            "green" => SetColor(Color.green, out color),
            "blue" => SetColor(Color.blue, out color),
            "yellow" => SetColor(Color.yellow, out color),
            "cyan" => SetColor(Color.cyan, out color),
            "magenta" => SetColor(Color.magenta, out color),
            "white" => SetColor(Color.white, out color),
            "black" => SetColor(Color.black, out color),
            "gray" or "grey" => SetColor(Color.gray, out color),
            _ => false,
        };
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        color = default;
        string hex = value[1..];
        if (hex.Length != 6 && hex.Length != 8)
            return false;

        if (!byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        byte a = 255;
        if (hex.Length == 8 &&
            !byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a))
        {
            return false;
        }

        color = new Color32(r, g, b, a);
        return true;
    }

    private static bool SetColor(Color value, out Color color)
    {
        color = value;
        return true;
    }

    private static HitboxDrawPreferences GetPreferences(Player? player)
    {
        string key = player?.UserId ?? string.Empty;
        if (!Preferences.TryGetValue(key, out HitboxDrawPreferences preferences))
        {
            preferences = new HitboxDrawPreferences();
            Preferences[key] = preferences;
        }

        return preferences;
    }

    private static bool ParseSimplifyPlayers(ArraySegment<string> arguments, bool fallback)
    {
        foreach (string optionValue in GetOptionValues(arguments, "players", "playerhitbox", "playerhitboxes", "simplifyplayers"))
        {
            if (TryParseSimplifyPlayers(optionValue, out bool simplifyPlayers))
                return simplifyPlayers;
        }

        return fallback;
    }

    private static bool TryParseSimplifyPlayers(string value, out bool simplifyPlayers)
    {
        simplifyPlayers = true;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "simple":
            case "simplified":
            case "simplify":
            case "true":
            case "on":
            case "1":
                simplifyPlayers = true;
                return true;
            case "full":
            case "raw":
            case "exact":
            case "detailed":
            case "false":
            case "off":
            case "0":
                simplifyPlayers = false;
                return true;
            default:
                return false;
        }
    }

    private static IEnumerable<string> GetOptionValues(ArraySegment<string> arguments, params string[] names)
    {
        for (int i = 0; i < arguments.Count; i++)
        {
            string arg = arguments.At(i);
            int split = arg.IndexOf('=');
            if (split <= 0)
                continue;

            string name = arg[..split];
            if (names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
                yield return arg[(split + 1)..];
        }
    }

    private static string GetOptionStrippedText(ArraySegment<string> arguments, int startIndex)
    {
        if (arguments.Count <= startIndex)
            return string.Empty;

        return string.Join(" ", arguments
            .Skip(startIndex)
            .Where(arg => !IsDrawOption(arg)));
    }

    private static bool IsDrawOption(string arg)
    {
        int split = arg.IndexOf('=');
        if (split <= 0)
            return false;

        string name = arg[..split];
        return name.Equals("color", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("colour", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("c", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("linecolor", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("players", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("playerhitbox", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("playerhitboxes", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("simplifyplayers", StringComparison.OrdinalIgnoreCase);
    }

    private static string StartedMessage(
        int id,
        string mode,
        int colliders,
        float duration,
        bool visibleToAll,
        Color color,
        bool simplifyPlayers)
        => $"Started hitbox draw #{id} ({mode}) colliders={colliders} visible={(visibleToAll ? "all" : "self")} color={FormatColor(color)} players={(simplifyPlayers ? "simple" : "full")} duration={(duration <= 0f ? "until clear" : $"{duration:0.#}s")}. Use `sl hitbox clear {id}` or `sl hitbox clear`.";

    private static string FormatColor(Color color)
    {
        var color32 = (Color32)color;
        return color32.a == 255
            ? $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}"
            : $"#{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
    }

    private static string FormatRemaining(HitboxDrawSession session)
        => float.IsPositiveInfinity(session.EndTime)
            ? "until clear"
            : $"{Mathf.Max(0f, session.EndTime - Time.time):0.#}s";

    private sealed class HitboxDrawPreferences
    {
        public Color? LineColor { get; set; }
        public bool SimplifyPlayers { get; set; } = true;
    }

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
            Color defaultColor,
            int maxColliders,
            bool simplifyPlayers)
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
            DefaultColor = defaultColor;
            MaxColliders = maxColliders;
            SimplifyPlayers = simplifyPlayers;
        }

        public int Id { get; }
        public string OwnerUserId { get; }
        public string OwnerName { get; }
        public string Label { get; }
        public Func<IEnumerable<Collider>> ResolveColliders { get; }
        public float EndTime { get; }
        public bool VisibleToAll { get; }
        public Player SelfViewer { get; }
        public Color Color { get; set; }
        public Color DefaultColor { get; }
        public int MaxColliders { get; }
        public bool SimplifyPlayers { get; set; }
        public List<Primitive> PrimitiveLines { get; } = [];
        public CoroutineHandle Handle { get; set; }
        public int LastDrawCount { get; set; }
        public float NextVisibilityRefreshTime { get; set; }
        public bool IsExpired => !float.IsPositiveInfinity(EndTime) && Time.time >= EndTime;
    }
}
