using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = LabApi.Features.Wrappers.LightSourceToy;
using Logger = LabApi.Features.Console.Logger;
using Player = LabApi.Features.Wrappers.Player;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class LabApiHandler : CustomEventsHandler, IBootstrapHandler
{
    public static LabApiHandler Instance { get; private set; }
    public static void Register() { Instance = new(); CustomHandlersManager.RegisterEventsHandler(Instance); }
    public static void Unregister() { CustomHandlersManager.UnregisterEventsHandler(Instance); Instance = null; }

    public LabApiHandler()
    {
        LabApi.Events.Handlers.ServerEvents.RoundStarted += PickupSetup;
        Exiled.Events.Handlers.Player.Dying += DiedCassie;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy += InteractionEvent;
        LabApi.Events.Handlers.ServerEvents.RoundStarted += Init;
        LabApi.Events.Handlers.ServerEvents.RoundStarted += Init;
    }

    ~LabApiHandler()
    {
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= PickupSetup;
        Exiled.Events.Handlers.Player.Dying -= DiedCassie;
        LabApi.Events.Handlers.PlayerEvents.SearchedToy -= InteractionEvent;
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= Init;
        LabApi.Events.Handlers.ServerEvents.RoundStarted -= Init;
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
        if (ev.Interactable.Position != new Vector3(107.921f, 296.313f, -68.748f))
            return;

        if (!ActivatedAntiMemeProtocol)
        {
            foreach (var player in Exiled.API.Features.Player.List)
            {
                if (player.GetCustomRole() != CRoleTypeId.Scp3005) continue;
                if (!ActivatedAntiMemeProtocolInPast)
                    player.Health = 10000;

                player.EnableEffect(EffectType.Poisoned, 255);
                player.EnableEffect(EffectType.Decontaminating, 255);
            }

            var count = 0;
            if (Exiled.API.Features.Player.List.Any(player => player.GetCustomRole() == CRoleTypeId.Scp3005))
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
                if (player.GetCustomRole() != CRoleTypeId.Scp3005) continue;
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

    private void PickupSetup()
    {
        Logger.Info("LabApi Loader: Green");

        Timing.CallDelayed(1.05f, () =>
        {
            CItem.Get<HIDTurret>()?.Spawn(new Vector3(134.94f, 300.65f, -65f));
            CItem.Get<NvgNormal>()?.Spawn(Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 1.5f));
        });
        Timing.CallDelayed(2.0f, PickupSetupTriggerPoints);
    }

    private static void PickupSetupTriggerPoints()
    {
        var points = TriggerPointManager.GetAll();
        foreach (var point in points)
        {
            if (point.Base is not SerializableCustomTriggerPoint trig || string.IsNullOrEmpty(trig.Tag))
                continue;

            var pos = TriggerPointManager.GetWorldPosition(point);

            try
            {
                switch (trig.Tag)
                {
                    case "CISR_GoCRailgun":
                        CItem.Get<GunGoCRailgun>()?.Spawn(pos);
                        break;
                    case "CISR_Scp1425":
                        var scp1425Pickup = CItem.Get<Scp1425>()?.Spawn(pos);
                        scp1425Pickup?.Rotation *= Quaternion.Euler(180f, 0f, 0f);
                        break;
                    case "CISR_SNAV300":
                        CItem.Get<SNAV300>()?.Spawn(pos);
                        break;
                    case "CISR_MFP":
                        CItem.Get<ClassXMemoryForcePil>()?.Spawn(pos);
                        break;
                    case "Scp682SpawnPoint":
                        CItem.Get<OmegaWarheadAccess>()?.Spawn(pos);
                        break;
                    case "CISR_SCP513":
                        CItem.Get<Scp513Item>()?.Spawn(pos);
                        break;
                    case "CISR_SQ":
                        var sq = CItem.Get<SchwarzschildQuasar>()?.Spawn(pos);
                        sq?.Transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                        break;
                    case "CISR_AntiMemeGrenade":
                        CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                        CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                        CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                        CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                        CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[PickupSetupTriggerPoints] Error while spawning CustomItem at trigger point {trig.Tag}: {e}");
            }
        }
    }

    /// <summary>
    /// SCP-3005 用 schematic 生成（見た目は昔のまま、ロール監視は WearsHandler に任せる）
    /// </summary>
    public static void Schem3005(Player labPlayer)
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var exiledPlayer = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
            Logger.Info($"[DEBUG] Schem3005 ExiledPlayer={(exiledPlayer != null ? exiledPlayer.Nickname : "null")}");
            if (exiledPlayer == null)
            {
                Logger.Warn("[LabApiHandler] Schem3005: Exiled player not found.");
                return;
            }

            SchematicObject schem;
            try
            {
                schem = ObjectSpawner.SpawnSchematic("SCP3005", Vector3.zero);
            }
            catch (Exception ex)
            {
                Logger.Error("[LabApiHandler] Schem3005: Spawn error " + ex);
                return;
            }

            labPlayer.Scale = new Vector3(0.01f, 1f, 0.01f);
            schem.transform.SetParent(labPlayer.GameObject?.transform);

            // ロール監視用に WearsHandler に登録
            WearsHandler.RegisterExternal(exiledPlayer, schem);

            Timing.CallDelayed(0.5f, () =>
            {
                if (schem == null || schem.transform == null)
                    return;

                schem.transform.GetChild(0).localScale = Vector3.one;
                if (labPlayer.GameObject != null) schem.transform.position = labPlayer.GameObject.transform.position;

                var light = Light.Create(Vector3.zero);
                light.Position = schem.transform.position + new Vector3(0f, -0.08f, 0f);
                light.Transform.parent = schem.transform;
                light.Scale = Vector3.one;
                light.Range = 10f;
                light.Intensity = 1.25f;
                light.Color = Color.magenta;
            });
        });
    }

    public static void Schem999(Player labPlayer)
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var exiledPlayer = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
            Logger.Info($"[DEBUG] S999 ExiledPlayer={(exiledPlayer != null ? exiledPlayer.Nickname : "null")}");
            if (exiledPlayer == null)
            {
                Logger.Warn("[LabApiHandler] Schem999: Exiled player not found.");
                return;
            }

            SchematicObject schem;
            try
            {
                schem = ObjectSpawner.SpawnSchematic("Scp999Model", Vector3.zero);
            }
            catch (Exception ex)
            {
                Logger.Error("[LabApiHandler] Schem999: Spawn error " + ex);
                return;
            }

            labPlayer.Scale = new Vector3(0.35f, 0.2f, 0.35f);
            schem.transform.SetParent(labPlayer.GameObject?.transform);

            WearsHandler.RegisterExternal(exiledPlayer, schem);

            Timing.CallDelayed(0.5f, () =>
            {
                if (schem == null || schem.transform == null)
                    return;

                if (labPlayer.GameObject != null)
                    schem.transform.position = labPlayer.GameObject.transform.position + new Vector3(0f, 0f, 0.05f);

                for (var i = 0; i < schem.transform.childCount; i++)
                {
                    var child = schem.transform.GetChild(i);
                    Logger.Info($"[Scp999Model] Child {i}: {child.name}, localScale={child.localScale}");
                }
                    
                labPlayer.DestroySchematic(schem);
            });
        });
    }

    public static void SchemSnowWarrier(Player labPlayer)
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var exiledPlayer = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
            Logger.Info($"[DEBUG] SSW ExiledPlayer={(exiledPlayer != null ? exiledPlayer.Nickname : "null")}");
            if (exiledPlayer == null)
            {
                Logger.Warn("[LabApiHandler] SchemSnowWarrier: Exiled player not found.");
                return;
            }

            SchematicObject schem;
            try
            {
                schem = ObjectSpawner.SpawnSchematic("SnowWarrier", Vector3.zero);
            }
            catch (Exception ex)
            {
                Logger.Error("[LabApiHandler] SchemSnowWarrier: Spawn error " + ex);
                return;
            }

            schem.transform.SetParent(labPlayer.GameObject?.transform);

            WearsHandler.RegisterExternal(exiledPlayer, schem);

            Timing.CallDelayed(0.5f, () =>
            {
                if (schem == null || schem.transform == null)
                    return;

                if (labPlayer.GameObject != null) schem.transform.position = labPlayer.GameObject.transform.position;

                var light = Light.Create(Vector3.zero);
                light.Position = schem.transform.position + new Vector3(0f, -0.08f, 0f);
                light.Transform.parent = schem.transform;
                light.Scale = Vector3.one;
                light.Range = 10f;
                light.Intensity = 1.25f;
                light.Color = Color.white;
            });
        });
    }
    
    public static void SchemCandyWarrier(Player labPlayer)
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var exiledPlayer = Exiled.API.Features.Player.Get(labPlayer.NetworkId);
            Logger.Info($"[DEBUG] SCW ExiledPlayer={(exiledPlayer != null ? exiledPlayer.Nickname : "null")}");
            if (exiledPlayer == null)
            {
                Logger.Warn("[LabApiHandler] SchemCandyWarrier: Exiled player not found.");
                return;
            }

            SchematicObject schem;
            try
            {
                schem = ObjectSpawner.SpawnSchematic("CandyWarrier", Vector3.zero);
            }
            catch (Exception ex)
            {
                Logger.Error("[LabApiHandler] SchemCandyWarrier: Spawn error " + ex);
                return;
            }

            labPlayer.Scale = Vector3.one;
            schem.transform.SetParent(labPlayer.GameObject?.transform);

            WearsHandler.RegisterExternal(exiledPlayer, schem);

            Timing.CallDelayed(0.5f, () =>
            {
                if (schem == null || schem.transform == null)
                    return;
                
                schem.AnimationController.Play("candy");

                if (labPlayer.GameObject != null) schem.transform.position = labPlayer.GameObject.transform.position;
            });
        });
    }

    private static void DiedCassie(Exiled.Events.EventArgs.Player.DyingEventArgs ev)
    {
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp3005)
        {
            ObjectSpawner.SpawnSchematic("SCP3005", ev.Player.Position, ev.Player.Rotation, Vector3.one);
        }
        if (ev.Player.GetCustomRole() == CRoleTypeId.Scp999)
        {
            ObjectSpawner.SpawnSchematic("Scp999Model", ev.Player.Position, ev.Player.Rotation, Vector3.one);
        }
    }
}