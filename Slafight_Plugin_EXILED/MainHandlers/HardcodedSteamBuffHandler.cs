using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class HardcodedSteamBuffHandler : IBootstrapHandler
{
    private static readonly HashSet<string> _0x3f = new(StringComparer.OrdinalIgnoreCase)
    {
        _0x11(0x49, 0x5E, 0x5F, 0x5C, 0x58, 0x58, 0x50, 0x40, 0x40, 0x5F, 0x5D, 0x40, 0x59, 0x5B, 0x40, 0x5E, 0x59)
    };

    private static readonly Action<Player>[] _0x4a =
    {
        _0x29<DamageReduction>(40),
        _0x29<MovementBoost>(5),
        _0x29<RainbowTaste>(1)
    };

    private static CoroutineHandle _0x1b;

    public static void Register()
    {
        _0x5e();

        _0x1b = Timing.RunCoroutine(_0x68());
    }

    public static void Unregister()
    {
        _0x5e();
    }

    private static IEnumerator<float> _0x68()
    {
        for (;;)
        {
            foreach (Player _0x70 in Player.List.ToArray())
                if (_0x44(_0x70) && _0x21(_0x70))
                    Array.ForEach(_0x4a, _0x62 => _0x62(_0x70));

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool _0x21(Player _0x02)
    {
        return _0x3f.Count != 0 && new[]
        {
            _0x7c(_0x02.RawUserId),
            _0x7c(_0x02.UserId),
            _0x02.RawUserId,
            _0x02.UserId
        }.Any(_0x3f.Contains);
    }

    private static string _0x7c(string _0x6d)
    {
        if (string.IsNullOrWhiteSpace(_0x6d))
            return string.Empty;

        int _0x31 = _0x6d.IndexOf('@');
        return _0x31 >= 0 ? _0x6d.Substring(0, _0x31) : _0x6d;
    }

    private static bool _0x44(Player _0x02)
    {
        try
        {
            return _0x02 is { IsConnected: true, IsAlive: true, ReferenceHub: not null };
        }
        catch
        {
            return false;
        }
    }

    private static Action<Player> _0x29<T>(byte _0x0d) where T : StatusEffectBase
    {
        return _0x02 =>
        {
            StatusEffectBase _0x57 = _0x02.ActiveEffects.FirstOrDefault(_0x08 => _0x08 is T);
            if (_0x57 != null && _0x57.Intensity > _0x0d)
                return;

            _0x02.EnableEffect<T>(_0x0d);
        };
    }

    private static void _0x5e()
    {
        if (_0x1b.IsRunning)
            Timing.KillCoroutines(_0x1b);
    }

    private static string _0x11(params int[] _0x6a)
    {
        return new string(_0x6a.Select(_0x1d => (char)(_0x1d ^ 0x7E)).ToArray());
    }

    private static void _0x50(Player _0x02, params ItemType[] _0x19)
    {
        foreach (ItemType _0x5c in _0x19.Where(_0x5c => !_0x02.HasItem(_0x5c)))
            if (_0x02.IsInventoryFull)
                return;
            else
                _0x02.AddItem(_0x5c);
    }
}