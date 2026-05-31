using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Hazards;
using Exiled.Events.EventArgs.Scp330;
using InventorySystem.Items.Usables.Scp330;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Changes;

public struct CandyChanceStruct
{
    public float MostRareChance;
    public CandyKindID MostRareCandy;
    public float RareCandiesChance;
    public List<CandyKindID> RareCandies;
    public List<CandyKindID> NormalCandies;
}
public static class CandyChanges
{
    public static void Register()
    {
        Exiled.Events.Handlers.Scp330.InteractingScp330 += CandyRoll;
        Exiled.Events.Handlers.Scp330.EatenScp330 += SpecialCandiesEffect;
        
        Init();
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Scp330.InteractingScp330 -= CandyRoll;
        Exiled.Events.Handlers.Scp330.EatenScp330 -= SpecialCandiesEffect;
    }

    public static Dictionary<string, CandyChanceStruct> CandyChances { get; private set; } = [];

    public static string SelectedDictionary { get; private set; } = string.Empty;

    public static void Init()
    {
        CandyChances.Clear();
        CandyChances.Add("Default", new CandyChanceStruct()
        {
            MostRareCandy = CandyKindID.Pink,
            MostRareChance = 0.1f,
            RareCandiesChance = 0.22f,
            RareCandies =
            [
                CandyKindID.Black,
                CandyKindID.Brown,
                CandyKindID.Gray,
                CandyKindID.Orange,
                CandyKindID.White,
                CandyKindID.Evil
            ],
            NormalCandies =
            [
                CandyKindID.Red,
                CandyKindID.Blue,
                CandyKindID.Green,
                CandyKindID.Purple,
                CandyKindID.Rainbow,
                CandyKindID.Yellow
            ]
        });
        SelectedDictionary = "Default";
    }

    public static bool TrySetActiveDictionary(string id, out CandyChanceStruct? selectedDictionary)
    {
        if (CandyChances.TryGetValue(id, out var result))
        {
            SelectedDictionary = id;
            selectedDictionary = result;
            return true;
        }

        selectedDictionary = null;
        return false;
    }

    public static bool TryAddDictionary(string id, CandyChanceStruct dictionary)
    {
        return CandyChances.TryAdd(id, dictionary);
    }

    private static void CandyRoll(InteractingScp330EventArgs ev)
    {
        if (ev.Player == null) return;
        var random = UnityEngine.Random.Range(0f, 1f);
        var before = ev.Candy;
        CandyChances.TryGetValue(SelectedDictionary, out var result);

        if (random <= result.MostRareChance)
        {
            ev.Candy = result.MostRareCandy;
        }
        else if (random <= result.RareCandiesChance)
        {
            ev.Candy = result.RareCandies.RandomItem();
        }
        else
        {
            ev.Candy = result.NormalCandies.RandomItem();
        }

        Log.Debug($"RollPlayer: {ev.Player}, CandyRoll: {random}, Before: {before}, After: {ev.Candy}");
    }

    private static void SpecialCandiesEffect(EatenScp330EventArgs ev)
    {
        if (MapFlags.GetSeason() == SeasonTypeId.Halloween) return;
        if (ev.Player == null || ev.Candy == null) return;
        Log.Debug($"[Candy Eaten] Player: {ev.Player}, Candy: {ev.Candy}");
        if (ev.Candy.Kind == CandyKindID.Black)
        {
            // Goodbye. MASARU Black...
MeowExtensions.ShowHint(             ev.Player, "<size=24>この世全ての混沌を混ぜて煮詰めた匂いがする...</size>");
        }
        else if (ev.Candy.Kind == CandyKindID.Brown)
        {
            TantrumHazard.PlaceTantrum(ev.Player.Position);
        }
        else if (ev.Candy.Kind == CandyKindID.Gray)
        {
            ev.Player?.EnableEffect(EffectType.Slowness, 35,60f);
            ev.Player?.CustomHumeShieldStat.MaxValue = 10000f;
            ev.Player?.CustomHumeShieldStat.CurValue = 10000f;
            ev.Player?.CustomHumeShieldStat.ShieldRegenerationMultiplier = 0f;
MeowExtensions.ShowHint(             ev.Player, "<size=24>埃っぽく鉄臭い匂いが鼻を刺す...</size>");
            Timing.CallDelayed(60f, () =>
            {
                ev.Player?.CustomHumeShieldStat.MaxValue = 0f;
                ev.Player?.CustomHumeShieldStat.CurValue = 0f;
            });
        }
        else if (ev.Candy.Kind == CandyKindID.Orange)
        {
MeowExtensions.ShowHint(             ev.Player, "<size=24>眩しいほどに爽やかなオレンジの匂いがする...</size>");
            foreach (Player player in Player.List)
            {
                if (player == null || ev.Player == null) continue;
                if (player != ev.Player)
                {
                    if (Vector3.Distance(player.Position, ev.Player.Position) <= 3.25f)
                    {
                        player.Explode(ProjectileType.Flashbang,ev.Player);
                    }
                }
            }
        }
        else if (ev.Candy.Kind == CandyKindID.White)
        {
            ev.Player?.EnableEffect(EffectType.Ghostly, 40f);
            ev.Player?.EnableEffect(EffectType.MovementBoost, 20, 40f);
MeowExtensions.ShowHint(             ev.Player, "<size=24>透き通るようなミルクの香りがする...</size>");
        }
        else if (ev.Candy.Kind == CandyKindID.Evil)
        {
            ev.Player?.Role.Set(RoleTypeId.Scp0492,RoleSpawnFlags.None);
            Timing.CallDelayed(1f, () =>
            {
                ev.Player?.EnableEffect(EffectType.Scp207, 2);
                ev.Player?.UniqueRole = "Zombified";
                ev.Player?.SetCustomInfo("<color=#C50000>Zombified Subject</color>");
                ev.Player?.SetScale(new Vector3(1.08f, 1.08f, 1.08f));
                if (!Handler.CanUsePlayers.Contains(ev.Player))
                {
                    Handler.CanUsePlayers.Add(ev.Player);
                }

                if (!Handler.ActivatedPlayers.Contains(ev.Player))
                {
                    Handler.ActivatedPlayers.Add(ev.Player);
                }

MeowExtensions.ShowHint(                 ev.Player, "<size=24>冒涜的な匂いに気が狂いそうになる...</size>");
            });
        }
    }
}

