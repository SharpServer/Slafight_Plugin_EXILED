using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerStatsSystem;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

public sealed class CustomShieldState
{
    private static readonly Dictionary<int, CustomShieldState> States = new();
    private static bool _eventsRegistered;

    private CoroutineHandle _decayCoroutine;
    private CoroutineHandle _smoothCoroutine;
    private float _maxValue;

    private CustomShieldState(Player player)
    {
        Player = player;
        _maxValue = player.MaxArtificialHealth;
    }

    public static event Action<CustomShieldAbsorbingDamageEventArgs> AbsorbingDamage;
    public static event Action<CustomShieldChangedEventArgs> Changed;
    public static event Action<CustomShieldChangingEventArgs> Changing;

    public Player Player { get; }
    public bool IsEnabled { get; private set; } = true;
    public bool IsAutoDecayEnabled { get; private set; }
    public bool IsDecaying { get; private set; }
    public float DecayPerSecond { get; private set; }
    public float DamageAcceptingThreshold { get; set; } = 0.01f;
    public float DamageReduction { get; set; }
    public float MaxValue
    {
        get => _maxValue;
        set
        {
            _maxValue = Math.Max(0f, value);
            ApplyAhpProcess(Value);
        }
    }

    public float Value
    {
        get => Player.ArtificialHealth;
        set => SetValue(value);
    }

    public bool CanAcceptDamage => IsEnabled && Value > DamageAcceptingThreshold;

    public static void RegisterEvents()
    {
        if (_eventsRegistered) return;

        PlayerHandlers.Hurting += OnHurting;
        PlayerHandlers.ChangingRole += OnChangingRole;
        PlayerHandlers.Left += OnLeft;
        ServerHandlers.WaitingForPlayers += ClearAll;
        ServerHandlers.RestartingRound += ClearAll;

        _eventsRegistered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_eventsRegistered) return;

        PlayerHandlers.Hurting -= OnHurting;
        PlayerHandlers.ChangingRole -= OnChangingRole;
        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.WaitingForPlayers -= ClearAll;
        ServerHandlers.RestartingRound -= ClearAll;

