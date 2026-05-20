using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using MEC;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.CustomMaps.FacilityControlRoomFunctions;

public sealed class SurfaceBombingFunction : FacilityControlRoomFunction
{
    private static readonly Vector3 BombingStartPoint = new(138f, 299f, -41f);
    private static readonly Vector3 BombingEndPoint = new(-40f, 299f, -41f);

    private const int BombCount = 32;
    private const float BombInterval = 0.35f;
    private const float BombScatterRadius = 7.5f;
    private const float BombFuseSeconds = 2.25f;
    private const float DownwardVelocity = 18f;

    private CoroutineHandle _bombingHandle;

    public override string Id => "SurfaceBombing";
    public override string DisplayName => "爆撃要請";
    public override string Description => "地上制圧のため、爆撃を要請する。";
    public override bool UseCooldown => true;
    public override float CooldownSeconds => 300f;

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
        Player.List.Where(p => p.Position.y >= 290f).ToList().ForEach(p => p.MessageTranslated(
            "Defense Forces to Control, Operation Accepted. Starting Surface Attack.",string.Empty,
            "[防衛部隊から管制室へ]地上爆撃を承認しました。これより攻撃を開始します・・・",
            true, false));
        _bombingHandle = Timing.RunCoroutine(SurfaceBombingCoroutine());

        return Success("<size=24>爆撃要請を送信しました。\n地上への爆撃を開始します。</size>");
    }

    private static IEnumerator<float> SurfaceBombingCoroutine()
    {
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
