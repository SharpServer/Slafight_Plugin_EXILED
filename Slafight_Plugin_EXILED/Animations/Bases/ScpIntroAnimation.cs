using System;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.Bases;

/// <summary>
/// SCP のダミーをスポーン地点に立たせ、暗くした部屋で照らして見せる登場演出の土台です。
/// </summary>
/// <remarks>
/// ステージはスポーン地点から一番開けた方向を前にして決めます。カメラは <see cref="Eye"/> /
/// <see cref="Around"/> でステージからの相対位置に置き、壁に掛かればビルダーが手前に寄せます。
/// </remarks>
public abstract class ScpIntroAnimation : FilmmakerAnimation
{
    // HDRP のライトなので値は大きめ。体がほのかに光る程度で 8、部屋を照らすなら数百が目安。
    protected const float KeyLight = 600f;
    protected const float RimLight = 250f;
    protected const float FlashLight = 2500f;

    private const float ProbeDistance = 14f;
    private const int ProbeDirections = 32;
    private const float WallMargin = 0.6f;
    private const float MinimumDistance = 0.8f;

    private readonly float[] space = new float[ProbeDirections];
    private Npc actor;

    /// <summary>
    /// 登場させる SCP です。
    /// </summary>
    protected abstract RoleTypeId Role { get; }

    /// <summary>
    /// ステージ位置 (体の中心) から見せたい箇所 (顔など) までの高さです。
    /// </summary>
    protected virtual float FocusHeight => 0.5f;

    /// <summary>
    /// カメラを引くのに欲しい奥行きです。スポーン地点の向きがこれだけ開けていればそのまま使います。
    /// </summary>
    protected virtual float DesiredSpace => 6f;

    /// <summary>
    /// ステージの部屋の照明を落とすかどうか。
    /// </summary>
    protected virtual bool DarkenRoom => true;

    /// <summary>
    /// スポーン地点が取れない役職の代わりに使う部屋です。中央が開けている部屋を選んでください。
    /// </summary>
    protected virtual RoomType FallbackRoom => RoomType.LczCrossing;

    /// <summary>
    /// ダミーの役職が付く (約 0.6 秒) まで、最初の構図で待ちます。
    /// </summary>
    public override float PrepareSeconds => Npc.SpawnSetRoleDelay + 0.5f;

    /// <summary>
    /// ダミーを立たせる位置です (<c>Player.Position</c> 基準 = 体の中心)。
    /// </summary>
    protected Vector3 Stage { get; private set; }

    /// <summary>
    /// ステージの前方 (一番開けた方向) です。ダミーは既定でこちらを向きます。
    /// </summary>
    protected Quaternion Facing { get; private set; } = Quaternion.identity;

    /// <summary>
    /// 前方に壁まで空いている距離です。
    /// </summary>
    protected float OpenDistance { get; private set; } = ProbeDistance;

    /// <summary>
    /// ステージのある部屋です。
    /// </summary>
    protected Room StageRoom { get; private set; }

    /// <summary>
    /// カメラが注視する点です。
    /// </summary>
    protected Vector3 Focus => Stage + Vector3.up * FocusHeight;

    /// <summary>
    /// 登場中のダミーです。準備前と片付け後は null。
    /// </summary>
    protected Npc Actor => actor is { IsConnected: true } ? actor : null;

    /// <summary>
    /// 目線をステージ基準の (右, 上, 前) に置くときのカメラ位置です。前方は <see cref="OpenDistance"/> に収めます。
    /// </summary>
    protected Vector3 Eye(float right, float up, float forward)
        => Local(right, up, Fit(forward)) - Vector3.up * FilmmakerAnimationClip.DefaultEyeHeight;

    /// <summary>
    /// ステージを中心に、前方から右回りに <paramref name="degrees"/> 度回った目線のカメラ位置です。
    /// 半径はその方向の壁までに収めます。
    /// </summary>
    protected Vector3 Around(float degrees, float radius, float up)
        => Stage + Facing * (Quaternion.Euler(0f, degrees, 0f) * new Vector3(0f, up, FitAt(degrees, radius)))
           - Vector3.up * FilmmakerAnimationClip.DefaultEyeHeight;

