using Slafight_Plugin_EXILED.API.Structs;
using System;
using System.Collections.Generic;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public class TimingUtils : EventHandlerBase
{
    public static readonly List<ManagedCoroutine> ManagedCoroutines = [];

    /// <inheritdoc />
    /// <remarks>購読するイベントはありません。管理対象コルーチンの後始末だけを担います。</remarks>
    public override void UnregisterEvents()
    {
        ManagedCoroutines.ForEach(x => Timing.KillCoroutines(x.CoroutineHandle));
        ManagedCoroutines.Clear();

        base.UnregisterEvents();
    }

    public static CoroutineHandle CreateManagedCoroutine(string key, Func<bool> predicate, Action action, float returnInterval, float killTime = -1f)
    {
        var mc = new ManagedCoroutine { Key = key };
        ManagedCoroutines.Add(mc);

        mc.CoroutineHandle = Timing.RunCoroutine(Coroutine(predicate, action, returnInterval, killTime));
        return mc.CoroutineHandle;
    }

    private static IEnumerator<float> Coroutine(Func<bool> predicate, Action action, float returnInterval, float killTime)
    {
        var elapsedTime = 0f;
        while (predicate())
        {
            if (killTime > 0f && elapsedTime > killTime) yield break;
            action?.Invoke();
            elapsedTime += returnInterval;
            yield return Timing.WaitForSeconds(returnInterval);
        }
    }
}