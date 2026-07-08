using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class ModeratorUtil : CItemWeapon
{
    public override string DisplayName => "Moderator Util";
    public override string Description =>
        "管理者用のモデレーションガン。\n" +
        "Tキーで機能切替、Iキーで詳細指定、対象を撃つと実行。";

    protected override string UniqueKey => "ModUtil";
    protected override ItemType BaseItem => ItemType.GunCOM18;
    protected override float Damage => 0f;
    protected override byte MagazineSize => 18;
    protected override Vector3 Scale => Vector3.zero;
    protected override bool AllowAttachmentChanges => false;

    public enum ModUtilType
    {
        Inspect,
        Warn,
        Kick,
        Ban,
        Kill,
        Teleport,
        Inventory,
        Restrain,
        Voice,
        Role,
        Privilege,
        Heal,
    }

    private struct ModUtilStats
    {
        public ModUtilType SelectedUtilType;
        public int OptionIndex;
    }

    private static readonly ModUtilType[] OrderedTypes = Enum.GetValues(typeof(ModUtilType)).Cast<ModUtilType>().ToArray();
    private static readonly string[] BanOptionNames =
    [
        "10分",
        "1時間",
        "6時間",
        "12時間",
        "1日",
        "3日",
        "7日（1週間）",
        "14日（2週間）",
        "28日（4週間）",
        "90日",
        "180日",
        "1年",
        "3年",
        "無期限（IP BAN）",
    ];
    private static readonly Dictionary<Player, ModUtilStats> StatsMap = [];
    private static readonly Dictionary<Player, CoroutineHandle> HintLoopHandles = [];
    private static readonly Dictionary<Player, float> NextActionTimes = [];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        Exiled.Events.Handlers.Item.InspectingItem += OnInspecting;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Exiled.Events.Handlers.Item.InspectingItem -= OnInspecting;
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        foreach (var h in HintLoopHandles.Values) Timing.KillCoroutines(h);
        HintLoopHandles.Clear();
        StatsMap.Clear();
        NextActionTimes.Clear();
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;
        if (HintLoopHandles.TryGetValue(ev.Player, out var h)) Timing.KillCoroutines(h);
        HintLoopHandles.Remove(ev.Player);
        StatsMap.Remove(ev.Player);
        NextActionTimes.Remove(ev.Player);
    }

    protected override void OnPickingUp(PickingUpItemEventArgs ev)
    {
        if (CanUse(ev.Player)) return;
        ev.IsAllowed = false;
        ev.Player.ShowHint("<size=22><color=red>Moderator Util は管理者専用です。</color></size>", 3f);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        EnsureStats(ev.Player);
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnSelectedHintFinished(Player player)
    {
        if (!CheckHeld(player)) return;
        EnsureStats(player);

        if (HintLoopHandles.TryGetValue(player, out var running) && running.IsRunning) return;
        HintLoopHandles[player] = Timing.RunCoroutine(HintLoopCoroutine(player));
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        ev.IsAllowed = false;
        if (!CanUse(ev.Player)) return;

        var stats = EnsureStats(ev.Player);
        var index = Array.IndexOf(OrderedTypes, stats.SelectedUtilType);
        stats.SelectedUtilType = OrderedTypes[(index + 1) % OrderedTypes.Length];
        stats.OptionIndex = 0;
        StatsMap[ev.Player] = stats;

        ShowCurrentSelection(ev.Player);
    }

    private void OnInspecting(InspectingItemEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        if (!CanUse(ev.Player)) return;

        var stats = EnsureStats(ev.Player);
        stats.OptionIndex = (stats.OptionIndex + 1) % GetOptionCount(stats.SelectedUtilType);
        StatsMap[ev.Player] = stats;

        ShowCurrentSelection(ev.Player);
        ev.IsAllowed = false;
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (!CheckHeld(ev.Attacker)) return;
        ev.IsAllowed = false;
        ev.Amount = 0f;

        if (!CanUse(ev.Attacker))
        {
            ev.Attacker.ShowHint("<size=22><color=red>Moderator Util を使用する権限がありません。</color></size>", 3f);
            return;
        }

        if (ev.Attacker == null || ev.Player == null)
            return;

        if (NextActionTimes.TryGetValue(ev.Attacker, out var next) && Time.time < next)
            return;
        
        ev.Attacker.ShowHitMarker();

        NextActionTimes[ev.Attacker] = Time.time + 0.6f;
        var stats = EnsureStats(ev.Attacker);
        Execute(ev.Attacker, ev.Player, stats);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        base.OnShot(ev);
        ev.Firearm.MagazineAmmo = ev.Firearm.MaxMagazineAmmo;
    }

    private static ModUtilStats EnsureStats(Player player)
    {
        if (StatsMap.TryGetValue(player, out var stats)) return stats;

        stats = new ModUtilStats
        {
            SelectedUtilType = ModUtilType.Inspect,
            OptionIndex = 0,
        };
        StatsMap[player] = stats;
        return stats;
    }

    private static bool CanUse(Player? player)
    {
        if (player == null) return false;
        return player.RemoteAdminAccess ||
               player.GetCustomRole() is CRoleTypeId.ModeratorRole or CRoleTypeId.HideAdmin;
    }

    private static void Execute(Player actor, Player target, ModUtilStats stats)
    {
        var option = GetOptionName(stats.SelectedUtilType, stats.OptionIndex);
        var result = stats.SelectedUtilType switch
        {
            ModUtilType.Inspect => InspectTarget(actor, target, stats.OptionIndex),
            ModUtilType.Warn => WarnTarget(actor, target, stats.OptionIndex),
            ModUtilType.Kick => KickTarget(actor, target, stats.OptionIndex),
            ModUtilType.Ban => BanTarget(actor, target, stats.OptionIndex),
            ModUtilType.Kill => KillTarget(actor, target),
            ModUtilType.Teleport => TeleportTarget(actor, target, stats.OptionIndex),
            ModUtilType.Inventory => InventoryAction(actor, target, stats.OptionIndex),
            ModUtilType.Restrain => RestrainAction(actor, target, stats.OptionIndex),
            ModUtilType.Voice => VoiceAction(actor, target, stats.OptionIndex),
            ModUtilType.Role => RoleAction(actor, target, stats.OptionIndex),
            ModUtilType.Privilege => PrivilegeAction(actor, target, stats.OptionIndex),
            ModUtilType.Heal => HealAction(actor, target, stats.OptionIndex),
            _ => "未実装の機能です。",
        };

        actor.ShowHint($"<size=22><color=#ff8bd6>[Moderator Util]</color> {GetTranslatedText(stats.SelectedUtilType)} / {option}\n" +
            $"{result}</size>",
            4f);
    }

    private static string InspectTarget(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                return $"{target.Nickname} | ID:{target.Id} | Role:{target.Role.Type} | CRole:{target.GetCustomRole()}\n" +
                       $"HP:{target.Health:0}/{target.MaxHealth:0} | UserId:{target.UserId}";
            case 1:
                var items = target.Items.Select(i => i.Type.ToString()).DefaultIfEmpty("None");
                return $"{target.Nickname} Inventory:\n{string.Join(", ", items)}";
            default:
                var pos = target.Position;
                return $"{target.Nickname} Pos: {pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}\n" +
                       $"God:{target.IsGodModeEnabled} Bypass:{target.IsBypassModeEnabled} NoclipPerm:{target.IsNoclipPermitted}";
        }
    }

    private static string WarnTarget(Player actor, Player target, int option)
    {
        var message = option switch
        {
            0 => "ルール違反の疑いがあります。直ちに行動を改めてください。",
            1 => "不適切な攻撃またはRDMの疑いがあります。戦闘を停止してください。",
            2 => "ボイスチャットの迷惑行為を停止してください。",
            _ => "管理者の指示に従ってください。",
        };

        target.Broadcast(6, $"<color=red><b>Moderator Warning</b></color>\n{message}");

        ModerationBridge.Send("warn", new
        {
            actor = actor.Nickname,
            actorId = actor.UserId,
            target = target.Nickname,
            targetId = target.UserId,
            message,
        });

        return $"{target.Nickname} に警告を送信しました。";
    }

    private static string KickTarget(Player actor, Player target, int option)
    {
        var reason = option switch
        {
            0 => "Moderator action",
            1 => "Rule violation",
            2 => "Mic spam",
            _ => "AFK / No response",
        };

        // Discord通知は Exiled の Kicked イベント経由 (ModerationEventsHandler) でグローバルに送信される。
        target.Kick(reason, actor);

        return $"{target.Nickname} を Kick しました。理由: {reason}";
    }

    private static string BanTarget(Player actor, Player target, int option)
    {
        var duration = option switch
        {
            0 => TimeSpan.FromMinutes(10),
            1 => TimeSpan.FromHours(1),
            2 => TimeSpan.FromHours(6),
            3 => TimeSpan.FromHours(12),
            4 => TimeSpan.FromDays(1),
            5 => TimeSpan.FromDays(3),
            6 => TimeSpan.FromDays(7),
            7 => TimeSpan.FromDays(14),
            8 => TimeSpan.FromDays(28),
            9 => TimeSpan.FromDays(90),
            10 => TimeSpan.FromDays(180),
            11 => TimeSpan.FromDays(365),
            12 => TimeSpan.FromDays(365 * 3),
            _ => TimeSpan.Zero,
        };

        var label = Pick(option, BanOptionNames);
        if (option == BanOptionNames.Length - 1)
            return PermanentlyIpBanTarget(actor, target);

        // Discord通知は Exiled の Banned イベント経由 (ModerationEventsHandler) でグローバルに送信される。
        target.Ban(duration, $"Moderator action ({label})", actor);

        return $"{target.Nickname} を {label} Ban しました。";
    }

    private static string PermanentlyIpBanTarget(Player actor, Player target)
    {
        if (string.IsNullOrWhiteSpace(target.IPAddress) ||
            target.IPAddress.Equals("localClient", StringComparison.OrdinalIgnoreCase))
            return $"{target.Nickname} の有効なIPアドレスを取得できませんでした。";

        const string reason = "Moderator action (Permanent IP BAN)";
        var issued = BanHandler.IssueBan(new BanDetails
        {
            OriginalName = BanPlayer.ValidateNick(target.Nickname),
            Id = target.IPAddress,
            IssuanceTime = TimeBehaviour.CurrentTimestamp(),
            Expires = DateTime.MaxValue.Ticks,
            Reason = reason,
            Issuer = actor.Sender.LogName,
        }, BanHandler.BanType.IP);

        if (!issued)
            return $"{target.Nickname} の無期限IP BAN登録に失敗しました。";

        // BanHandler.IssueBan は BanPlayer.BanUser を経由しないため Banned イベントが発火しない。
        // ここで直接通知する（直後の Kick() 呼び出しで Kicked イベント経由の通知も別途飛ぶが許容する）。
        target.Kick(reason, actor);

        ModerationBridge.Send("ban", new
        {
            actor = actor.Nickname,
            actorId = actor.UserId,
            target = target.Nickname,
            targetId = target.UserId,
            duration = "無期限（IP BAN）",
        });

        return $"{target.Nickname} を無期限IP BANしました。";
    }

    private static string KillTarget(Player actor, Player target)
    {
        target.Kill("Moderator action");
        return $"{target.Nickname} を Kill しました。";
    }

    private static string TeleportTarget(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                target.Position = actor.Position + actor.CameraTransform.forward * 1.5f;
                return $"{target.Nickname} を自分の前へ Bring しました。";
            case 1:
                actor.Position = target.Position + Vector3.up * 0.2f;
                return $"{target.Nickname} へ移動しました。";
            default:
                var actorPos = actor.Position;
                actor.Position = target.Position + Vector3.up * 0.2f;
                target.Position = actorPos + Vector3.up * 0.2f;
                return $"{target.Nickname} と位置を交換しました。";
        }
    }

    private static string InventoryAction(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                target.ClearInventory();
                return $"{target.Nickname} のインベントリを消去しました。";
            case 1:
                target.DropItems();
                return $"{target.Nickname} のアイテムをドロップさせました。";
            case 2:
                target.AddItem(ItemType.Medkit);
                return $"{target.Nickname} に Medkit を付与しました。";
            default:
                target.AddItem(ItemType.Radio);
                return $"{target.Nickname} に Radio を付与しました。";
        }
    }

    private static string RestrainAction(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                if (target.IsCuffed)
                {
                    target.RemoveHandcuffs();
                    return $"{target.Nickname} の拘束を解除しました。";
                }

                target.Handcuff(actor);
                return $"{target.Nickname} を拘束しました。";
            case 1:
                target.Handcuff(actor);
                return $"{target.Nickname} を拘束しました。";
            default:
                target.RemoveHandcuffs();
                return $"{target.Nickname} の拘束を解除しました。";
        }
    }

    private static string VoiceAction(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                target.IsMuted = !target.IsMuted;
                return $"{target.Nickname} VoiceMute={target.IsMuted}";
            case 1:
                target.IsIntercomMuted = !target.IsIntercomMuted;
                return $"{target.Nickname} IntercomMute={target.IsIntercomMuted}";
            case 2:
                target.IsMuted = true;
                target.IsIntercomMuted = true;
                return $"{target.Nickname} の通常VC/インカムをミュートしました。";
            default:
                target.IsMuted = false;
                target.IsIntercomMuted = false;
                return $"{target.Nickname} の通常VC/インカムミュートを解除しました。";
        }
    }

    private static string RoleAction(Player actor, Player target, int option)
    {
        var role = option switch
        {
            0 => RoleTypeId.Spectator,
            1 => RoleTypeId.Tutorial,
            2 => RoleTypeId.ClassD,
            3 => RoleTypeId.Scientist,
            4 => RoleTypeId.FacilityGuard,
            5 => RoleTypeId.NtfPrivate,
            _ => RoleTypeId.ChaosRifleman,
        };

        target.SetRole(role, RoleSpawnFlags.AssignInventory);
        return $"{target.Nickname} を {role} に変更しました。";
    }

    private static string PrivilegeAction(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                target.IsGodModeEnabled = !target.IsGodModeEnabled;
                return $"{target.Nickname} GodMode={target.IsGodModeEnabled}";
            case 1:
                target.IsBypassModeEnabled = !target.IsBypassModeEnabled;
                return $"{target.Nickname} Bypass={target.IsBypassModeEnabled}";
            default:
                target.IsNoclipPermitted = !target.IsNoclipPermitted;
                return $"{target.Nickname} NoclipPermission={target.IsNoclipPermitted}";
        }
    }

    private static string HealAction(Player actor, Player target, int option)
    {
        switch (option)
        {
            case 0:
                target.Health = target.MaxHealth;
                return $"{target.Nickname} を全回復しました。";
            case 1:
                target.Health = Math.Min(target.MaxHealth, target.Health + 50f);
                return $"{target.Nickname} を 50 回復しました。";
            case 2:
                target.MaxHealth = 100f;
                target.Health = 100f;
                return $"{target.Nickname} の最大HPを 100 にしました。";
            default:
                target.MaxHealth = 999f;
                target.Health = 999f;
                return $"{target.Nickname} の最大HPを 999 にしました。";
        }
    }

    private static IEnumerator<float> HintLoopCoroutine(Player player)
    {
        while (true)
        {
            if (Round.IsLobby) yield break;
            if (CItem.Get<ModeratorUtil>()?.CheckHeld(player) != true) yield break;
            if (!StatsMap.TryGetValue(player, out _)) yield break;

            ShowCurrentSelection(player, 1.2f);
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static void ShowCurrentSelection(Player player, float duration = 2.5f)
    {
        if (!StatsMap.TryGetValue(player, out var stats)) return;

            player.ShowHint($"<size=23><color=#ff8bd6><b>Moderator Util</b></color>\n" +
            $"T: {GetTranslatedText(stats.SelectedUtilType)} / I: {GetOptionName(stats.SelectedUtilType, stats.OptionIndex)}\n" +
            $"{GetDescription(stats.SelectedUtilType, stats.OptionIndex)}</size>",
            duration);
    }

    private static int GetOptionCount(ModUtilType utilType)
        => utilType switch
        {
            ModUtilType.Inspect => 3,
            ModUtilType.Warn => 4,
            ModUtilType.Kick => 4,
            ModUtilType.Ban => BanOptionNames.Length,
            ModUtilType.Kill => 1,
            ModUtilType.Teleport => 3,
            ModUtilType.Inventory => 4,
            ModUtilType.Restrain => 3,
            ModUtilType.Voice => 4,
            ModUtilType.Role => 7,
            ModUtilType.Privilege => 3,
            ModUtilType.Heal => 4,
            _ => 1,
        };

    private static string GetTranslatedText(ModUtilType utilType)
        => utilType switch
        {
            ModUtilType.Inspect => "情報確認",
            ModUtilType.Warn => "警告",
            ModUtilType.Kick => "Kick",
            ModUtilType.Ban => "Ban",
            ModUtilType.Kill => "Kill",
            ModUtilType.Teleport => "テレポート",
            ModUtilType.Inventory => "インベントリ",
            ModUtilType.Restrain => "拘束",
            ModUtilType.Voice => "VC制御",
            ModUtilType.Role => "ロール変更",
            ModUtilType.Privilege => "権限フラグ",
            ModUtilType.Heal => "回復/HP",
            _ => utilType.ToString(),
        };

    private static string GetOptionName(ModUtilType utilType, int option)
        => utilType switch
        {
            ModUtilType.Inspect => Pick(option, "概要", "所持品", "位置/状態"),
            ModUtilType.Warn => Pick(option, "一般警告", "RDM警告", "VC警告", "指示警告"),
            ModUtilType.Kick => Pick(option, "一般", "ルール違反", "VC迷惑", "AFK"),
            ModUtilType.Ban => Pick(option, BanOptionNames),
            ModUtilType.Kill => "管理者Kill",
            ModUtilType.Teleport => Pick(option, "Bring", "Goto", "Swap"),
            ModUtilType.Inventory => Pick(option, "全削除", "全ドロップ", "Medkit付与", "Radio付与"),
            ModUtilType.Restrain => Pick(option, "切替", "拘束", "解除"),
            ModUtilType.Voice => Pick(option, "通常VC切替", "インカム切替", "両方Mute", "両方Unmute"),
            ModUtilType.Role => Pick(option, "Spectator", "Tutorial", "ClassD", "Scientist", "FacilityGuard", "NtfPrivate", "ChaosRifleman"),
            ModUtilType.Privilege => Pick(option, "God切替", "Bypass切替", "Noclip許可切替"),
            ModUtilType.Heal => Pick(option, "全回復", "+50", "MaxHP100", "MaxHP999"),
            _ => "None",
        };

    private static string GetDescription(ModUtilType utilType, int option)
        => utilType switch
        {
            ModUtilType.Inspect => "対象を撃つと情報だけを表示する。",
            ModUtilType.Warn => "対象へ警告Broadcastを送る。",
            ModUtilType.Kick => "対象をサーバーから切断する。",
            ModUtilType.Ban => option == BanOptionNames.Length - 1
                ? "対象のIPアドレスを無期限BANし、サーバーから切断する。"
                : "対象を指定時間Banする。",
            ModUtilType.Kill => "対象を管理者処置で死亡させる。",
            ModUtilType.Teleport => "対象または自分の位置を操作する。",
            ModUtilType.Inventory => "対象の所持品を操作する。",
            ModUtilType.Restrain => "対象の拘束状態を操作する。",
            ModUtilType.Voice => "対象のVC/インカムミュートを操作する。",
            ModUtilType.Role => "対象の通常ロールを変更する。",
            ModUtilType.Privilege => "対象のGod/Bypass/Noclip許可を切り替える。",
            ModUtilType.Heal => "対象のHPを調整する。",
            _ => string.Empty,
        };

    private static string Pick(int index, params string[] values)
        => values[Mathf.Clamp(index, 0, values.Length - 1)];
}