        ClearAll();
        AbsorbingDamage = null;
        Changed = null;
        Changing = null;
        _eventsRegistered = false;
    }

    public static CustomShieldState GetOrCreate(Player player)
    {
        if (player == null) return null;

        if (States.TryGetValue(player.Id, out var state))
            return state;

        state = new CustomShieldState(player);
        States[player.Id] = state;
        return state;
    }

    public static bool TryGet(Player player, out CustomShieldState state)
    {
        state = null;
        return player != null && States.TryGetValue(player.Id, out state);
    }

    public static void Clear(Player player)
    {
        if (player == null) return;
        if (!States.TryGetValue(player.Id, out var state)) return;

        state.StopDecay();
        state.StopSmooth();
        state.SetValue(0f, CustomShieldChangeReason.Reset);
        state._maxValue = 0f;
        state.KillAhpProcesses();
        States.Remove(player.Id);
    }

    public static void ClearAll()
    {
        foreach (var state in States.Values.ToList())
        {
            state.StopDecay();
            state.StopSmooth();
            state.SetValue(0f, CustomShieldChangeReason.Reset);
            state._maxValue = 0f;
            state.KillAhpProcesses();
        }

        States.Clear();
    }

    public CustomShieldState Configure(
        float maxValue,
        float value,
        float damageReduction = 0f,
        float damageAcceptingThreshold = 0.01f,
        bool autoDecay = false,
        float decayPerSecond = 0f)
    {
        MaxValue = maxValue;
        DamageReduction = Clamp01(damageReduction);
        DamageAcceptingThreshold = Math.Max(0f, damageAcceptingThreshold);
        SetValue(value);
        SetAutoDecay(autoDecay, decayPerSecond);
        return this;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public void SetAutoDecay(bool isEnabled, float decayPerSecond = -1f)
    {
        IsAutoDecayEnabled = isEnabled;
        if (decayPerSecond >= 0f)
            DecayPerSecond = decayPerSecond;

        if (IsAutoDecayEnabled)
            StartDecay();
        else
            StopDecay();
    }

    public void StartDecay(float decayPerSecond = -1f)
    {
        if (decayPerSecond >= 0f)
            DecayPerSecond = decayPerSecond;

        if (DecayPerSecond <= 0f || IsDecaying)
            return;

        IsAutoDecayEnabled = true;
        IsDecaying = true;
        _decayCoroutine = Timing.RunCoroutine(DecayLoop());
    }

    public void StopDecay()
    {
        if (!IsDecaying) return;

        Timing.KillCoroutines(_decayCoroutine);
        IsDecaying = false;
    }

    public void SmoothSet(float targetValue, float duration, CustomShieldChangeReason reason = CustomShieldChangeReason.Smooth)
    {
        StopSmooth();

        if (duration <= 0f)
        {
            SetValue(targetValue, reason);
            return;
        }

        _smoothCoroutine = Timing.RunCoroutine(SmoothValue(Value, targetValue, duration, reason));
    }

    public void SmoothHeal(float amount, float duration)
        => SmoothSet(Value + Math.Max(0f, amount), duration, CustomShieldChangeReason.Heal);

    public void SmoothDamage(float amount, float duration)
        => SmoothSet(Value - Math.Max(0f, amount), duration, CustomShieldChangeReason.Damage);

    public void Damage(float amount)
        => SetValue(Value - Math.Max(0f, amount), CustomShieldChangeReason.Damage);

    public void Heal(float amount)
        => SetValue(Value + Math.Max(0f, amount), CustomShieldChangeReason.Heal);

    public void ConsumeAll()
        => SetValue(0f, CustomShieldChangeReason.Damage);

    private void StopSmooth()
    {
        if (_smoothCoroutine.IsRunning)
            Timing.KillCoroutines(_smoothCoroutine);
    }

    private void SetValue(float value, CustomShieldChangeReason reason = CustomShieldChangeReason.Set)
    {
        float oldValue = Player.ArtificialHealth;
        float newValue = Clamp(value, 0f, MaxValue);
        if (Math.Abs(oldValue - newValue) < 0.001f) return;

        var changing = new CustomShieldChangingEventArgs(Player, this, oldValue, newValue, reason);
        try { Changing?.Invoke(changing); }
        catch (Exception ex) { Log.Error($"CustomShieldState.Changing error: {ex}"); }

        if (!changing.IsAllowed) return;

        newValue = Clamp(changing.NewValue, 0f, MaxValue);
        ApplyAhpProcess(newValue);

        var changed = new CustomShieldChangedEventArgs(Player, this, oldValue, newValue, reason);
        try { Changed?.Invoke(changed); }
        catch (Exception ex) { Log.Error($"CustomShieldState.Changed error: {ex}"); }
    }

    private IEnumerator<float> DecayLoop()
    {
        while (IsPlayerUsable(Player) && IsAutoDecayEnabled && DecayPerSecond > 0f)
        {
            if (Value <= 0f)
            {
                IsDecaying = false;
                yield break;
            }

            SetValue(Value - DecayPerSecond * 0.1f, CustomShieldChangeReason.Decay);
            yield return Timing.WaitForSeconds(0.1f);
        }

        IsDecaying = false;
    }

    private IEnumerator<float> SmoothValue(float startValue, float targetValue, float duration, CustomShieldChangeReason reason)
    {
        float elapsed = 0f;
        targetValue = Clamp(targetValue, 0f, MaxValue);

        while (elapsed < duration)
        {
            if (!IsPlayerUsable(Player))
                yield break;

            elapsed += Time.deltaTime;
            float t = Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            SetValue(startValue + (targetValue - startValue) * t, reason);
            yield return Timing.WaitForOneFrame;
        }

        SetValue(targetValue, reason);
    }

    private void ApplyAhpProcess(float value)
    {
        KillAhpProcesses();

        if (!IsPlayerUsable(Player) || value <= 0f || MaxValue <= 0f)
            return;

        Player.ReferenceHub.playerStats.GetModule<AhpStat>()
            .ServerAddProcess(value, MaxValue, 0f, 0f, 0f, true);
    }

    private void KillAhpProcesses()
    {
        if (!IsPlayerUsable(Player))
            return;

        Player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerKillAllProcesses();
    }

    private static void OnHurting(HurtingEventArgs? ev)
    {
        if (ev?.Player == null || !ev.IsAllowed) return;
        if (ev.Amount <= 0f && !ev.IsInstantKill) return;
        if (!TryGet(ev.Player, out var state) || !state.CanAcceptDamage) return;

        float originalAmount = Math.Max(0f, ev.Amount);
        float reducedHealthDamage = originalAmount * (1f - Clamp01(state.DamageReduction));
        float requestedShieldDamage = originalAmount - reducedHealthDamage;
        float availableShield = state.Value;
        float actualShieldDamage = Math.Min(availableShield, Math.Max(0f, requestedShieldDamage));
        float overflow = Math.Max(0f, requestedShieldDamage - actualShieldDamage);

        var absorbing = new CustomShieldAbsorbingDamageEventArgs(
            ev,
            state,
            originalAmount,
            actualShieldDamage,
            reducedHealthDamage + overflow);

        try { AbsorbingDamage?.Invoke(absorbing); }
        catch (Exception ex) { Log.Error($"CustomShieldState.AbsorbingDamage error: {ex}"); }

        if (!absorbing.IsAllowed) return;

        float shieldDamage = Math.Max(0f, absorbing.ShieldDamage);
        float healthDamage = Math.Max(0f, absorbing.HealthDamage);
        state.SetValue(state.Value - shieldDamage, CustomShieldChangeReason.AbsorbDamage);
        ev.Amount = healthDamage;
    }

    private static void OnChangingRole(ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null || !ev.IsAllowed) return;
        Clear(ev.Player);
    }

    private static void OnLeft(LeftEventArgs? ev)
    {
        Clear(ev?.Player);
    }

    private static float Clamp(float value, float min, float max)
        => Math.Min(Math.Max(value, min), max);

    private static float Clamp01(float value)
        => Clamp(value, 0f, 1f);

    private static bool IsPlayerUsable(Player player)
        => player?.ReferenceHub != null;
}