    /// <summary>
    /// 前方から右回りに <paramref name="degrees"/> 度の方向に、壁まで空いている距離です。
    /// </summary>
    protected float SpaceAt(float degrees)
    {
        float step = 360f / ProbeDirections;
        float index = Mathf.Repeat(degrees, 360f) / step;
        int a = Mathf.FloorToInt(index) % ProbeDirections;
        int b = (a + 1) % ProbeDirections;
        return Mathf.Lerp(space[a], space[b], index - Mathf.Floor(index));
    }

    /// <summary>
    /// 前方 (0 度) から左右へ、<paramref name="minimum"/> 以上空いている範囲を広げた角度です (左が負)。
    /// 回り込む演出の角度はこの中に収めると壁に当たりません。
    /// </summary>
    protected (float From, float To) OpenArc(float minimum, float maximumSpan = 180f)
    {
        float step = 360f / ProbeDirections;
        float half = maximumSpan * 0.5f;
        float left = 0f;
        float right = 0f;

        while (left - step >= -half && SpaceAt(left - step) >= minimum)
            left -= step;

        while (right + step <= half && SpaceAt(right + step) >= minimum)
            right += step;

        return (left, right);
    }

    /// <summary>
    /// <paramref name="degrees"/> 方向の距離を、壁まで余裕を残して収めます。
    /// </summary>
    protected float FitAt(float degrees, float distance)
        => Mathf.Clamp(distance, MinimumDistance, Mathf.Max(MinimumDistance, SpaceAt(degrees) - WallMargin));

    /// <summary>
    /// ステージ基準の相対位置をワールド座標にします。x が右、y が上、z が前方です。
    /// </summary>
    protected Vector3 Local(float right, float up, float forward)
        => Stage + Facing * new Vector3(right, up, forward);

    /// <summary>
    /// 前方の距離を、壁まで余裕を残して収めます。
    /// </summary>
    protected float Fit(float forward)
        => forward <= 0f ? forward : FitAt(0f, forward);

    /// <summary>
    /// 欲しい奥行き (<see cref="DesiredSpace"/>) に対する <paramref name="fraction"/> の距離です。
    /// 狭い部屋では全体が縮むので、遠景と近景の差が残ります。
    /// </summary>
    protected float Span(float fraction)
        => Mathf.Max(MinimumDistance, Mathf.Min(DesiredSpace, OpenDistance - WallMargin) * fraction);

    protected sealed override void Build(FilmmakerAnimationBuilder builder)
    {
        ResolveStage();
        BuildCamera(builder);
    }

    /// <summary>
    /// カメラの動きと合図を並べます。<see cref="Stage"/> などはこの時点で決まっています。
    /// </summary>
    protected abstract void BuildCamera(FilmmakerAnimationBuilder builder);

    protected internal sealed override void OnPreparing(FilmmakerCutscenePreparingEventArgs ev)
    {
        FilmmakerCutscene cutscene = ev.Cutscene;

        if (DarkenRoom)
            cutscene.SetRoomLights(StageRoom, false);

        actor = cutscene.SpawnDummy($"{Role} (演出)", Role, Stage, Facing, npc => OnActorReady(cutscene, npc));
        OnStaging(cutscene);
    }

    /// <summary>
    /// 準備の始めに呼ばれます。ライトなどの小道具はここで出します (ダミーはまだ準備中です)。
    /// </summary>
    protected virtual void OnStaging(FilmmakerCutscene cutscene)
    {
    }

