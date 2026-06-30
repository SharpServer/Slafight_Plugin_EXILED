using System;
using System.Collections.Generic;
using MEC;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.Extensions;

public class TimingUtils : IBootstrapHandler
{
    public struct ManagedCoroutine
    {
        public CoroutineHandle CoroutineHandle;
        public string Key;
    }

    private static TimingUtils? _instance;

    public static readonly List<ManagedCoroutine> ManagedCoroutines = [];

    public static void Register()
    {
        _instance = new TimingUtils();
    }

    public static void Unregister()
    {
        ManagedCoroutines.ForEach(x => Timing.KillCoroutines(x.CoroutineHandle));
        ManagedCoroutines.Clear();
        _instance = null;
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