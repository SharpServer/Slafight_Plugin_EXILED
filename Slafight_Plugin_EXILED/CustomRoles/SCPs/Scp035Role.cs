using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Extensions;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp035Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-035";
    protected override string Description { get; set; } = "<size=23><color=red>SCP-035</color>\n" +
                                                          "愚かな博士が仮面をつけて乗っ取れた！\n" +
                                                          "但し、博士がなんとかしようと仮面に抵抗している為精神状態が不安定です。\n" +
                                                          "あなたの最終的な目標は<color=red>施設の破壊</color>です。\n" +
                                                          "精神が安定している時は比較的人間達に友好的に接し、そうでない時は\n" +
                                                          "「触手」を用いて邪魔をさせないようにし、弾頭へと向かいましょう。\n" +
                                                          "<color=yellow>※通常時は博士、発狂時はチュートリアルの見た目になります。\n" +
                                                          "※RP性がとても高いため、不慣れな場合は取り合えず\n" +
                                                          "暴れまくることを推奨します。頑張って！</color></size>";
    protected override float DescriptionDuration { get; set; } = 15f;
    protected override bool DescriptionShowRoleName { get; set; } = false;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp035;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp035";
    protected override RoleTypeId? TeamNpcRoleTypeId { get; set; } = RoleTypeId.Scp0492;
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Tutorial;
    protected override float? SpawnMaxHealth => 2500f;
    protected override bool SpawnClearsInventory => true;
    protected override IReadOnlyList<object> SpawnItems => [ItemType.KeycardScientist, ItemType.Painkillers];
    protected override string SpawnCustomInfo => "<color=#C50000>SCP-035</color>";
    protected override CRoleVoiceSettings VoiceSettings => new(
        routes:
        [
            CRoleVoiceRoute.ToTeams(
                [CTeam.SCPs],
                VoiceRouteDecision.Direct(),
                context => CanSpeakWithScps(context.Sender) &&
                           context.SourceChannel != VoiceChat.VoiceChatChannel.None)
        ]);
    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.SpecialWeaponsDisabled
    ];

    // 全SCP-035共有状態
    private static readonly Dictionary<int, Scp035State> GlobalStates = new();

    // 完全覚醒などで「自動遷移させない」プレイヤー
    private static readonly HashSet<int> FrozenPlayers = [];

    public struct Scp035State
    {
        public Scp035StateType NowState;
        public float ChangeStateTimeAwaiting;
    }

    // インスタンス側の状態辞書（互換保持用）
    private readonly Dictionary<int, Scp035State> _states = new();

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Handcuffing += OnHandcuffing;
        Exiled.Events.Handlers.Player.Dying += OnDyingByRole;
        Exiled.Events.Handlers.Warhead.Starting += OnWarheadStartingGlobal;
        OmegaWarhead.OmegaWarheadStarting += OnOmegaWarheadStartingGlobal;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Handcuffing -= OnHandcuffing;
        Exiled.Events.Handlers.Player.Dying -= OnDyingByRole;
        Exiled.Events.Handlers.Warhead.Starting -= OnWarheadStartingGlobal;
        OmegaWarhead.OmegaWarheadStarting -= OnOmegaWarheadStartingGlobal;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.ChangeAppearance(RoleTypeId.Scientist);
        player.MaxArtificialHealth = 500f;
        player.ArtificialHealth = 500f;

        TryChangeState(player, new Scp035State
        {
            NowState = Scp035StateType.Stable,
            ChangeStateTimeAwaiting = 180f,
        });

        FrozenPlayers.Remove(player.Id);

        TrySetPlayerPosition(player, Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 0.65f), nameof(Scp035Role));

        player.TryWear("SCP035", player.Transform, out var schematicObject, (Vector3.forward * 0.205f)+(Vector3.up*0.6f));
        schematicObject.Scale *= 1.185f;
        LabApi.Features.Wrappers.Player.Get(player.NetId)!.DestroySchematic(schematicObject);

        Timing.RunCoroutine(Coroutine(player));
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        Cleanup(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 0 3 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    private void OnHandcuffing(HandcuffingEventArgs ev)
    {
        if (!Check(ev.Target)) return;
        ev.IsAllowed = false;
    }

    private void OnDyingByRole(DyingEventArgs ev)
    {
        if (!Check(ev.Attacker)) return;
        ev.Attacker.ArtificialHealth += 35f;
    }

    // Warhead起動 → 全SCP-035を完全覚醒
    private void OnWarheadStartingGlobal(StartingEventArgs ev)
    {
        FullyAwakenAllScp035();
    }

    private void OnOmegaWarheadStartingGlobal(object sender, OmegaWarheadStartingEventArgs ev)
    {
        FullyAwakenAllScp035();
    }

    private void FullyAwakenAllScp035()
    {
        foreach (var pl in Player.List)
        {
            if (!IsScp035(pl)) continue;

            var state = GetState(pl);
            state.NowState = Scp035StateType.FullyAwaken;
            state.ChangeStateTimeAwaiting = 0f;

            TryChangeState(pl, state);
            Trigger(pl, Scp035StateType.FullyAwaken);

            // ここで「このプレイヤーはもう自動遷移させない」フラグON
            FrozenPlayers.Add(pl.Id);
        }
    }

    private bool IsScp035(Player player) =>
        player.IsAlive && player.UniqueRole == UniqueRoleKey;

    public void Cleanup(Player player)
    {
        _states.Remove(player.Id, out _);
        GlobalStates.Remove(player.Id, out _);
        FrozenPlayers.Remove(player.Id);
        RoleSpecificTextProvider.Clear(player);

        if (AbilityBase.HasAbility<Scp035TentacleAbility>(player))
            player.RemoveAbility<Scp035TentacleAbility>();

        CleanupTeamNpc(player);
    }

    public bool TryChangeState(Player player, Scp035StateType newState)
    {
        try
        {
            var s = new Scp035State { NowState = newState };
            return TryChangeState(player, s);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to change {player?.Nickname}'s SCP-035 State. Reason:\n{e}");
            return false;
        }
    }

    public bool TryChangeState(Player player, Scp035State newState)
    {
        try
        {
            _states[player.Id] = newState;
            GlobalStates[player.Id] = newState;
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to change {player?.Nickname}'s SCP-035 State. Reason:\n{e}");
            return false;
        }
    }

    public Scp035State GetState(Player player)
    {
        if (_states.TryGetValue(player.Id, out var local))
            return local;

        if (GlobalStates.TryGetValue(player.Id, out var global))
            return global;

        return default;
    }

    public static bool CanSpeakWithScps(Player player)
    {
        return player != null &&
               GlobalStates.TryGetValue(player.Id, out var state) &&
               state.NowState is Scp035StateType.Awaken or Scp035StateType.FullyAwaken;
    }

    public string GetStateLoc(Scp035StateType stateType)
    {
        return stateType switch
        {
            Scp035StateType.Stable => "<color=green>安定</color>",
            Scp035StateType.Unstable => "<color=yellow>不安定</color>",
            Scp035StateType.Awaken => "<color=red>発狂／覚醒</color>",
            Scp035StateType.FullyAwaken => "<color=red><b>完全覚醒</b></color>",
            _ => "[不明]"
        };
    }

    private static bool Trigger(Player player, Scp035StateType stateType)
    {
        switch (stateType)
        {
            case Scp035StateType.Stable:
                player.DisableAllEffects();
                if (AbilityBase.HasAbility<Scp035TentacleAbility>(player))
                    player.RemoveAbility<Scp035TentacleAbility>();
                player.ChangeAppearance(RoleTypeId.Scientist);
                player.ShowHint($"<color=green>安定</color>状態へと移行しました！\n現在精神は比較的安定しており、人々に危害を与える必要は無いでしょう。\nアビリティ「触手」が無効化されました。\n<color=green>人々と友好的に接しましょう</color>");
                return true;

            case Scp035StateType.Unstable:
                player.EnableEffect(EffectType.Poisoned, 10);
                player.ShowHint($"<color=yellow>不安定</color>状態へと移行しました！\n現在精神は揺れ動いており、常に回復が必要でしょう。\n腐蝕が再開しました。\n<color=yellow>人々に警告を与え、己の生存を心掛けましょう。</color>");
                return true;

            case Scp035StateType.Awaken:
                player.DisableEffect(EffectType.Poisoned);
                if (!AbilityBase.HasAbility<Scp035TentacleAbility>(player))
                    player.AddAbility<Scp035TentacleAbility>();
                player.EnableEffect(EffectType.Invigorated, 20);
                player.EnableEffect(EffectType.BodyshotReduction, 30);
                player.EnableEffect(EffectType.DamageReduction, 30);
                player.ChangeAppearance(RoleTypeId.Tutorial);
                player.ShowHint($"<color=red>発狂／覚醒</color>状態へと移行しました！\n現在精神は支配されており、己の為に全てを犠牲にする必要があるでしょう。\n腐蝕が止まり、アビリティ「触手」が使用可能になりました！\n<color=red>ためらう必要はない。出る事だけを考えるのだ。</color>");
                return true;

            case Scp035StateType.FullyAwaken:
                player.DisableEffect(EffectType.Poisoned);
                if (!AbilityBase.HasAbility<Scp035TentacleAbility>(player))
                    player.AddAbility<Scp035TentacleAbility>();
                player.EnableEffect(EffectType.Invigorated, 30);
                player.EnableEffect(EffectType.BodyshotReduction, 40);
                player.EnableEffect(EffectType.DamageReduction, 40);
                player.EnableEffect(EffectType.MovementBoost, 5);
                player.ChangeAppearance(RoleTypeId.Tutorial);
                player.ShowHint($"<color=red><b>完全覚醒</b></color>状態へと移行しました！\n現在精神は完全に支配されており、もはや受け入れるしかないでしょう！\nアビリティ「触手」が利用可能になりました！");
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(stateType), stateType, null);
        }
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        while (true)
        {
            if (!IsValid(player))
            {
                Cleanup(player);
                yield break;
            }

            var state = GetState(player);

            if (state.NowState is Scp035StateType.Awaken or Scp035StateType.FullyAwaken)
            {
                if (player.IsCuffed)
                {
                    player.RemoveHandcuffs();
                }
            }
            if (state.NowState == Scp035StateType.FullyAwaken)
            {
                // 完全覚醒用コルーチンへ移行
                Timing.RunCoroutine(FullyAwakenCoroutine(player));
                yield break;
            }

            // Frozen は従来どおり
            if (FrozenPlayers.Contains(player.Id))
            {
                RoleSpecificTextProvider.Set(player,
                    $"状態：{GetStateLoc(state.NowState)}\n変化まで：<color=red><b>抵抗不可能</b></color>");
                TryChangeState(player, state);
                yield return Timing.WaitForSeconds(0.1f);
                continue;
            }

            var value = (int)state.ChangeStateTimeAwaiting;
            if (value < 0f)
                value = -1;

            RoleSpecificTextProvider.Set(player,
                value > 0f
                    ? $"状態：{GetStateLoc(state.NowState)}\n変化まで：{(int)state.ChangeStateTimeAwaiting}"
                    : $"状態：{GetStateLoc(state.NowState)}\n変化まで：<color=red><b>抵抗不可能</b></color>");

            state.ChangeStateTimeAwaiting -= 0.1f;

            if (state.ChangeStateTimeAwaiting <= 0)
            {
                state.NowState = state.NowState switch
                {
                    Scp035StateType.Stable => Scp035StateType.Unstable,
                    Scp035StateType.Unstable => Scp035StateType.Awaken,
                    Scp035StateType.Awaken => Scp035StateType.Stable,
                    Scp035StateType.FullyAwaken => Scp035StateType.FullyAwaken,
                    _ => state.NowState
                };
                Trigger(player, state.NowState);
                if (state.NowState != Scp035StateType.FullyAwaken)
                {
                    state.ChangeStateTimeAwaiting = 180f;
                }
            }

            if (!player.IsEffectActive<CustomPlayerEffects.Poisoned>() &&
                state.NowState == Scp035StateType.Unstable)
                player.EnableEffect(EffectType.Poisoned, 10);

            TryChangeState(player, state);
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private IEnumerator<float> FullyAwakenCoroutine(Player player)
    {
        while (true)
        {
            if (!IsValid(player))
            {
                Cleanup(player);
                yield break;
            }

            var state = GetState(player);
            
            if (player.IsCuffed)
            {
                player.RemoveHandcuffs();
            }

            // 他から勝手に状態を変えられても、ここで強制上書きする
            if (state.NowState != Scp035StateType.FullyAwaken)
            {
                state.NowState = Scp035StateType.FullyAwaken;
                state.ChangeStateTimeAwaiting = 0f;
                TryChangeState(player, state);
                Trigger(player, Scp035StateType.FullyAwaken);
            }

            RoleSpecificTextProvider.Set(player,
                $"状態：{GetStateLoc(state.NowState)}\n変化まで：<color=red><b>抵抗不可能</b></color>");

            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private bool IsValid(Player player) =>
        player.IsAlive &&
        player.GetCustomRole() == CRoleTypeId.Scp035 &&
        Round.InProgress;

    // プラグイン終了時用
    public static void CleanupAllScp035()
    {
        GlobalStates.Clear();
        FrozenPlayers.Clear();
    }
}
