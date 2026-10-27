using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// 演出中だけ存在する小道具です。ここで出したものは、打ち切り・ラウンド再開を含めて演出の終わりに片付きます。
/// </summary>
public sealed partial class FilmmakerCutscene
{
    private readonly Dictionary<Room, RoomLightingState> _rooms = new();

    /// <summary>
    /// ライトを出します。
    /// </summary>
    /// <remarks>部屋の照明が点いたままだと弱いライトは見えません。<see cref="SetRoomLights"/> で落としてから使うと効きます。</remarks>
    public Light SpawnLight(Vector3 position, Color color, float intensity, float range, LightShadows shadows = LightShadows.Soft)
    {
        Light light = Light.Create(position, null, null, false, color);
        light.Intensity = intensity;
        light.Range = range;
        light.ShadowType = shadows;
        light.Spawn();

        OnEnd(() =>
        {
            if (light.Base != null)
                light.Destroy();
        });

        return light;
    }

    /// <summary>
    /// 演出用のダミーを出します。役職が付く (<see cref="Npc.SpawnSetRoleDelay"/> 秒後) と無敵・Noclip で
    /// <paramref name="position"/> に立ち、<paramref name="onReady"/> が呼ばれます。
    /// </summary>
    public Npc SpawnDummy(string name, RoleTypeId role, Vector3 position, Quaternion rotation, Action<Npc> onReady = null)
    {
        Npc npc = Npc.Spawn(name, role, true, position);
        InternalNpcRegistry.Register(npc, InternalNpcCategory.Generic);
        OnEnd(() =>
        {
            FilmmakerAnimationPlayer.Stop(npc);
            DestroyDummy(npc);
        });

        Delay(Npc.SpawnSetRoleDelay + 0.1f, () =>
        {
            if (!npc.IsConnected)
                return;

            // 重力と当たり判定を切り、床下からの登場や宙吊りも置けるようにする。
            npc.IsGodModeEnabled = true;
            npc.IsNoclipEnabled = true;
            npc.Position = position;
            npc.Rotation = rotation;
            onReady?.Invoke(npc);
        });

        return npc;
    }

    /// <summary>
    /// 役者のダミーを出し、カメラが動き出すと同時に <paramref name="clip"/> を演じさせます。
    /// </summary>
    /// <param name="role">null ならクリップに残っている役職 (無ければ ClassD) です。</param>
    /// <param name="origin">
    /// 演じる起点です。指定すると動きをこの位置と <paramref name="facing"/> の向きへ置き直し (Relative)、
    /// null なら記録した場所でそのまま演じます。
    /// </param>
    public Npc SpawnActor(
        string name,
        FilmmakerAnimationClip clip,
        Vector3? origin = null,
        Quaternion? facing = null,
        RoleTypeId? role = null,
        FilmmakerActorOptions options = null,
        Action<Npc> onReady = null)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        clip.Normalize();
        options ??= new FilmmakerActorOptions();
        if (origin.HasValue)
            options.Mode = FilmmakerPlaybackMode.Relative;

        clip.TryGetFirstPosition(out Vector3 first);
        clip.TryGetFirstRotation(out Quaternion firstRotation);
        Vector3 position = origin ?? first;
        Quaternion rotation = facing ?? Quaternion.Euler(0f, firstRotation.eulerAngles.y, 0f);

