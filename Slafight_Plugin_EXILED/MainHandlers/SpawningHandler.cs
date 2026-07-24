using System;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class SpawningHandler : IBootstrapHandler, IDisposable
{
    public static SpawningHandler Instance { get; private set; }
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

    public SpawningHandler()
    {
        SpawnSystem.Spawning += OnSpawning;
        SpawnSystem.Spawned += OnSpawned;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SpawnSystem.Spawning -= OnSpawning;
        SpawnSystem.Spawned -= OnSpawned;
        GC.SuppressFinalize(this);
    }
    private void OnSpawning(object sender, SpawnSystem.CustomSpawningEventArgs ev)
    {
        // SpawnType が既に確定している or Default 以外のコンテキストは触らない
        if (ev.SpawnType.HasValue)
            return;
        if (ev.NowContext.Name != "Default")
            return;

        // 3005 / FifthistPriest が場にいるかどうか
        var hasGodBlessedRolePlayer = Player.List.Any(p =>
            p.GetCustomRole() == CRoleTypeId.Scp3005 ||
            p.GetCustomRole() == CRoleTypeId.FifthistPriest);

        var w = ev.ContextOverride;

        // ===== 財団敵 (Chaos/Fifthist) 側の調整 =====
        if (ev.Faction == Faction.FoundationEnemy)
        {
            if (hasGodBlessedRolePlayer)
            {
                if (ev.IsMiniWave)
                {
                    if (w.ContainsKey(SpawnTypeId.GoiFifthistBackup))
                        w[SpawnTypeId.GoiFifthistBackup] = 40;
                    if (w.ContainsKey(SpawnTypeId.GoiChaosBackup))
                        w[SpawnTypeId.GoiChaosBackup] = 60;
                }
                else
                {
                    if (w.ContainsKey(SpawnTypeId.GoiFifthistNormal))
                        w[SpawnTypeId.GoiFifthistNormal] = 40;
                    if (w.ContainsKey(SpawnTypeId.GoiChaosNormal))
                        w[SpawnTypeId.GoiChaosNormal] = 60;
                }
            }
            else
            {
                if (ev.IsMiniWave)
                {
                    if (w.ContainsKey(SpawnTypeId.GoiFifthistBackup))
                        w[SpawnTypeId.GoiFifthistBackup] = 0;
                    if (w.ContainsKey(SpawnTypeId.GoiChaosBackup))
                        w[SpawnTypeId.GoiChaosBackup] = 100;
                }
                else
                {
                    if (w.ContainsKey(SpawnTypeId.GoiFifthistNormal))
                        w[SpawnTypeId.GoiFifthistNormal] = 0;
                    if (w.ContainsKey(SpawnTypeId.GoiChaosNormal))
                        w[SpawnTypeId.GoiChaosNormal] = 100;
                }
            }

            return;
        }

        // ===== 財団味方 (MTF) 側の調整 =====
        if (ev.Faction == Faction.FoundationStaff && hasGodBlessedRolePlayer)
        {
            if (ev.IsMiniWave)
            {
                if (w.ContainsKey(SpawnTypeId.MtfNtfBackup))
                    w[SpawnTypeId.MtfNtfBackup] = 40;
                if (w.ContainsKey(SpawnTypeId.MtfHdBackup))
                    w[SpawnTypeId.MtfHdBackup] = 20;

                if (w.ContainsKey(SpawnTypeId.MtfSneBackup))
                    w[SpawnTypeId.MtfSneBackup] = 40;
            }
            else
            {
                if (w.ContainsKey(SpawnTypeId.MtfNtfNormal))
                    w[SpawnTypeId.MtfNtfNormal] = 40;
                if (w.ContainsKey(SpawnTypeId.MtfHdNormal))
                    w[SpawnTypeId.MtfHdNormal] = 20;

                if (w.ContainsKey(SpawnTypeId.MtfSneNormal))
                    w[SpawnTypeId.MtfSneNormal] = 40;
            }
        }
    }

    private void OnSpawned(object sender, SpawnSystem.CustomSpawningEventArgs ev)
    {
        // Spawned は「実際に湧いた後」だけ飛んでくる前提なので IsAllowed チェックは不要
        if (!ev.SpawnType.HasValue)
            return;

        var spawnType = ev.SpawnType.Value;
        int spawnCount = ev.SpawnCount;
        switch (spawnType)
        {
            // Mobile Task Forces
            case SpawnTypeId.MtfNtfNormal:
                SpeakerApi.Play("_w_ntf.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceNtfArrival();
                break;
            case SpawnTypeId.MtfNtfBackup:
                SpeakerApi.Play("_w_ntf.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceNtfBackup();
                break;

            case SpawnTypeId.MtfHdNormal:
                SpeakerApi.Play("_w_hd.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceHdArrival();
                break;
            case SpawnTypeId.MtfHdBackup:
                SpeakerApi.Play("_w_hd.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceHdBackup();
                break;

            case SpawnTypeId.MtfLastOperationNormal:
                SpeakerApi.Play("_w_lo.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceLastOperationArrival();
                break;
            case SpawnTypeId.MtfLastOperationBackup:
                SpeakerApi.Play("_w_lo.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceLastOperationBackup();
                break;

            case SpawnTypeId.MtfSneNormal:
                SpeakerApi.Play("_w_sne.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceSneArrival();
                break;
            case SpawnTypeId.MtfSneBackup:
                SpeakerApi.Play("_w_sne.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceSneBackup();
                break;

            case SpawnTypeId.MtfLwsNormal:
                SpeakerApi.Play("_w_lws.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceLwsArrival();
                break;
            case SpawnTypeId.MtfLwsBackup:
                SpeakerApi.Play("_w_lws.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceLwsBackup();
                break;

            case SpawnTypeId.MtfRrhNormal:
                SpeakerApi.Play("_w_rrh.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceRrhArrival();
                break;
            case SpawnTypeId.MtfRrhBackup:
                SpeakerApi.Play("_w_rrh.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceRrhBackup();
                break;

            case SpawnTypeId.MtfPdx:
                SpeakerApi.Play("_w_pdx.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnouncePdxArrival();
                break;

            // ==== Groups of Interests ====
            case SpawnTypeId.GoiChaosNormal:
            case SpawnTypeId.GoiChaosBackup:
                SpeakerApi.Play("_w_chaos.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceChaos(spawnCount);
                break;

            case SpawnTypeId.GoiFifthistNormal:
            case SpawnTypeId.GoiFifthistBackup:
                SpeakerApi.Play("_w_fifthists.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceFifthist(spawnCount);
                break;

            case SpawnTypeId.GoiGoCNormal:
            case SpawnTypeId.GoiGoCBackup:
                SpeakerApi.Play("_w_ungoc.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceGoCEnter(spawnCount);
                break;

            case SpawnTypeId.GoiHorizonInitiative:
                SpeakerApi.Play("_w_initiative.ogg", "WaveTheme", Vector3.zero, true, null, false, 999999999, 0);
                CassieHelper.AnnounceInitiativeEnter(spawnCount);
                break;

            // ==== EXPERIMENTAL FEATURES ==== //
            case SpawnTypeId.SecurityTeam:
                TerminalTrain.Start();
                CassieHelper.AnnounceSecurityTeamEnter(spawnCount);
                break;

            case SpawnTypeId.ChaosAgents:
                TerminalTrain.Start();
                break;

            // ==== WARRIERS ==== //
            case SpawnTypeId.GoiSnowNormal:
                break;
            case SpawnTypeId.GoiSnowBackup:
                break;
        }
    }
}
