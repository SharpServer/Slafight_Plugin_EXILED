using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Mirror;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp106Role : CRole
{
    public sealed class Scp106HuntSettings
    {
        public float DamageThreshold { get; set; } = 500f;
        public float Duration { get; set; } = 100f;
        public float EscapeDistance { get; set; } = 100f;
        public float TeleportCooldown { get; set; } = 20f;
        public float TeleportVigorCost { get; set; }
        public string ChaseAudioFile { get; set; } = "106Chase.ogg";
        public float ChaseAudioMaxDistance { get; set; } = 1f;
        public float ChaseAudioMinDistance { get; set; } = 0.1f;
    }

    public static Scp106HuntSettings HuntSettings { get; } = new();

    private const byte HuntMovementBoostIntensity = 20;
    private const byte HuntInvigoratedIntensity = 1;

    private sealed class HuntState
    {
        public int OwnerId { get; init; }
        public int TargetId { get; init; }
        public float EndTime { get; init; }
        public CoroutineHandle Coroutine { get; set; }
        public string OwnerAudioPurpose { get; set; }
        public string TargetAudioPurpose { get; set; }
    }

    private static readonly Dictionary<int, Dictionary<int, float>> AccumulatedDamage = [];
    private static readonly Dictionary<int, HuntState> Hunts = [];

    protected override string RoleName { get; set; } = "SCP-106";

    protected override string Description { get; set; } = MapFlags.GetSeason() == SeasonTypeId.April
        ? "若者の叫び声大好き爺。いっぱいPDに送り込もう！\nアビリティで糞まみれの爺街道を創り出せるぞ！\n施設中に糞を垂れ流す奴二号機になりましょう！"
        : "若者の叫び声大好き爺。いっぱいPDに送り込もう！\n" +
          "大きなダメージを与えた人間を追跡し、対象の部屋へ侵食できる。\n" +
          "アビリティで陥没穴を創り出し、人間を引き込め！";
    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp106;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp106";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp106;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-106";

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurt += OnPlayerHurt;
        Exiled.Events.Handlers.Player.ChangingRole += OnAnyPlayerChangingRole;
        Exiled.Events.Handlers.Player.Died += OnAnyPlayerDied;
        Exiled.Events.Handlers.Player.Left += OnAnyPlayerLeft;
        Exiled.Events.Handlers.Server.WaitingForPlayers += CleanupAll;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurt -= OnPlayerHurt;
        Exiled.Events.Handlers.Player.ChangingRole -= OnAnyPlayerChangingRole;
        Exiled.Events.Handlers.Player.Died -= OnAnyPlayerDied;
        Exiled.Events.Handlers.Player.Left -= OnAnyPlayerLeft;
        Exiled.Events.Handlers.Server.WaitingForPlayers -= CleanupAll;
        CleanupAll();
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        CleanupOwner(player.Id, false);

        if (MapFlags.GetSeason() == SeasonTypeId.April)
        {
            player.AddAbility<DropBiggerShitAbility>();
        }
        else
        {
            player.AddAbility<CreateSinkholeAbility>();
        }
    }

    public static bool TryGetHuntTarget(Player owner, out Player target)
    {
        target = null!;
        if (owner == null || !Hunts.TryGetValue(owner.Id, out var hunt))
            return false;

        target = Player.Get(hunt.TargetId);
        return IsValidTarget(target);
    }

    private static void OnPlayerHurt(HurtEventArgs ev)
    {
        if (ev?.Player == null || ev.Attacker == null || ev.Amount <= 0f ||
            float.IsNaN(ev.Amount) || float.IsInfinity(ev.Amount))
            return;

        var owner = ev.Player;
        var attacker = ev.Attacker;
        if (!IsScp106(owner) || !IsValidTarget(attacker) || attacker.GetTeam() == CTeam.SCPs)
            return;

        if (Hunts.ContainsKey(owner.Id))
            return;

        if (!AccumulatedDamage.TryGetValue(owner.Id, out var ownerDamage))
        {
            ownerDamage = [];
            AccumulatedDamage[owner.Id] = ownerDamage;
        }

        var total = ownerDamage.GetValueOrDefault(attacker.Id) + ev.Amount;
        ownerDamage[attacker.Id] = total;

        var settings = HuntSettings;
        if (total >= Mathf.Max(1f, settings.DamageThreshold))
            StartHunt(owner, attacker, settings);
    }

    private static void StartHunt(Player owner, Player target, Scp106HuntSettings settings)
    {
        if (owner == null || target == null || Hunts.ContainsKey(owner.Id))
            return;

        var duration = Mathf.Max(1f, settings.Duration);
        var hunt = new HuntState
        {
            OwnerId = owner.Id,
            TargetId = target.Id,
            EndTime = Time.time + duration,
        };

        Hunts[owner.Id] = hunt;
        AccumulatedDamage.Remove(owner.Id);

        target.EnableEffect(EffectType.AnomalousTarget, duration);
        target.EnableEffect(EffectType.Traumatized, duration);
        owner.EnableEffect(EffectType.MovementBoost, HuntMovementBoostIntensity, duration);
        owner.EnableEffect(EffectType.Invigorated, HuntInvigoratedIntensity, duration);
        if (!owner.AddAbility<Scp106TargetTeleportAbility>(Mathf.Max(0f, settings.TeleportCooldown)))
        {
            AbilityBase.RevokeAbility(owner.Id, typeof(Scp106TargetTeleportAbility));
            Log.Warn($"[Scp106Hunt] Could not add teleport ability to {owner.Nickname}: loadout is full.");
        }

        owner.ShowHint(
            $"<color=#c50000><b>{target.Nickname}</b>を追跡対象に指定しました。</color>\n" +
            "対象のいる部屋へテレポートする能力が使用可能です。",
            5f);
        target.ShowHint(
            "<color=#c50000><b>SCP-106に追跡されています。</b></color>\n" +
            $"距離を{Mathf.Max(1f, settings.EscapeDistance):F0}m以上離すことで追跡を解除できます。",
            5f);

        StartChaseAudio(owner, target, hunt, settings);
        hunt.Coroutine = Timing.RunCoroutine(HuntCoroutine(hunt));
    }

    private static IEnumerator<float> HuntCoroutine(HuntState hunt)
    {
        while (Hunts.TryGetValue(hunt.OwnerId, out var current) && ReferenceEquals(current, hunt))
        {
            var owner = Player.Get(hunt.OwnerId);
            var target = Player.Get(hunt.TargetId);
            if (!IsScp106(owner) || !IsValidTarget(target))
            {
                EndHunt(hunt.OwnerId, "対象を見失いました。");
                yield break;
            }

            var settings = HuntSettings;
            var distance = Vector3.Distance(owner.Position, target.Position);
            var remaining = Mathf.Max(0f, hunt.EndTime - Time.time);

            RoleSpecificTextProvider.Set(
                owner,
                $"Target: {target.Nickname}\nDistance: {distance:F1}m / Time: {remaining:F0}s");
            EffectedInfoTextProvider.Set(
                target,
                $"<color=#c50000>SCP-106に追跡されています</color>  距離: {distance:F1}m",
                1.25f);

            if (distance >= Mathf.Max(1f, settings.EscapeDistance))
            {
                EndHunt(hunt.OwnerId, "対象が追跡圏外まで離れました。");
                yield break;
            }

            if (Time.time >= hunt.EndTime)
            {
                EndHunt(hunt.OwnerId, "追跡可能時間が終了しました。");
                yield break;
            }

            yield return Timing.WaitForSeconds(0.5f);
        }
    }

    private static void StartChaseAudio(
        Player owner,
        Player target,
        HuntState hunt,
        Scp106HuntSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ChaseAudioFile))
            return;

        try
        {
            var key = $"Scp106Hunt_{owner.Id}_{target.Id}";
            var maxDistance = Mathf.Max(1f, settings.ChaseAudioMaxDistance);
            var minDistance = Mathf.Clamp(settings.ChaseAudioMinDistance, 1f, maxDistance);

            hunt.OwnerAudioPurpose = $"{key}_Owner";
            hunt.TargetAudioPurpose = $"{key}_Target";
            PlayerSpeakerManager.PlayLoop(
                owner,
                settings.ChaseAudioFile,
                hunt.OwnerAudioPurpose,
                maxDistance: maxDistance,
                minDistance: minDistance,
                listeners: listener => IsChaseAudioListener(listener, owner.Id));
            PlayerSpeakerManager.PlayLoop(
                target,
                settings.ChaseAudioFile,
                hunt.TargetAudioPurpose,
                maxDistance: maxDistance,
                minDistance: minDistance,
                listeners: listener => IsChaseAudioListener(listener, target.Id));
        }
        catch (Exception ex)
        {
            Log.Warn($"[Scp106Hunt] Chase audio could not be started: {ex.Message}");
        }
    }

    private static bool IsChaseAudioListener(Player listener, int playerId)
        => listener?.ReferenceHub != null &&
           (listener.Id == playerId || listener.IsDead);

    private static void EndHunt(int ownerId, string reason, bool showHints = true)
    {
        if (!Hunts.TryGetValue(ownerId, out var hunt))
            return;

        Hunts.Remove(ownerId);
        Timing.KillCoroutines(hunt.Coroutine);

        if (!string.IsNullOrWhiteSpace(hunt.OwnerAudioPurpose))
            PlayerSpeakerManager.Stop(hunt.OwnerId, hunt.OwnerAudioPurpose);
        if (!string.IsNullOrWhiteSpace(hunt.TargetAudioPurpose))
            PlayerSpeakerManager.Stop(hunt.TargetId, hunt.TargetAudioPurpose);

        var owner = Player.Get(hunt.OwnerId);
        var target = Player.Get(hunt.TargetId);

        if (owner != null)
        {
            owner.RemoveAbility<Scp106TargetTeleportAbility>();
            owner.DisableEffect(EffectType.MovementBoost);
            owner.DisableEffect(EffectType.Invigorated);
            RoleSpecificTextProvider.Clear(owner);
            if (showHints)
                owner.ShowHint($"<color=#aaaaaa>{reason}</color>", 4f);
        }

        if (target != null)
        {
            target.DisableEffect(EffectType.AnomalousTarget);
            target.DisableEffect(EffectType.Traumatized);
            EffectedInfoTextProvider.Clear(target);
            if (showHints)
                target.ShowHint("<color=#aaaaaa>SCP-106の追跡から逃れました。</color>", 4f);
        }
    }

    private static void OnAnyPlayerChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        ResetAccumulatedDamageForAttacker(ev.Player.Id);

        if (Hunts.ContainsKey(ev.Player.Id))
            CleanupOwner(ev.Player.Id);

        foreach (var hunt in new List<HuntState>(Hunts.Values))
        {
            if (hunt.TargetId == ev.Player.Id)
                EndHunt(hunt.OwnerId, "対象のロールが変更されました。");
        }
    }

    private static void OnAnyPlayerDied(DiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        if (Hunts.ContainsKey(ev.Player.Id))
            CleanupOwner(ev.Player.Id);

        foreach (var hunt in new List<HuntState>(Hunts.Values))
        {
            if (hunt.TargetId == ev.Player.Id)
                EndHunt(hunt.OwnerId, "対象が死亡しました。");
        }
    }

    private static void OnAnyPlayerLeft(LeftEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        ResetAccumulatedDamageForAttacker(ev.Player.Id);
        if (Hunts.ContainsKey(ev.Player.Id))
            CleanupOwner(ev.Player.Id, false);

        foreach (var hunt in new List<HuntState>(Hunts.Values))
        {
            if (hunt.TargetId == ev.Player.Id)
                EndHunt(hunt.OwnerId, "対象が切断しました。");
        }
    }

    private static void ResetAccumulatedDamageForAttacker(int attackerId)
    {
        foreach (var damage in AccumulatedDamage.Values)
            damage.Remove(attackerId);
    }

    private static void CleanupOwner(int ownerId, bool showHints = true)
    {
        EndHunt(ownerId, "追跡を終了しました。", showHints);
        AccumulatedDamage.Remove(ownerId);
    }

    private static void CleanupAll()
    {
        foreach (var ownerId in new List<int>(Hunts.Keys))
            EndHunt(ownerId, "追跡を終了しました。", false);

        Hunts.Clear();
        AccumulatedDamage.Clear();
    }

    private static bool IsScp106(Player player)
        => player?.ReferenceHub != null &&
           player.IsAlive &&
           (player.GetCustomRole() == CRoleTypeId.Scp106 ||
            player.GetCustomRole() == CRoleTypeId.None && player.Role.Type == RoleTypeId.Scp106);

    private static bool IsValidTarget(Player player)
        => player?.ReferenceHub != null &&
           player.IsConnected &&
           player.IsAlive &&
           player.Role.Type is not RoleTypeId.Spectator and not RoleTypeId.None and not RoleTypeId.Destroyed;

    private void CreateSmallSinkhole(Player player)
    {
        try
        {
            var position = player.Position + new Vector3(0f,-0.5f,0f);
        
            // SinkholeのPrefabIdを指定（実際のIDは要確認）
            var sinkholePrefabId = PrefabType.Sinkhole;
        
            // PrefabHelperでスポーン
            var sinkhole = PrefabHelper.Spawn(sinkholePrefabId, position, Quaternion.identity);
            
            // 必要に応じてNetwork同期
            NetworkServer.Spawn(sinkhole);
        
            // 10秒後に削除
            Timing.CallDelayed(5f, () => UnityEngine.Object.Destroy(sinkhole));
        }
        catch (System.Exception ex)
        {
            Log.Error($"Sinkhole Prefabスポーン失敗: {ex.Message}");
        }
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupOwner(ev.Player.Id, false);
        CassieHelper.AnnounceTermination(ev, "SCP 1 0 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupOwner(ev.Player.Id, false);
        base.OnRoleChanging(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupOwner(ev.Player.Id, false);
        base.OnRoleLeft(ev);
    }
}
