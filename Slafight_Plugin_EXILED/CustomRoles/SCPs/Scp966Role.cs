using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp966Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-966";

    protected override string Description { get; set; } = "肉眼では捉えられない睡眠殺し。\n" +
                                                          "20m以内の獲物へ睡眠阻害波を浴びせ、疲弊と幻覚を蓄積させろ。\n" +
                                                          "赤外線・S-NAV・異常視覚装備には輪郭が漏れる。死体を摂食して狩猟本能を高めよ。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp966;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp966";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp3114;
    protected override float? SpawnMaxHealth => 966f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-966";

    private const float WaveRange = 20f;
    private const float MaxSleepDebt = 100f;
    private const float IntermittentVisibilityDebt = 60f;
    private const float PassiveDebtGain = 2.8f;
    private const float SameRoomMultiplier = 1f;
    private const float DifferentRoomMultiplier = 0.45f;
    private const float DebtDecay = 0.5f;
    private const float LoopInterval = 1f;

    private static readonly Dictionary<int, int> FeastLevels = new();
    private static readonly Dictionary<int, SleepDebtState> SleepDebtStates = new();

    private sealed class SleepDebtState
    {
        public float Value;
        public int LastHunterId;
        public float LastExposureTime;
        public float NextHintTime;
        public float NextHallucinationTime;
        public float NextVisibilityPulseTime;
        public float VisibilityPulseEndTime;
        public bool IsFullyDeprived;
    }
    
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising += ExtendTime;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Dying += OnAnyPlayerDying;
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound += OnWaitingForPlayers;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising -= ExtendTime;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
        Exiled.Events.Handlers.Player.Dying -= OnAnyPlayerDying;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Server.RestartingRound -= OnWaitingForPlayers;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.MaxHumeShield = 300f;
        player.HumeShield = player.MaxHumeShield;
        player.HumeShieldRegenerationMultiplier = 0.7f;
        player.SetScale(new Vector3(0.84f, 0.9f, 0.84f));
        FeastLevels[player.Id] = 0;
        player.AddAbility(new Scp966SleepWaveAbility(player));
        player.AddAbility(new Scp966SpeedAbility(player));

        var spawnRoom = Room.Get(RoomType.LczGlassBox);
        if (spawnRoom != null)
        {
            var offset = new Vector3(0f, 1.5f, 0f);
            player.Position = spawnRoom.Position + spawnRoom.Rotation * offset;
            player.Rotation = spawnRoom.Rotation;
        }

        Timing.RunCoroutine(Coroutine(player));
    }

    private static IEnumerator<float> Coroutine(Player player)
    {
        for (;;)
        {
            if (!IsScp966(player))
            {
                CleanupScp966(player);
                yield break;
            }

            var nearestPrey = UpdateSleepDebtAura(player);
            UpdateVisibilityRules();
            UpdateSelfEffects(player);
            ApplyMovementProfile(player, nearestPrey);
            UpdateRoleHud(player, nearestPrey);
            
            yield return Timing.WaitForSeconds(LoopInterval);
        }
    }

    private void ExtendTime(Exiled.Events.EventArgs.Scp3114.DisguisingEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.Scp966) return;
    
        var level = FeastLevels.GetValueOrDefault(ev.Player.Id) + 1;
        FeastLevels[ev.Player.Id] = Math.Min(4, level);

        ev.Player.Heal(level > 4 ? 90f : 55f);
        ev.Player.HumeShield = Mathf.Min(ev.Player.MaxHumeShield, ev.Player.HumeShield + 90f);
        EffectedInfoTextProvider.Set(ev.Player,
            $"<color=#ff9966>摂食完了。狩猟本能 {FeastLevels[ev.Player.Id] + 1}/5</color>\n" +
            "<size=22>Site-02の補給不足により、肉を得るほど神経波が安定する。</size>",
            4f);

        if (FeastLevels[ev.Player.Id] >= 4)
        {
            AbilityBase.ResetCooldown(ev.Player.Id, typeof(Scp966SleepWaveAbility));
        }

        ev.IsAllowed = false;
        ev.Ragdoll.Destroy();
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (!IsScp966(ev.Attacker) || ev.Player == null)
            return;

        var debt = GetSleepDebt(ev.Player);
        var feastLevel = FeastLevels.GetValueOrDefault(ev.Attacker.Id);
        var debtBonus = Mathf.Clamp(debt / 9f, 0f, 12f);
        var feastBonus = feastLevel * 2.5f;

        ev.Amount = 8f + debtBonus + feastBonus;
        Scp966SpeedAbility.OnAttackedCancelSpeed(ev.Attacker);

        if (debt >= 60f)
        {
            ev.Player.EnableEffect(EffectType.Traumatized, 1, 6f);
            ev.Player.EnableEffect(EffectType.Blurred, 90, 4f);
        }

        AddSleepDebt(ev.Player, ev.Attacker, 8f, true);
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (!IsScp966(ev.Player)) return;

        ev.Attacker?.ShowHitMarker();

        ev.Amount *= 1.12f;
        EffectedInfoTextProvider.Set(ev.Player, "<color=#ff9966>脆い骨に銃撃が響く。長く撃ち合うな。</color>", 1.5f);
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupScp966(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 9 6 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    public static int EmitSleepWave(Player scp966, float debtAmount, float range, bool forcedBurst)
    {
        if (!IsScp966(scp966))
            return 0;

        var hitCount = 0;
        foreach (var target in Player.List.ToList())
        {
            if (!IsValidPrey(target)) continue;
            if (Vector3.Distance(scp966.Position, target.Position) > range) continue;

            var gain = debtAmount * GetRoomWaveMultiplier(scp966, target);
            AddSleepDebt(target, scp966, gain, true);

            target.EnableEffect(EffectType.Exhausted, 1, 8f);
            target.EnableEffect(EffectType.Concussed, 90, 5f);
            target.EnableEffect(EffectType.Deafened, 80, 5f);
            EffectedInfoTextProvider.Set(target, "<color=#ff9966>睡眠阻害波が神経に焼き付く。眠気だけが失われていく。</color>", 4f);
            hitCount++;
        }

        if (forcedBurst && hitCount > 0)
            scp966.CurrentRoom?.RoomLightController?.ServerFlickerLights(0.8f);

        return hitCount;
    }

    private static Player UpdateSleepDebtAura(Player scp966)
    {
        Player nearestPrey = null;
        var nearestDistance = float.MaxValue;

        foreach (var target in Player.List.ToList())
        {
            if (!IsValidPrey(target)) continue;

            var distance = Vector3.Distance(scp966.Position, target.Position);
            if (distance <= WaveRange)
            {
                var gain = PassiveDebtGain * GetRoomWaveMultiplier(scp966, target);
                AddSleepDebt(target, scp966, gain, false);
                ApplySleepDebtEffects(target, scp966);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPrey = target;
                }
            }
            else
            {
                DecaySleepDebt(target.Id);
            }
        }

        return nearestPrey;
    }

    private static void AddSleepDebt(Player target, Player scp966, float amount, bool showThresholdHint)
    {
        if (!IsValidPrey(target) || !IsScp966(scp966)) return;

        if (!SleepDebtStates.TryGetValue(target.Id, out var state))
        {
            state = new SleepDebtState();
            SleepDebtStates[target.Id] = state;
        }

        var previous = state.Value;
        state.Value = Mathf.Clamp(state.Value + Math.Max(0f, amount), 0f, MaxSleepDebt);
        if (state.Value >= MaxSleepDebt)
            state.IsFullyDeprived = true;

        state.LastHunterId = scp966.Id;
        state.LastExposureTime = Time.time;
        UpdateVisibilityPulseState(state);

        if (showThresholdHint)
            ShowDebtThresholdHint(target, previous, state.Value);
    }

    private static void DecaySleepDebt(int playerId)
    {
        if (!SleepDebtStates.TryGetValue(playerId, out var state))
            return;

        if (Time.time - state.LastExposureTime < 2f)
            return;

        if (state.IsFullyDeprived)
            return;

        state.Value = Mathf.Max(0f, state.Value - DebtDecay);
        UpdateVisibilityPulseState(state);
        if (state.Value <= 0.1f)
            SleepDebtStates.Remove(playerId);
    }

    private static void ApplySleepDebtEffects(Player target, Player hunter)
    {
        if (!SleepDebtStates.TryGetValue(target.Id, out var state))
            return;

        if (state.Value >= 20f)
        {
            target.EnableEffect(EffectType.Exhausted, 1, 3f);
            target.EnableEffect(EffectType.Slowness, 8, 2.5f);
        }

        if (state.Value >= 45f)
        {
            target.EnableEffect(EffectType.Concussed, 40, 2.5f);
            target.EnableEffect(EffectType.Blurred, 45, 2.5f);
        }

        if (state.Value >= 70f && Time.time >= state.NextHallucinationTime)
        {
            state.NextHallucinationTime = Time.time + UnityEngine.Random.Range(11f, 18f);
            target.EnableEffect(EffectType.Traumatized, 1, 6f);
            target.EnableEffect(EffectType.Blinded, 35, 1.25f);
            target.CurrentRoom?.RoomLightController?.ServerFlickerLights(0.35f);
            EffectedInfoTextProvider.Set(target,
                "<color=#c50000>背後で針のような歯音が聞こえた気がする。</color>\n" +
                "<size=22>休息は来ない。怒りと幻覚だけが残る。</size>",
                4f);
        }

        if (Time.time >= state.NextHintTime)
        {
            state.NextHintTime = Time.time + 9f;
            EffectedInfoTextProvider.Set(target,
                $"<color=#ff9966>睡眠剥奪: {(int)state.Value}%</color>\n" +
                $"<size=22>{hunter.Nickname}の波が神経を削っている。</size>",
                3f);
        }
    }

    private static void ShowDebtThresholdHint(Player target, float previous, float current)
    {
        if (previous < MaxSleepDebt && current >= MaxSleepDebt)
            EffectedInfoTextProvider.Set(target, "<color=#c50000>輪郭が焼き付いた。もう目を逸らしてもSCP-966が見える。</color>", 5f);
        else if (previous < 20f && current >= 20f)
            EffectedInfoTextProvider.Set(target, "<color=#ff9966>眠気が消えた。体だけが重い。</color>", 3f);
        else if (previous < 45f && current >= 45f)
            EffectedInfoTextProvider.Set(target, "<color=#ff9966>視界が歪む。何かが追っている。</color>", 3f);
        else if (previous < 70f && current >= 70f)
            EffectedInfoTextProvider.Set(target, "<color=#c50000>脳が休息を拒絶している。幻覚が混じり始めた。</color>", 4f);
    }

    private static void UpdateSelfEffects(Player player)
    {
        player.EnableEffect(EffectType.NightVision, 255, 2.2f);
        player.EnableEffect(EffectType.SilentWalk, 1, 2.2f);
        player.DisableEffect(EffectType.Invisible);
    }

    private static bool IsInfraredLeaking(Player player)
        => player != null && player.IsConnected && IsVisibleToAnyViewer(player);

    private static void UpdateVisibilityRules()
    {
        foreach (var viewer in Player.List.ToList())
        {
            if (viewer == null || !viewer.IsConnected)
                continue;

            if (EffectFakeSyncProvider.IsEnabled(viewer, EffectType.Invisible))
            {
                EffectFakeSyncProvider.Refresh(viewer, EffectType.Invisible);
                continue;
            }

            EffectFakeSyncProvider.SetTargetRule(
                viewer,
                EffectType.Invisible,
                target => IsScp966(target) && !CanViewerSeeScp966(viewer, target),
                255,
                0.35f,
                false);
        }
    }

    private static bool CanViewerSeeScp966(Player viewer, Player scp966)
    {
        if (viewer == null || scp966 == null) return false;
        if (!viewer.IsConnected || !scp966.IsConnected) return false;
        if (viewer.Id == scp966.Id) return true;
        if (!viewer.IsAlive) return true;
        if (viewer.GetTeam() == CTeam.SCPs) return true;
        if (HasInfraredLikeSensor(viewer)) return true;

        if (!SleepDebtStates.TryGetValue(viewer.Id, out var state))
            return false;

        if (state.LastHunterId != scp966.Id)
            return false;

        if (state.IsFullyDeprived || state.Value >= MaxSleepDebt)
            return true;

        return state.Value >= IntermittentVisibilityDebt &&
               Time.time <= state.VisibilityPulseEndTime;
    }

    private static bool IsVisibleToAnyViewer(Player scp966)
    {
        foreach (var viewer in Player.List.ToList())
        {
            if (viewer == null || viewer.Id == scp966.Id)
                continue;

            if (CanViewerSeeScp966(viewer, scp966))
                return true;
        }

        return false;
    }

    private static void UpdateVisibilityPulseState(SleepDebtState state)
    {
        if (state == null)
            return;

        if (state.IsFullyDeprived || state.Value >= MaxSleepDebt)
        {
            state.IsFullyDeprived = true;
            state.Value = MaxSleepDebt;
            state.VisibilityPulseEndTime = float.PositiveInfinity;
            return;
        }

        if (state.Value < IntermittentVisibilityDebt)
        {
            state.VisibilityPulseEndTime = 0f;
            if (state.NextVisibilityPulseTime <= 0f)
                state.NextVisibilityPulseTime = Time.time + UnityEngine.Random.Range(6f, 12f);
            return;
        }

        if (state.NextVisibilityPulseTime <= 0f)
            state.NextVisibilityPulseTime = Time.time + UnityEngine.Random.Range(2f, 6f);

        if (Time.time < state.NextVisibilityPulseTime)
            return;

        state.VisibilityPulseEndTime = Time.time + UnityEngine.Random.Range(1.5f, 3.25f);
        state.NextVisibilityPulseTime = state.VisibilityPulseEndTime + UnityEngine.Random.Range(7f, 14f);
    }

    private static bool HasInfraredLikeSensor(Player player)
    {
        if (player == null) return false;
        if (player.IsEffectActive<CustomPlayerEffects.Scp1344>()) return true;

        foreach (var item in player.Items)
        {
            if (!CItem.TryGet(item, out var cItem) || cItem == null)
                continue;

            var key = cItem.UniqueKeyName;
            if (key.Contains("Nvg") ||
                key.Contains("AntiMemeGoggle"))
                return true;
        }
        
        if (player.CurrentItem is Firearm firearm && firearm.HasAttachment(AttachmentName.NightVisionSight))
            return true;

        return false;
    }

    private static void ApplyMovementProfile(Player player, Player nearestPrey)
    {
        if (Scp966SpeedAbility.IsSprinting(player))
            return;

        var feastLevel = FeastLevels.GetValueOrDefault(player.Id);
        player.DisableEffect(EffectType.MovementBoost);
        player.DisableEffect(EffectType.Slowness);

        if (nearestPrey == null)
        {
            player.EnableEffect(EffectType.Slowness, (byte)Math.Max(6, 18 - feastLevel * 3), 2.2f);
            return;
        }

        var debt = GetSleepDebt(nearestPrey);
        var boost = (byte)Mathf.Clamp(feastLevel * 4 + debt / 18f, 0f, 22f);
        if (boost > 0)
            player.EnableEffect(EffectType.MovementBoost, boost, 2.2f);
    }

    private static void UpdateRoleHud(Player player, Player nearestPrey)
    {
        var feast = FeastLevels.GetValueOrDefault(player.Id) + 1;
        var leak = IsInfraredLeaking(player)
            ? "<color=#ff9966>赤外漏洩</color>"
            : "<color=green>不可視</color>";

        var preyText = nearestPrey == null
            ? "獲物: <color=#888888>未捕捉</color>"
            : $"獲物: <color=#ff9966>{nearestPrey.Nickname}</color> {Vector3.Distance(player.Position, nearestPrey.Position):F0}m / 睡眠剥奪 {(int)GetSleepDebt(nearestPrey)}%";

        RoleSpecificTextProvider.Set(player,
            $"{preyText}\n狩猟本能: {feast}/5\n視認状態: {leak}");
    }

    private static float GetRoomWaveMultiplier(Player scp966, Player target)
    {
        if (scp966.CurrentRoom == null || target.CurrentRoom == null)
            return SameRoomMultiplier;

        return scp966.CurrentRoom == target.CurrentRoom
            ? SameRoomMultiplier
            : DifferentRoomMultiplier;
    }

    private static float GetSleepDebt(Player player)
        => player != null && SleepDebtStates.TryGetValue(player.Id, out var state)
            ? state.Value
            : 0f;

    private static bool IsValidPrey(Player player)
        => player != null &&
           player.IsConnected &&
           player.IsAlive &&
           player.GetTeam() != CTeam.SCPs &&
           player.Role.Type != RoleTypeId.Spectator;

    private static bool IsScp966(Player player)
        => player != null &&
           player.IsConnected &&
           player.IsAlive &&
           player.GetCustomRole() == CRoleTypeId.Scp966;

    private static void CleanupScp966(Player player)
    {
        if (player == null) return;

        FeastLevels.Remove(player.Id);
        Scp966SpeedAbility.StopSpeed(player);
        RoleSpecificTextProvider.Clear(player);
        EffectedInfoTextProvider.Clear(player);
        player.DisableEffect(EffectType.Invisible);
        player.DisableEffect(EffectType.NightVision);
        player.DisableEffect(EffectType.SilentWalk);
        player.DisableEffect(EffectType.Slowness);
        player.DisableEffect(EffectType.MovementBoost);
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev?.Player == null) return;

        SleepDebtStates.Remove(ev.Player.Id);
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp966)
            CleanupScp966(ev.Player);
    }

    private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null) return;

        SleepDebtStates.Remove(ev.Player.Id);
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp966)
            CleanupScp966(ev.Player);
    }

    private static void OnAnyPlayerDying(DyingEventArgs ev)
    {
        if (ev?.Player == null) return;

        SleepDebtStates.Remove(ev.Player.Id);
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp966)
            CleanupScp966(ev.Player);
    }

    private static void OnWaitingForPlayers()
    {
        FeastLevels.Clear();
        SleepDebtStates.Clear();
    }
}
