using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

public class FacilityControlRoom : SlafightLabApiHandler, IBootstrapHandler
{
    private const float ConsoleInteractRadius = 3f;
    private const float SessionMaxDistance = 4.5f;
    private const float HintRefreshSeconds = 0.8f;

    private static readonly Vector3 DefaultConsolePosition = new(107.921f, 296.313f, -68.748f);
    private static readonly IReadOnlyList<FacilityControlRoomFunction> Functions = CreateFunctions();

    private static FacilityControlRoom _instance;

    private readonly Dictionary<Exiled.API.Features.Player, FacilityControlRoomSession> _sessions = [];
    private readonly Dictionary<string, int> _functionExecutionCounts = [];
    private readonly Dictionary<string, float> _functionCooldownReadyTimes = [];

    public static FacilityControlRoom Instance => _instance;
    public static bool IsAntiMemeProtocolActive => AntiMemeProtocolFunction.IsActive;
    public static bool HasAntiMemeProtocolActivatedInPast => AntiMemeProtocolFunction.HasActivatedInPast;

    public static void Register()
    {
        _instance = LabApiHandlerRegistry.Register(_instance);
    }

    public static void Unregister()
    {
        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.SearchedToy += OnSearchedToy, () => LabApi.Events.Handlers.PlayerEvents.SearchedToy -= OnSearchedToy);
        subscriptions.Add(() => LabApi.Events.Handlers.ServerEvents.RoundStarted += ResetState, () => LabApi.Events.Handlers.ServerEvents.RoundStarted -= ResetState);
        subscriptions.Add(() => Exiled.Events.Handlers.Server.WaitingForPlayers += ResetState, () => Exiled.Events.Handlers.Server.WaitingForPlayers -= ResetState);
        subscriptions.Add(() => Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem, () => Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem);
        subscriptions.Add(() => Exiled.Events.Handlers.Player.ChangingItem += OnChangingItem, () => Exiled.Events.Handlers.Player.ChangingItem -= OnChangingItem);
        subscriptions.Add(() => Exiled.Events.Handlers.Player.Left += OnPlayerLeft, () => Exiled.Events.Handlers.Player.Left -= OnPlayerLeft);
    }

    private void ResetState()
    {
        foreach (var session in _sessions.Values)
            Timing.KillCoroutines(session.HintLoop);

        _sessions.Clear();
        _functionExecutionCounts.Clear();
        _functionCooldownReadyTimes.Clear();

        foreach (var function in Functions)
            function.ResetState();
    }

    private void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
            EndSession(ev.Player, null);
    }

    private void OnSearchedToy(LabApi.Events.Arguments.PlayerEvents.PlayerSearchedToyEventArgs ev)
    {
        var consolePosition = GetConsolePosition();
        if (ev.Interactable == null || Vector3.Distance(ev.Interactable.Position, consolePosition) >= ConsoleInteractRadius)
            return;

        var player = Exiled.API.Features.Player.Get(ev.Player);
        if (player == null)
            return;

        if (_sessions.TryGetValue(player, out var session))
        {
            if (!TryGetStagedKeycard(player, session, out var stagedKeycard))
            {
                EndSession(player, "<size=24>制御室操作を終了しました。\nステージされたキーカードが選択されていません。</size>");
                return;
            }

            ExecuteSelectedFunction(player, stagedKeycard, session);
            return;
        }

        TryStartSession(player, consolePosition);
    }

    private void OnDroppingItem(DroppingItemEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null)
            return;

        if (!_sessions.TryGetValue(ev.Player, out var session))
            return;

        if (ev.Item.Serial != session.StagedItemSerial)
            return;

        ev.IsAllowed = false;
        session.SelectedFunctionIndex = (session.SelectedFunctionIndex + 1) % Functions.Count;
        ev.Player.ShowHint(BuildModeHint(session), 1.2f);
    }

    private void OnChangingItem(ChangingItemEventArgs ev)
    {
        if (ev.Player == null)
            return;

        if (!_sessions.TryGetValue(ev.Player, out var session))
            return;

        if (ev.Player.CurrentItem?.Serial != session.StagedItemSerial)
            return;

        if (ev.Item?.Serial == session.StagedItemSerial)
            return;

        EndSession(ev.Player, "<size=24>制御室操作を終了しました。\nステージされたキーカードから切り替えました。</size>");
    }

    private void TryStartSession(Exiled.API.Features.Player player, Vector3 consolePosition)
    {
        if (player.CurrentItem is not Keycard keycard)
        {
            player.ShowHint("<size=24>制御室操作を終了しました。\n管理権限を持つキーカードを手に持ってください。</size>", 3.5f);
            return;
        }

        var session = new FacilityControlRoomSession(player, keycard.Serial, consolePosition);
        session.HintLoop = Timing.RunCoroutine(HintLoopCoroutine(session));
        _sessions[player] = session;

        player.ShowHint(BuildModeHint(session), 1.2f);
    }

    private void ExecuteSelectedFunction(
        Exiled.API.Features.Player player,
        Keycard stagedKeycard,
        FacilityControlRoomSession session)
    {
        var function = Functions[session.SelectedFunctionIndex];
        var executedCount = _functionExecutionCounts.TryGetValue(function.Id, out var count) ? count : 0;

        if (!HasRequiredPermissions(stagedKeycard.Permissions, function.RequiredPermissions))
        {
            ShowFunctionHint(player, session, BuildPermissionBlockedHint(function));
            return;
        }

        if (IsExecutionLimitReached(function, executedCount))
        {
            ShowFunctionHint(player, session, function.GetExecutionLimitBlockedHint(executedCount));
            return;
        }

        if (TryGetCooldownRemaining(function, out var remainingSeconds))
        {
            ShowFunctionHint(player, session, function.GetCooldownBlockedHint(remainingSeconds));
            return;
        }

        var context = new FacilityControlRoomFunctionContext(player, stagedKeycard, executedCount);
        var result = function.Execute(context);

        if (result.CountAsExecution)
        {
            _functionExecutionCounts[function.Id] = executedCount + 1;
            StartCooldown(function);
        }

        if (!string.IsNullOrWhiteSpace(result.Hint))
            ShowFunctionHint(player, session, result.Hint);
    }

    private IEnumerator<float> HintLoopCoroutine(FacilityControlRoomSession session)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(HintRefreshSeconds);

            if (Round.IsLobby || !_sessions.ContainsKey(session.Player))
                yield break;

            if (session.Player?.ReferenceHub == null)
            {
                EndSession(session.Player, null);
                yield break;
            }

            if (Vector3.Distance(session.Player.Position, session.ConsolePosition) > SessionMaxDistance)
            {
                EndSession(session.Player, "<size=24>制御室操作を終了しました。\n制御盤から離れました。</size>");
                yield break;
            }

            if (!TryGetStagedKeycard(session.Player, session, out _))
            {
                EndSession(session.Player, "<size=24>制御室操作を終了しました。\nステージされたキーカードが選択されていません。</size>");
                yield break;
            }

            if (Time.time >= session.SuppressModeHintUntil)
                session.Player.ShowHint(BuildModeHint(session), 1.2f);
        }
    }

    private void EndSession(Exiled.API.Features.Player player, string hint)
    {
        if (!_sessions.TryGetValue(player, out var session))
            return;

        Timing.KillCoroutines(session.HintLoop);
        _sessions.Remove(player);

        if (!string.IsNullOrWhiteSpace(hint) && player.ReferenceHub != null && player.IsConnected && !player.IsNPC)
            player.ShowHint(hint, 3.5f);
    }

    private static bool TryGetStagedKeycard(
        Exiled.API.Features.Player player,
        FacilityControlRoomSession session,
        out Keycard keycard)
    {
        keycard = player.CurrentItem as Keycard;
        return keycard != null &&
               keycard.Serial == session.StagedItemSerial;
    }

    private string BuildModeHint(FacilityControlRoomSession session)
    {
        var function = Functions[session.SelectedFunctionIndex];
        var keycard = session.Player.CurrentItem as Keycard;
        var permissionText = BuildRequiredPermissionStatus(function, keycard);
        var statusText = BuildFunctionStatusText(function);
        return
            $"<size=24>施設管理者制御室 操作モード</size>\n" +
            $"<size=22>現在選択されている機能：{function.DisplayName}</size>\n" +
            $"<size=20>{function.Description}\n{permissionText}{statusText}\nキーカードをドロップ：機能切替 / 再度インタラクト：実行</size>";
    }

    private static bool HasRequiredPermissions(KeycardPermissions actual, KeycardPermissions required)
    {
        if (required == KeycardPermissions.None)
            return true;

        var actualLevels = KeycardAccessLevels.FromPermissions(actual);
        var requiredLevels = KeycardAccessLevels.FromPermissions(required);

        return actualLevels.Containment >= requiredLevels.Containment &&
               actualLevels.Armory >= requiredLevels.Armory &&
               actualLevels.Administration >= requiredLevels.Administration;
    }

    private static string BuildRequiredPermissionStatus(FacilityControlRoomFunction function, Keycard? keycard)
    {
        var hasPermission = keycard != null && HasRequiredPermissions(keycard.Permissions, function.RequiredPermissions);
        var color = hasPermission ? "#57ff7a" : "#ff4d4d";
        return $"必要権限：<color={color}>{BuildRequiredPermissionText(function.RequiredPermissions)}</color>";
    }

    private static string BuildPermissionBlockedHint(FacilityControlRoomFunction function)
    {
        return $"<size=24>{function.DisplayName} は実行できません。\n必要権限：<color=#ff4d4d>{BuildRequiredPermissionText(function.RequiredPermissions)}</color></size>";
    }

    private static string BuildRequiredPermissionText(KeycardPermissions permissions)
    {
        if (permissions == KeycardPermissions.None)
            return "なし";

        var levels = KeycardAccessLevels.FromPermissions(permissions);
        var parts = new List<string>();

        if (levels.Containment > 0)
            parts.Add($"Containment Level {levels.Containment}");

        if (levels.Armory > 0)
            parts.Add($"Armory Level {levels.Armory}");

        if (levels.Administration > 0)
            parts.Add(GetAdministrationPermissionText(permissions, levels.Administration));

        return string.Join(", ", parts);
    }

    private static string GetAdministrationPermissionText(KeycardPermissions permissions, int administrationLevel)
    {
        if ((permissions & KeycardPermissions.AlphaWarhead) == KeycardPermissions.AlphaWarhead)
            return "AlphaWarhead";

        if ((permissions & KeycardPermissions.ExitGates) == KeycardPermissions.ExitGates)
            return "ExitGates";

        if ((permissions & KeycardPermissions.Checkpoints) == KeycardPermissions.Checkpoints &&
            (permissions & KeycardPermissions.Intercom) == KeycardPermissions.Intercom)
            return "Checkpoints + Intercom";

        return $"Administration Level {administrationLevel}";
    }

    private static Vector3 GetConsolePosition()
    {
        return MapFlags.AntiMemeButton == Vector3.zero
            ? DefaultConsolePosition
            : MapFlags.AntiMemeButton;
    }

    private static bool IsExecutionLimitReached(FacilityControlRoomFunction function, int executedCount)
    {
        return function.UseExecutionLimit &&
               function.MaxExecutionCount >= 0 &&
               executedCount >= function.MaxExecutionCount;
    }

    private bool TryGetCooldownRemaining(FacilityControlRoomFunction function, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (!function.UseCooldown)
            return false;

        if (!_functionCooldownReadyTimes.TryGetValue(function.Id, out var readyAt))
            return false;

        remainingSeconds = readyAt - Time.time;
        return remainingSeconds > 0f;
    }

    private void StartCooldown(FacilityControlRoomFunction function)
    {
        if (!function.UseCooldown || function.CooldownSeconds <= 0f)
            return;

        _functionCooldownReadyTimes[function.Id] = Time.time + function.CooldownSeconds;
    }

    private string BuildFunctionStatusText(FacilityControlRoomFunction function)
    {
        var parts = new List<string>();

        if (function.UseExecutionLimit)
        {
            var executedCount = _functionExecutionCounts.TryGetValue(function.Id, out var count) ? count : 0;
            parts.Add($"使用回数：{executedCount}/{function.MaxExecutionCount}");
        }

        if (TryGetCooldownRemaining(function, out var remainingSeconds))
            parts.Add($"クールダウン：{remainingSeconds:F0}秒");

        return parts.Count <= 0 ? string.Empty : $"\n{string.Join(" / ", parts)}";
    }

    private static void ShowFunctionHint(
        Exiled.API.Features.Player player,
        FacilityControlRoomSession session,
        string hint)
    {
        session.SuppressModeHintUntil = Time.time + 3f;
        player.ShowHint(hint, 3.5f);
    }

    private static IReadOnlyList<FacilityControlRoomFunction> CreateFunctions()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                typeof(FacilityControlRoomFunction).IsAssignableFrom(type) &&
                type.IsClass &&
                !type.IsAbstract &&
                type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (FacilityControlRoomFunction)Activator.CreateInstance(type))
            .OrderBy(function => function.Order)
            .ThenBy(function => function.Id)
            .ToList();
    }

    private sealed class FacilityControlRoomSession
    {
        public FacilityControlRoomSession(
            Exiled.API.Features.Player player,
            ushort stagedItemSerial,
            Vector3 consolePosition)
        {
            Player = player;
            StagedItemSerial = stagedItemSerial;
            ConsolePosition = consolePosition;
        }

        public Exiled.API.Features.Player Player { get; }
        public ushort StagedItemSerial { get; }
        public Vector3 ConsolePosition { get; }
        public int SelectedFunctionIndex { get; set; }
        public CoroutineHandle HintLoop { get; set; }
        public float SuppressModeHintUntil { get; set; }
    }
}

public static class FacilityContorlRoom
{
    public static bool IsAntiMemeProtocolActive => FacilityControlRoom.IsAntiMemeProtocolActive;
    public static bool HasAntiMemeProtocolActivatedInPast => FacilityControlRoom.HasAntiMemeProtocolActivatedInPast;
}
