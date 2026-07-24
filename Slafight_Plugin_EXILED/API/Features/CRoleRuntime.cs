using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;

namespace Slafight_Plugin_EXILED.API.Features;

public sealed class CRoleRuntime : IDisposable
{
    private readonly Dictionary<string, CoroutineHandle> _coroutines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action<Player?>> _cleanupActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<Player?, bool> _isValid;
    private readonly string _displayName;
    private bool _disposed;

    internal CRoleRuntime(int playerId, string uniqueRole, string displayName, Func<Player?, bool> isValid)
    {
        PlayerId = playerId;
        UniqueRole = uniqueRole;
        _displayName = displayName;
        _isValid = isValid;
    }

    public int PlayerId { get; }
    public string UniqueRole { get; }
    public Player? Player => Player.Get(PlayerId);
    public bool IsActive => !_disposed && _isValid(Player);
    public IReadOnlyDictionary<string, object> Values => _values;

    internal bool Matches(string uniqueRole)
        => string.Equals(UniqueRole, uniqueRole, StringComparison.OrdinalIgnoreCase);

    public void Set<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _values[key] = value!;
    }

    public T Get<T>(string key, T fallback = default!)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback;

        return _values.TryGetValue(key, out var value) && value is T typed
            ? typed
            : fallback;
    }

    public T GetOrSet<T>(string key, Func<T> factory)
    {
        if (string.IsNullOrWhiteSpace(key))
            return factory != null ? factory() : default!;

        if (_values.TryGetValue(key, out var value) && value is T typed)
            return typed;

        var created = factory != null ? factory() : default!;
        _values[key] = created!;
        return created;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default!;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!_values.TryGetValue(key, out var raw) || raw is not T typed)
            return false;

        value = typed;
        return true;
    }

    public bool Remove(string key)
        => !string.IsNullOrWhiteSpace(key) && _values.Remove(key);

    public CoroutineHandle RunLoop(
        string key,
        float interval,
        Action<Player, CRoleRuntime> tick,
        bool runImmediately = true,
        bool restart = true,
        Action<Player?>? cleanup = null)
    {
        if (string.IsNullOrWhiteSpace(key) || tick == null || _disposed)
            return default;

        if (_coroutines.TryGetValue(key, out var existing))
        {
            if (!restart)
                return existing;

            Stop(key);
        }

        if (cleanup != null)
            _cleanupActions[key] = cleanup;

        var handle = Timing.RunCoroutine(LoopCoroutine(key, Math.Max(0.02f, interval), tick, runImmediately));
        _coroutines[key] = handle;
        return handle;
    }

    public CoroutineHandle Delay(
        string key,
        float delay,
        Action<Player, CRoleRuntime> action,
        bool restart = true,
        Action<Player?>? cleanup = null)
    {
        if (string.IsNullOrWhiteSpace(key) || action == null || _disposed)
            return default;

        if (_coroutines.TryGetValue(key, out var existing))
        {
            if (!restart)
                return existing;

            Stop(key);
        }

        if (cleanup != null)
            _cleanupActions[key] = cleanup;

        var handle = Timing.RunCoroutine(DelayCoroutine(key, Math.Max(0f, delay), action));
        _coroutines[key] = handle;
        return handle;
    }

    public CoroutineHandle SyncEffect(
        string key,
        EffectType effectType,
        Func<Player, byte> intensity,
        float interval = 0.25f,
        float duration = 0f,
        bool disableWhenZero = true,
        bool restart = true)
    {
        if (intensity == null)
            return default;

        return RunLoop(
            key,
            interval,
            (player, _) => ApplyEffect(player, effectType, intensity(player), duration, disableWhenZero),
            cleanup: player =>
            {
                if (disableWhenZero)
                    player?.DisableEffect(effectType);
            },
            restart: restart);
    }

    public CoroutineHandle SyncEffect<T>(
        string key,
        Func<Player, byte> intensity,
        float interval = 0.25f,
        float duration = 0f,
        bool disableWhenZero = true,
        bool restart = true)
        where T : StatusEffectBase
    {
        if (intensity == null)
            return default;

        return RunLoop(
            key,
            interval,
            (player, _) => ApplyEffect<T>(player, intensity(player), duration, disableWhenZero),
            cleanup: player =>
            {
                if (disableWhenZero)
                    player?.DisableEffect<T>();
            },
            restart: restart);
    }

    public bool Stop(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var stopped = false;

        if (_coroutines.TryGetValue(key, out var handle))
        {
            Timing.KillCoroutines(handle);
            _coroutines.Remove(key);
            stopped = true;
        }

        RunCleanup(key);
        return stopped;
    }

    public void StopAll()
    {
        foreach (var key in new List<string>(_coroutines.Keys))
            Stop(key);
    }

    public void Clear()
    {
        StopAll();
        _values.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAll();
        _cleanupActions.Clear();
        _values.Clear();
    }

    private IEnumerator<float> LoopCoroutine(
        string key,
        float interval,
        Action<Player, CRoleRuntime> tick,
        bool runImmediately)
    {
        if (!runImmediately)
            yield return Timing.WaitForSeconds(interval);

        while (!_disposed)
        {
            var player = Player;
            if (!_isValid(player))
                break;

            try
            {
                tick(player!, this);
            }
            catch (Exception ex)
            {
                Log.Warn($"CRoleRuntime loop '{key}' failed for {_displayName}: {ex}");
            }

            yield return Timing.WaitForSeconds(interval);
        }

        FinishCoroutine(key);
    }

    private IEnumerator<float> DelayCoroutine(string key, float delay, Action<Player, CRoleRuntime> action)
    {
        if (delay > 0f)
            yield return Timing.WaitForSeconds(delay);

        if (!_disposed)
        {
            var player = Player;
            if (_isValid(player))
            {
                try
                {
                    action(player!, this);
                }
                catch (Exception ex)
                {
                    Log.Warn($"CRoleRuntime delay '{key}' failed for {_displayName}: {ex}");
                }
            }
        }

        FinishCoroutine(key);
    }

    private void FinishCoroutine(string key)
    {
        _coroutines.Remove(key);
        RunCleanup(key);
    }

    private void RunCleanup(string key)
    {
        if (!_cleanupActions.TryGetValue(key, out var cleanup))
            return;

        _cleanupActions.Remove(key);

        try
        {
            cleanup(Player);
        }
        catch (Exception ex)
        {
            Log.Warn($"CRoleRuntime cleanup '{key}' failed for {_displayName}: {ex}");
        }
    }

    private static void ApplyEffect(
        Player player,
        EffectType effectType,
        byte intensity,
        float duration,
        bool disableWhenZero)
    {
        if (intensity == 0 && disableWhenZero)
        {
            player.DisableEffect(effectType);
            return;
        }

        if (duration > 0f)
            player.EnableEffect(effectType, intensity, duration);
        else
            player.EnableEffect(effectType, intensity);
    }

    private static void ApplyEffect<T>(
        Player player,
        byte intensity,
        float duration,
        bool disableWhenZero)
        where T : StatusEffectBase
    {
        if (intensity == 0 && disableWhenZero)
        {
            player.DisableEffect<T>();
            return;
        }

        if (duration > 0f)
            player.EnableEffect<T>(intensity, duration);
        else
            player.EnableEffect<T>(intensity);
    }
}
