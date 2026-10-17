using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 部屋ライト色を Lerp で滑らかに遷移させる。直接 <c>room.Color = x</c> する代わりに使う。
/// 同じ部屋に再度指定した場合は、その時点の色から新しい目標へ繋ぎ直す。
/// </summary>
public static class RoomColorFader
{
    public const float DefaultDuration = 2f;
    private const float StepInterval = 0.1f;

    private sealed class Fade
    {
        public Color From;
        public Color To;
        public float Elapsed;
        public float Duration;
    }

    private static readonly Dictionary<Room, Fade> Fades = [];
    private static CoroutineHandle _handle;

    /// <summary>部屋色を <paramref name="duration"/> 秒かけて目標色へ変化させる。0 以下なら即時。</summary>
    public static void FadeColor(this Room room, Color target, float duration = DefaultDuration)
    {
        if (room == null)
            return;

        if (duration <= 0f)
        {
            Fades.Remove(room);
            room.Color = target;
            return;
        }

        Fades[room] = new Fade { From = room.Color, To = target, Duration = duration };

        if (!_handle.IsRunning)
        {
            // ラウンド跨ぎでコルーチンが強制終了された場合の取り残しを捨てる。
            var current = Fades[room];
            Fades.Clear();
            Fades[room] = current;
            _handle = RoundScopedCoroutines.Run(FadeCoroutine());
        }
    }

    /// <summary>進行中の遷移を破棄する(色自体は変更しない)。</summary>
    public static void Cancel(Room room)
    {
        if (room != null)
            Fades.Remove(room);
    }

    private static IEnumerator<float> FadeCoroutine()
    {
        while (Fades.Count > 0)
        {
            yield return Timing.WaitForSeconds(StepInterval);

            foreach (var pair in Fades.ToList())
            {
                var room = pair.Key;
                var fade = pair.Value;
                if (room == null)
                {
                    Fades.Remove(room);
                    continue;
                }

                fade.Elapsed += StepInterval;
                var t = Mathf.Clamp01(fade.Elapsed / fade.Duration);
                room.Color = Color.Lerp(fade.From, fade.To, t);

                if (t >= 1f)
                    Fades.Remove(room);
            }
        }
    }
}
