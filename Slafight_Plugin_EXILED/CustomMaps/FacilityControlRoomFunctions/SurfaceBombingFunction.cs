using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Pickups.Projectiles;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;

public sealed class SurfaceBombingFunction : FacilityControlRoomFunction
{
    private static readonly Vector3 BombingStartPoint = new(138f, 299f, -41f);
    private static readonly Vector3 BombingEndPoint = new(-20f, 305f, -41f);

    private const float BombDurationSeconds = 3f;  // 爆撃総時間（秒）
    private const int BombCount = 200;               // 爆弾の総数
    private const float BombScatterRadius = 10f;
    private const float BombFuseSeconds = 1.25f;
    private const float DownwardVelocity = 18f;

    private static float BombInterval => BombDurationSeconds / BombCount;

    private static CoroutineHandle _bombingHandle;

    public override string Id => "SurfaceBombing";
    public override string DisplayName => "爆撃要請";
    public override string Description => "地上制圧のため、爆撃を要請する。";
    public override KeycardPermissions RequiredPermissions => KeycardPermissions.ArmoryLevelThree | KeycardPermissions.ExitGates;
    public override bool UseCooldown => true;
    public override float CooldownSeconds => 300f;

    private static SpeakerApi.Playback AlertPlayback;

    public override void ResetState()
    {
        if (_bombingHandle.IsRunning)
            Timing.KillCoroutines(_bombingHandle);
    }

    public override FacilityControlRoomFunctionResult Execute(FacilityControlRoomFunctionContext context)
    {
        return OnExecute(context);
    }

    private FacilityControlRoomFunctionResult OnExecute(FacilityControlRoomFunctionContext context)
    {
        if (_bombingHandle.IsRunning)
        {
            return Failure("<size=24>爆撃要請に失敗しました。\n既に爆撃が進行中です。</size>");
        }
        StartBombing(skipStartupDelay: false);

        return Success("<size=24>爆撃要請を送信しました。\n地上への爆撃を開始します。</size>");
    }

    public static bool TryStartInstantBombing(out string failureReason)
    {
        if (Round.IsLobby || Round.IsEnded)
        {
            failureReason = "Surface bombing cannot start outside an active round.";
            return false;
        }

        if (_bombingHandle.IsRunning)
        {
            failureReason = "Surface bombing is already in progress.";
            return false;
        }

        StartBombing(skipStartupDelay: false);
        failureReason = string.Empty;
        return true;
    }

    private static void StartBombing(bool skipStartupDelay)
    {
        Player.List.Where(p => p.Position.y >= 290f).ToList().ForEach(p => p.MessageTranslated(
            "Defense Forces to Control, Operation Accepted. Starting Surface Attack.", string.Empty,
            "[防衛部隊から管制室へ]地上爆撃を承認しました。これより攻撃を開始します・・・",
            true, false));

        AlertPlayback = SpeakerApi.PlayLoop("sbialert.ogg", "SurfaceBombing",
            Player.List.GetRandomValue(p => p.Zone is ZoneType.Surface).Position, 
            maxDistance: 250f,
            minDistance: 0f);
        _bombingHandle = Timing.RunCoroutine(SurfaceBombingCoroutine(skipStartupDelay));
    }

    private static IEnumerator<float> SurfaceBombingCoroutine(bool skipStartupDelay)
    {
        if (!skipStartupDelay)
            yield return Timing.WaitForSeconds(6.5f);

        for (int i = 0; i < BombCount; i++)
        {
            if (Round.IsLobby || Round.IsEnded)
                yield break;

            float progress = BombCount <= 1 ? 1f : i / (BombCount - 1f);
            Vector3 position = Vector3.Lerp(BombingStartPoint, BombingEndPoint, progress);
            position += new Vector3(
                Random.Range(-BombScatterRadius, BombScatterRadius),
                0f,
                Random.Range(-BombScatterRadius, BombScatterRadius));

            SpawnTimeGrenade(position);

            yield return Timing.WaitForSeconds(BombInterval);
        }

        SpeakerApi.StopClip(AlertPlayback.AudioPlayer.Name, AlertPlayback.ClipName);
    }

    private static void SpawnTimeGrenade(Vector3 position)
    {
        if (Projectile.CreateAndSpawn(ProjectileType.FragGrenade, position, Quaternion.identity) is not ExplosionGrenadeProjectile grenade)
            return;

        grenade.FuseTime = BombFuseSeconds;

        if (grenade.Rigidbody != null)
            grenade.Rigidbody.linearVelocity = Vector3.down * DownwardVelocity;
    }
}
