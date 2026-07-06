using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Commands.DevTools;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using Log = Exiled.API.Features.Log;
using Player = Exiled.API.Features.Player;
using PlayerHandler = Exiled.Events.Handlers.Player;
using Room = Exiled.API.Features.Room;
using ServerHandler = Exiled.Events.Handlers.Server;
using Slafight_Plugin_EXILED.API.Interface;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class EventHandler : IBootstrapHandler, IDisposable
{
    public static EventHandler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }
    private bool _disposed;

    public EventHandler()
    {
        PlayerHandler.Verified += OnVerified;
        PlayerHandler.Left += OnLeft;
        ServerHandler.RestartingRound += OnRoundRestarted;
        ServerHandler.RoundStarted += OnRoundStarted;
        ServerHandler.ReloadedPlugins += OnPluginLoad;

        PlayerHandler.ChangingRole += OnChangingRole;
        PlayerHandler.InteractingDoor += DoorGet;
        PlayerHandler.UsedItem += OnUsed;
        PlayerHandler.Hurting += OnHurting;
        PlayerHandler.Dying += OnDying;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        PlayerHandler.Verified -= OnVerified;
        PlayerHandler.Left -= OnLeft;
        ServerHandler.RestartingRound -= OnRoundRestarted;
        ServerHandler.RoundStarted -= OnRoundStarted;
        ServerHandler.ReloadedPlugins -= OnPluginLoad;

        PlayerHandler.ChangingRole -= OnChangingRole;
        PlayerHandler.InteractingDoor -= DoorGet;
        PlayerHandler.UsedItem -= OnUsed;
        PlayerHandler.Hurting -= OnHurting;
        PlayerHandler.Dying -= OnDying;
        GC.SuppressFinalize(this);
    }

    private bool _pluginLoaded = false;

    private static bool IsPlayerValid(Player? p)
    {
        try
        {
            return p?.ReferenceHub != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanReceiveClientUi(Player? p)
    {
        try
        {
            return IsPlayerValid(p) && p.IsConnected && !p.IsNPC;
        }
        catch
        {
            return false;
        }
    }

    private void OnPluginLoad()
    {
        Log.Info("OnPluginLoad is successfully called!");
        if (!_pluginLoaded)
        {
            _pluginLoaded = true;
        }
    }

    private static void OnVerified(VerifiedEventArgs? ev)
    {
        var player = ev?.Player;
        if (player == null) return;

        player.InitPlayerFlags();
        player.Broadcast(
            6,
            "\n<size=28><color=#008cff>シャープ鯖</color>へようこそ！\\n本サーバーはRP鯖です。RPを念頭に置いておく以外の制約は無いので自由に楽しんでください！</size>",
            Broadcast.BroadcastFlags.Normal,
            true);

        if (Round.InProgress) return;
    }

    private static void OnLeft(LeftEventArgs? ev)
    {
        var leaving = ev?.Player;
        if (leaving == null) return;

        DebugModeHandler.RemovePlayer(leaving);
        ServerSpecificsHandler.RemovePlayer(leaving);
        RPNameSetter.Clear(leaving);
        EffectedInfoTextProvider.Clear(leaving);
        EffectFakeSyncProvider.RemovePlayer(leaving);
        PlayerVisibilitySyncProvider.RemoveViewer(leaving);
        RoleSpecificTextProvider.Clear(leaving);
        SpecificFlagsManager.Clear(leaving);
        SpawnObjectPrefab.CleanupPlayer(leaving);
        HitboxCommand.CleanupPlayer(leaving);

        if (leaving.GetTeam() != CTeam.SCPs) return;
        if (leaving.IsVanillaOrCustom(RoleTypeId.Scp0492, CRoleTypeId.Zombified)) return;
        if (Round.ElapsedTime.TotalSeconds > 179) return;

        int scpAlive = Player.List.Count(p => p != null && p != leaving && p.IsAlive && p.GetTeam() == CTeam.SCPs);
        if (scpAlive >= 1) return;

        var candidate = Player.List.FirstOrDefault(p => p != null && p.IsConnected && !p.IsAlive);
        if (candidate == null) return;

        var roleInfo = leaving.GetRoleInfo();
        if (roleInfo.Custom == CRoleTypeId.None)
            candidate.SetRole(roleInfo.Vanilla);
        else
            candidate.SetRole(roleInfo.Custom);

        candidate.ShowHint("※SCPプレイヤーが切断したため代わりにスポーンしました");
    }

    private static void ClearLobbyInfoHint(Player player)
    {
        try
        {
            var display = player.GetPlayerDisplay();
            var oldHint = display.GetHint(HudConstId.LobbyInfo);
            if (oldHint != null)
                display.RemoveHint(oldHint);
        }
        catch (Exception ex)
        {
            Log.Debug($"[LobbyInfoHint] clear failed for {player.Nickname}: {ex.Message}");
        }
    }

    private void OnRoundRestarted()
    {
        EffectFakeSyncProvider.DisableAll();
        EffectedInfoTextProvider.ClearAll();

        Timing.CallDelayed(0.1f, () =>
        {
            RoundHazardController.ResetRoundState();
        });
    }

    private static void OnRoundStarted()
    {
        EffectFakeSyncProvider.DisableAll();
        PlayerVisibilitySyncProvider.ClearAll();
        EffectedInfoTextProvider.ClearAll();
        SpecificFlagsManager.ClearAll();
        foreach (var player in Player.List.ToList().Where(IsPlayerValid))
        {
            if (CanReceiveClientUi(player))
            {
                ClearLobbyInfoHint(player);
                player.ShowHint("");
            }

            player.InitPlayerFlags();
        }

        Timing.CallDelayed(1f, () =>
        {
            foreach (var pickup in Pickup.List)
            {
                if (pickup == null) return;
                pickup.UnSpawn();
                Timing.CallDelayed(0.1f, () => pickup.Spawn());
            }
            List<SpecialEventType> notallowed =
            [
                SpecialEventType.OperationBlackout,
                SpecialEventType.Scp1509BattleField,
                SpecialEventType.FacilityTermination,
                SpecialEventType.SergeyMakarovReturns,
                SpecialEventType.DanteBattle
            ];
            if (!notallowed.Contains(SpecialEventsHandler.Instance.NowEvent))
            {
                if (SpecialEventsHandler.Instance.NowEvent == SpecialEventType.OmegaWarhead)
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "Emergency , emergency , A large containment breach is currently started within the site. All personnel must immediately begin evacuation .",
                        "緊急、緊急、現在大規模な収容違反がサイト内で発生しています。全職員は警備隊の指示に従い、避難を開始してください。", true);
                }
                else
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "Attention, All personnel . Detected containment breach is currently started within the site. All personnel must immediately begin evacuation .",
                        "全職員へ通達。収容違反の発生を確認しました。全職員は警備隊の指示に従い、避難を開始してください。", true);
                }
                foreach (var room in Room.List)
                {
                    room.RoomLightController.ServerFlickerLights(3f);
                }
            }

            Timing.CallDelayed(5f, () =>
            {
                if (Round.IsLobby) return;

                foreach (var door in Door.List)
                {
                    if (door.Type != DoorType.Scp173Gate) continue;
                    door.Unlock();
                    door.IsOpen = true;
                }
                foreach (var door in Door.List)
                {
                    List<SpecialEventType> a = [
                        SpecialEventType.NuclearAttack,
                        SpecialEventType.SnowWarriorsAttack,
                        SpecialEventType.CandyWarriorsAttack,
                        SpecialEventType.FacilityTermination,
                        SpecialEventType.SergeyMakarovReturns
                    ];
                    if (a.Contains(SpecialEventsHandler.Instance.NowEvent)) break;
                    if (door.Type is DoorType.GateA or DoorType.GateB)
                    {
                        door.Lock(120f, DoorLockType.AdminCommand);
                    }
                }
            });
        });
    }
    
    private readonly List<CandyKindID> _candies =
    [
        CandyKindID.Black,
        CandyKindID.Brown,
        CandyKindID.Gray,
        CandyKindID.Orange,
        CandyKindID.White,
        CandyKindID.Evil,
        CandyKindID.Red,
        CandyKindID.Blue,
        CandyKindID.Green,
        CandyKindID.Purple,
        CandyKindID.Rainbow,
        CandyKindID.Yellow,
        CandyKindID.Pink,
    ];

    private void OnChangingRole(ChangingRoleEventArgs? ev)
    {
        var player = ev?.Player;
        if (player == null) return;
        if (!ev.IsAllowed) return;

        EffectFakeSyncProvider.Disable(player);

        var newRole = ev.NewRole;

        Timing.CallDelayed(0.2f, () =>
        {
            if (!IsPlayerValid(player)) return;
            if (!Round.InProgress) return;
            if (newRole == RoleTypeId.Spectator) return;
            if (!player.IsAlive) return;

            if (player.Role.Team == Team.SCPs) return;
            if (player.Inventory == null) return;
            if (player.IsInventoryFull) return;

            if (MapFlags.GetSeason() == SeasonTypeId.April)
            {
                if (!player.HasItem(ItemType.SCP330))
                    player.TryAddCandy(_candies.RandomItem());
            }
            else
            {
                if (!player.HasItem(ItemType.Flashlight))
                    player.GiveOrDrop(ItemType.Flashlight);
            }
        });
    }

    /// <summary>
    /// ドアに触ったとき、デバッグモード ON なら情報を DebugModeHandler に記録する。
    /// それ以外のプレイヤーにはゲートロックのヒントを出す。
    /// </summary>
    private static void DoorGet(InteractingDoorEventArgs? ev)
    {
        if (ev?.Player == null || ev.Door == null) return;

        if (DebugModeHandler.IsDebugMode(ev.Player))
        {
            var room = ev.Door.Room;
            if (room == null) return;

            var invRot = Quaternion.Inverse(room.Rotation);
            var info = new DebugModeHandler.DoorInfo(
                DoorType:   ev.Door.Type.ToString(),
                DoorName:   ev.Door.Name,
                RoomType:   room.Type.ToString(),
                LocalPos:   invRot * (ev.Player.Position - room.Position),
                LocalEuler: invRot.eulerAngles,
                RoomEuler:  room.Rotation.eulerAngles
            );

            DebugModeHandler.UpdateDoor(ev.Player, info);
            // Log.Debug($"[DoorGet] {ev.Player.Nickname} door={ev.Door.Type} room={room.Type}");
        }
        else
        {
            if (ev.Door.Type is DoorType.GateA or DoorType.GateB)
            {
                if (ev.Door.IsLocked && SpecialEventsHandler.Instance.NowEvent == SpecialEventType.None)
                {
                    ev.Player.ShowHint("収容違反への対応として暫くロックされているようだ・・・");
                }
            }
        }
    }

    private void OnUsed(UsedItemEventArgs ev)
    {
        if (ev.Player == null || ev.Item == null) return;
        if (ev.Player.HasFlag(SpecificFlagType.AntiMemeEffectDisabled))
        {
            if (ev.Item.Type == ItemType.SCP500 && !ev.Item.TryGetCustomItem(out _) && !CItem.TryGet(ev.Item, out _))
            {
                if (ev.Player.HasFlag(SpecificFlagType.Scp207Level4))
                {
                    ev.Player.EnableEffect(EffectType.Scp207, 4);
                    ev.Player.EnableEffect(EffectType.Invigorated, 60);
                }
            }
        }

        if (ev.Player.HasFlag(SpecificFlagType.Infecting610))
        {
            if (ev.Item.Type == ItemType.SCP500 && !ev.Item.TryGetCustomItem(out _) && !CItem.TryGet(ev.Item, out _))
            {
                ev.Player.TryRemoveFlag(SpecificFlagType.Infecting610);
            }
        }
    }

    private static void OnHurting(HurtingEventArgs ev)
    {
        if (ev.Player == null) return;
        if (ev.DamageHandler.Type != DamageType.Scp207) return;
        if (ev.Player.HasFlag(SpecificFlagType.Scp207Resistance))
        {
            ev.IsAllowed = false;
        }
    }

    private static void OnDying(DyingEventArgs ev)
    {
        if (ev.Player is null) return;
        if (ev.Player.GetCustomRole() != CRoleTypeId.None) return;
        if (ev.Player.Role.Team != Team.SCPs) return;
        if (ev.Player.Role.Type is RoleTypeId.Scp0492 or RoleTypeId.Scp079) return;
        Exiled.API.Features.Cassie.Clear();
        CassieHelper.AnnounceTermination(ev, "SCP "+string.Join(" ", Regex.Replace(ev.Player.Role.Name, @"[^0-9]", "").ToCharArray()), $"<color={CTeam.SCPs.GetTeamColor()}>{ev.Player.Role.Name}</color>");
    }
}
