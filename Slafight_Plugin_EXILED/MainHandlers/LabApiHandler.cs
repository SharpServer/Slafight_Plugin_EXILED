using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = LabApi.Features.Wrappers.LightSourceToy;
using Player = LabApi.Features.Wrappers.Player;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.LabApiBridgeHandlers;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class LabApiHandler : SlafightLabApiHandler, IBootstrapHandler
{
    private static LabApiHandler _instance;
    private static CustomItemTriggerPointLabHandler _customItemTriggerPointHandler;

    public static LabApiHandler Instance => _instance;

    public static void Register()
    {
        _instance = LabApiHandlerRegistry.Register(_instance);
        _customItemTriggerPointHandler = LabApiHandlerRegistry.Register(_customItemTriggerPointHandler);
    }

    public static void Unregister()
    {
        LabApiHandlerRegistry.Unregister(ref _customItemTriggerPointHandler);
        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => Exiled.Events.Handlers.Player.Dying += ModelRolesRagdoll, () => Exiled.Events.Handlers.Player.Dying -= ModelRolesRagdoll);
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.SearchedToy += InteractionEvent, () => LabApi.Events.Handlers.PlayerEvents.SearchedToy -= InteractionEvent);
        subscriptions.Add(() => LabApi.Events.Handlers.ServerEvents.RoundStarted += Init, () => LabApi.Events.Handlers.ServerEvents.RoundStarted -= Init);
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.RaPlayerListAddingPlayer += HideWatchFromRaPlayerList, () => LabApi.Events.Handlers.PlayerEvents.RaPlayerListAddingPlayer -= HideWatchFromRaPlayerList);
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo += HideWatchFromRaPlayerInfo, () => LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo -= HideWatchFromRaPlayerInfo);
    }

    public bool ActivatedAntiMemeProtocol;
    public bool ActivatedAntiMemeProtocolInPast;

    private void Init()
    {
        ActivatedAntiMemeProtocol = false;
        ActivatedAntiMemeProtocolInPast = false;
    }

    private void InteractionEvent(PlayerSearchedToyEventArgs ev)
    {
        var antiMemeButton = MapFlags.AntiMemeButton == Vector3.zero
            ? new Vector3(107.921f, 296.313f, -68.748f)
            : MapFlags.AntiMemeButton;

        if (ev.Interactable == null || Vector3.Distance(ev.Interactable.Position, antiMemeButton) >= 3f)
            return;

        if (!ActivatedAntiMemeProtocol)
        {
            if (Exiled.API.Features.Player.Get(ev.Player)?.GetTeam() is CTeam.Fifthists)
            {
                ev.Player.SendHint("※第五教会は開始できません！");
                return;
            }
            foreach (var player in Exiled.API.Features.Player.List)
            {
                if (!IsAntiMemeProtocolTarget(player)) continue;
                if (!ActivatedAntiMemeProtocolInPast)
                    player.Health = 10000;

                player.EnableEffect(EffectType.Poisoned, 255);
                player.EnableEffect(EffectType.Decontaminating, 255);
            }

            var count = 0;
            if (Exiled.API.Features.Player.List.Any(IsAntiMemeProtocolTarget))
            {
                count++;
                if (!ActivatedAntiMemeProtocolInPast)
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "By order of Facility Manager Control Room , $pitch_.85 Anti- $pitch_1 Me mu Protocol Activated .",
                        $"<color=#ff0087>施設管理者制御室</color>からの命令により、<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコロル</color>が有効化されました。エージェントにより反ミーム性物体の非活性化が開始されます。",
                        true,
                        false);
                    ActivatedAntiMemeProtocolInPast = true;
                }
                else
                {
                    Exiled.API.Features.Cassie.MessageTranslated(
                        "$pitch_.85 Anti- $pitch_1 Me mu Protocol Resumed .",
                        $"<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコル</color>が再開されました。",
                        false,
                        false);
                }

                ActivatedAntiMemeProtocol = true;
            }

            if (count <= 0)
                ev.Player.SendHint("<size=26>※対象が見つかりませんでした</size>", 3.5f);
        }
        else
        {
            foreach (var player in Exiled.API.Features.Player.List)
            {
                if (!IsAntiMemeProtocolTarget(player)) continue;
                player.DisableEffect(EffectType.Poisoned);
                player.DisableEffect(EffectType.Decontaminating);
            }

            if (!Exiled.API.Features.Player.List.Any()) return;
            Exiled.API.Features.Cassie.MessageTranslated(
                "$pitch_.85 Anti- $pitch_1 Me mu Protocol Stopped .",
                $"<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコル</color>が停止されました。",
                false,
                false);
            ActivatedAntiMemeProtocol = false;
        }
    }

    private static bool IsAntiMemeProtocolTarget(Exiled.API.Features.Player player)
    {
        return player.GetCustomRole() is CRoleTypeId.Scp3005 or CRoleTypeId.Scp3125;
    }

    private static bool IsHideWatchTarget(Player labPlayer)
    {
        if (labPlayer == null)
            return false;

        var player = Exiled.API.Features.Player.Get(labPlayer.ReferenceHub);
        return player?.GetCustomRole() == CRoleTypeId.HideWatch;
    }

    private static void HideWatchFromRaPlayerList(PlayerRaPlayerListAddingPlayerEventArgs ev)
    {
        if (IsHideWatchTarget(ev.Target))
            ev.InOverwatch = false;
    }

    private static void HideWatchFromRaPlayerInfo(PlayerRequestedRaPlayerInfoEventArgs ev)
    {
        if (!IsHideWatchTarget(ev.Target))
            return;

        ev.InfoBuilder.Replace(" <color=#008080>[OVERWATCH MODE]</color>", string.Empty);
    }

    /// <summary>
    /// SCP-3005 用 schematic 生成（見た目は昔のまま、ロール監視は WearsHandler に任せる）
    /// </summary>
    public static void Schem3005(Player labPlayer)
        => WearRoleSchematic(
            labPlayer,
            "SCP3005",
            nameof(Schem3005),
            playerScale: new Vector3(0.01f, 1f, 0.01f),
            afterAttach: (player, schem) =>
            {
                if (schem.transform.childCount > 0)
                    schem.transform.GetChild(0).localScale = Vector3.one;
                if (player.GameObject != null)
                    schem.transform.position = player.GameObject.transform.position;

                AttachRoleSchematicLight(schem, Color.magenta);
            });

    public static void Schem999(Player labPlayer)
        => WearRoleSchematic(
            labPlayer,
            "Scp999Model",
            nameof(Schem999),
            playerScale: new Vector3(0.35f, 0.2f, 0.35f),
            offset: new Vector3(0f, 0f, 0.05f),
            afterAttach: (player, schem) =>
            {
                if (player.GameObject != null)
                    schem.transform.position = player.GameObject.transform.position + new Vector3(0f, 0f, 0.05f);

                player.DestroySchematic(schem);
            });

    public static void SchemSnowWarrier(Player labPlayer)
        => WearRoleSchematic(
            labPlayer,
            "SnowWarrier",
            nameof(SchemSnowWarrier),
            afterAttach: (player, schem) =>
            {
                if (player.GameObject != null)
                    schem.transform.position = player.GameObject.transform.position;

                AttachRoleSchematicLight(schem, Color.white);
            });

    public static void SchemCandyWarrier(Player labPlayer)
        => WearRoleSchematic(
            labPlayer,
            "CandyWarrier",
            nameof(SchemCandyWarrier),
            playerScale: Vector3.one,
            afterAttach: (player, schem) =>
            {
                schem.AnimationController.Play("candy");

                if (player.GameObject != null)
                    schem.transform.position = player.GameObject.transform.position;
            });

    private static void WearRoleSchematic(
        Player labPlayer,
        string schematicName,
        string logName,
        Vector3? playerScale = null,
        Vector3? offset = null,
        Action<Player, SchematicObject> afterAttach = null)
    {
        Timing.CallDelayed(1.5f, () =>
        {
            if (labPlayer == null)
                return;

            var exiledPlayer = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
            if (exiledPlayer == null)
            {
                Log.Warn($"[LabApiHandler] {logName}: Exiled player not found.");
                return;
            }

            SchematicObject schem;
            try
            {
                schem = ObjectSpawner.SpawnSchematic(schematicName, Vector3.zero);
            }
            catch (Exception ex)
            {
                Log.Error($"[LabApiHandler] {logName}: Spawn error {ex}");
                return;
            }

            if (playerScale.HasValue)
                labPlayer.Scale = playerScale.Value;

            WearsHandler.RegisterExternal(exiledPlayer, schem, offset);

            Timing.CallDelayed(0.5f, () =>
            {
                if (schem == null || schem.transform == null)
                    return;

                afterAttach?.Invoke(labPlayer, schem);
            });
        });
    }

    private static void AttachRoleSchematicLight(SchematicObject schem, Color color)
    {
        if (schem == null || schem.transform == null)
            return;

        var light = Light.Create(Vector3.zero);
        light.Position = schem.transform.position + new Vector3(0f, -0.08f, 0f);
        light.Transform.parent = schem.transform;
        light.Scale = Vector3.one;
        light.Range = 10f;
        light.Intensity = 1.25f;
        light.Color = color;
    }

    private static void ModelRolesRagdoll(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
    {
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp3005)
        {
            ObjectSpawner.SpawnSchematic("SCP3005_N", ev.Player.Position, ev.Player.Rotation, Vector3.one);
        }
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp999)
        {
            ObjectSpawner.SpawnSchematic("Scp999Model", ev.Player.Position, ev.Player.Rotation, Vector3.one);
        }
    }
}