    /// <summary>
    /// ダミーの役職が付き、ステージに立った後に呼ばれます。
    /// </summary>
    protected virtual void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
    }

    /// <summary>
    /// ダミーに本体のアクション (能力のキー入力など) を実行させます。使えない状態なら何もしません。
    /// </summary>
    /// <remarks>使えるパスは <c>slc cutscene actions &lt;ダミー&gt;</c> で確認できます。</remarks>
    protected bool Act(FilmmakerCutscene cutscene, string path)
        => Actor is { } npc && cutscene.DummyAction(npc, path);

    /// <summary>
    /// ライトの明るさを <paramref name="duration"/> 秒かけて変えます。
    /// </summary>
    protected static void FadeLight(FilmmakerCutscene cutscene, Light light, float to, float duration)
    {
        float from = light.Intensity;
        cutscene.Animate(duration, t => light.Intensity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
    }

    /// <summary>
    /// ダミーを水平に <paramref name="target"/> へ向けます。
    /// </summary>
    protected void TurnActor(Vector3 target)
    {
        if (Actor is not { } npc)
            return;

        Vector3 direction = target - npc.Position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            npc.Rotation = Quaternion.LookRotation(direction);
    }

    /// <summary>
    /// ダミーを <paramref name="duration"/> 秒かけて <paramref name="rotation"/> へ振り向かせます。
    /// </summary>
    protected void RotateActor(FilmmakerCutscene cutscene, Quaternion rotation, float duration)
    {
        if (Actor is not { } npc)
            return;

        Quaternion from = npc.Rotation;
        from = Quaternion.Euler(0f, from.eulerAngles.y, 0f);
        cutscene.Animate(duration, t =>
        {
            if (Actor is not null)
                npc.Rotation = Quaternion.Slerp(from, rotation, Mathf.SmoothStep(0f, 1f, t));
        });
    }

    /// <summary>
    /// ダミーがカメラを目で追い続けるようにします。
    /// </summary>
    protected void FollowCamera(FilmmakerCutscene cutscene)
        => cutscene.Every(0.05f, () =>
        {
            if (cutscene.TryGetCameraPose(out Vector3 camera, out _))
                TurnActor(camera);
        });

    /// <summary>
    /// ダミーを <paramref name="duration"/> 秒かけて <paramref name="destination"/> へ動かします。
    /// </summary>
    protected void MoveActor(FilmmakerCutscene cutscene, Vector3 destination, float duration)
    {
        if (Actor is not { } npc)
            return;

        Vector3 origin = npc.Position;
        cutscene.Animate(duration, t =>
        {
            if (Actor is not null)
                npc.Position = Vector3.Lerp(origin, destination, Mathf.SmoothStep(0f, 1f, t));
        });
    }

    private void ResolveStage()
    {
        float yaw;
        if (Role.GetRandomSpawnLocation() is { } spawn)
        {
            Stage = spawn.Position;
            yaw = spawn.HorizontalRotation;
        }
        else if (Room.Get(FallbackRoom) is { } room)
        {
            Stage = room.Position + Vector3.up;
            yaw = room.Rotation.eulerAngles.y;
        }
        else
        {
            throw new InvalidOperationException($"{Role} のスポーン地点も {FallbackRoom} も見つかりません。");
        }

        StageRoom = Room.Get(Stage);

        // スポーン地点の向きが十分開けていればそのまま、狭ければ一番開けた方向を前にする。
        Vector3 eye = Stage + Vector3.up * FilmmakerAnimationClip.DefaultEyeHeight;
        float bestYaw = yaw;
        float bestDistance = Probe(eye, yaw);

        if (bestDistance < DesiredSpace)
        {
            for (int i = 1; i < ProbeDirections; i++)
            {
                float candidate = yaw + 360f * i / ProbeDirections;
                float distance = Probe(eye, candidate);
                if (distance > bestDistance + 0.25f)
                {
                    bestDistance = distance;
                    bestYaw = candidate;
                }
            }
        }

        Facing = Quaternion.Euler(0f, bestYaw, 0f);
        OpenDistance = bestDistance;

        for (int i = 0; i < ProbeDirections; i++)
            space[i] = Probe(eye, bestYaw + 360f * i / ProbeDirections);
    }

    private static float Probe(Vector3 origin, float yaw)
    {
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        return Physics.SphereCast(origin, 0.2f, direction, out RaycastHit hit, ProbeDistance, FpcStateProcessor.Mask, QueryTriggerInteraction.Ignore)
            ? hit.distance
            : ProbeDistance;
    }
}