        return SpawnDummy(name, role ?? clip.ActorRole ?? RoleTypeId.ClassD, position, rotation, npc =>
        {
            // 役者は歩きの体勢を見せたいので、浮いた姿勢になる Noclip は切る。
            npc.IsNoclipEnabled = false;
            onReady?.Invoke(npc);

            WhenStarted(() =>
            {
                if (npc.IsConnected && !FilmmakerActor.TryPlay(npc, clip, options, out string error))
                    Log.Warn($"[FilmmakerCutscene] 役者 {npc.Nickname} を動かせませんでした: {error}");
            });
        });
    }

    /// <inheritdoc cref="SpawnActor(string, FilmmakerAnimationClip, Vector3?, Quaternion?, RoleTypeId?, FilmmakerActorOptions, Action{Npc})"/>
    public Npc SpawnActor(
        string name,
        string animationName,
        Vector3? origin = null,
        Quaternion? facing = null,
        RoleTypeId? role = null,
        FilmmakerActorOptions options = null,
        Action<Npc> onReady = null)
    {
        if (!FilmmakerAnimation.TryResolve(animationName, out FilmmakerAnimationClip clip, out string error))
        {
            Log.Warn($"[FilmmakerCutscene] 役者のクリップ '{animationName}' を用意できませんでした: {error}");
            return null;
        }

        return SpawnActor(name, clip, origin, facing, role, options, onReady);
    }

    /// <summary>
    /// ダミーにアクション (<see cref="DummyActions"/> のパス) を実行させます。見つからなければ false。
    /// </summary>
    public bool DummyAction(Npc dummy, string path)
    {
        if (DummyActions.TryInvoke(dummy, path, out string error))
            return true;

        Log.Debug($"[FilmmakerCutscene] {error}");
        return false;
    }

    /// <summary>
    /// 部屋の照明を点けたり消したりします。演出の終わりに元へ戻ります。
    /// </summary>
    public void SetRoomLights(Room room, bool enabled)
    {
        if (room == null)
            return;

        Remember(room);
        room.AreLightsOff = !enabled;
    }

    /// <summary>
    /// 部屋の照明の色を変えます。演出の終わりに元へ戻ります。
    /// </summary>
    public void SetRoomColor(Room room, Color color)
    {
        if (room == null)
            return;

        Remember(room);
        room.Color = color;
    }

    /// <summary>
    /// 部屋の照明を <paramref name="seconds"/> 秒だけ消します。
    /// </summary>
    /// <remarks>終わると照明は点灯に戻ります。<see cref="SetRoomLights"/> で消した部屋には使わないでください。</remarks>
    public void FlickerRoom(Room room, float seconds)
    {
        if (room == null)
            return;

        Remember(room);
        foreach (RoomLightController controller in room.RoomLightControllers)
            controller.ServerFlickerLights(seconds);
    }

    /// <summary>
    /// <paramref name="duration"/> 秒かけて毎フレーム <paramref name="step"/> を呼びます。進み具合 (0〜1) を受け取り、最後は必ず 1 です。
    /// </summary>
    public CoroutineHandle Animate(float duration, Action<float> step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        return Track(Timing.RunCoroutine(AnimateLoop(Mathf.Max(0f, duration), step)));
    }

    /// <summary>
    /// 演出が終わるまで <paramref name="interval"/> 秒ごとに <paramref name="tick"/> を呼びます。
    /// </summary>
    public CoroutineHandle Every(float interval, Action tick)
    {
        if (tick == null)
            throw new ArgumentNullException(nameof(tick));

        return Track(Timing.RunCoroutine(EveryLoop(Mathf.Max(Timing.WaitForOneFrame, interval), tick)));
    }

    /// <summary>
    /// いまカメラがいる位置です。ダミーにカメラを目で追わせるときなどに使います。
    /// </summary>
    public bool TryGetCameraPose(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;

        if (!Clip.TryEvaluate(Elapsed, out FilmmakerAnimationSample sample) || !sample.HasPosition)
            return false;

        position = sample.Position + Vector3.up * Clip.EyeHeight;
        rotation = sample.HasRotation ? sample.Rotation : Quaternion.identity;
        return true;
    }

    private IEnumerator<float> AnimateLoop(float duration, Action<float> step)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (IsFinished)
                yield break;

            RunSafely(() => step(duration > 0f ? elapsed / duration : 1f), "Animate");
            yield return Timing.WaitForOneFrame;
            elapsed += Time.deltaTime;
        }

        if (!IsFinished)
            RunSafely(() => step(1f), "Animate");
    }

    private IEnumerator<float> EveryLoop(float interval, Action tick)
    {
        while (!IsFinished)
        {
            RunSafely(tick, "Every");
            yield return Timing.WaitForSeconds(interval);
        }
    }

    private void Remember(Room room)
    {
        if (_rooms.ContainsKey(room))
            return;

        _rooms[room] = new RoomLightingState(room.AreLightsOff, room.Color);
        OnEnd(() => RestoreRoom(room));
    }

    private void RestoreRoom(Room room)
    {
        if (!_rooms.TryGetValue(room, out RoomLightingState state))
            return;

        _rooms.Remove(room);
        if (room == null || room.RoomLightController == null)
            return;

        foreach (RoomLightController controller in room.RoomLightControllers)
            controller.ServerFlickerLights(0f);

        room.AreLightsOff = state.LightsOff;
        if (state.Color == Color.clear)
            room.ResetColor();
        else
            room.Color = state.Color;
    }

    private static void DestroyDummy(Npc npc)
    {
        if (npc == null)
            return;

        InternalNpcRegistry.Unregister(npc.Id);

        if (npc.IsConnected)
            npc.Destroy();
    }

    private readonly struct RoomLightingState(bool lightsOff, Color color)
    {
        public bool LightsOff { get; } = lightsOff;
        public Color Color { get; } = color;
    }
}
