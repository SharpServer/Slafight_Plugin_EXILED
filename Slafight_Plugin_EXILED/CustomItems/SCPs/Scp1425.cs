using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace Slafight_Plugin_EXILED.CustomItems.SCPs;

/// <summary>
/// 第五教会の聖典です。使うたびに 1 ページずつ読み進め、3 ページ目で改宗者に、5 ページ目でマリオネットになります。
/// </summary>
public sealed class Scp1425 : CustomUsableItem
{
    // 読んだページ数は本ではなく読者が持つ。死ぬと最初から。
    private static readonly Dictionary<uint, ReadingProgress> ReadCounts = [];
    private static RoundScope countsScope;

    public override string Name => "SCP-1425";
    public override string Description => "第五的な力を感じる・・・";
    public override ItemType BaseType => ItemType.Medkit;
    protected override bool SuppressVanillaEffects => true;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;
    protected override string PickupSchematicName => "Scp1425Model";

    private static void OnDied(DiedEventArgs ev) => ReadCounts.Remove(ev.Player.GetNetId());

    private static void OnLeft(LeftEventArgs ev) => ReadCounts.Remove(ev.Player.GetNetId());

    protected override void OnCustomUse()
    {
        if (Owner is not { } reader) return;

        if (!ReferenceEquals(countsScope, RoundScope.Current))
        {
            countsScope = RoundScope.Current;
            ReadCounts.Clear();
            PlayerHandlers.Died += OnDied;
            PlayerHandlers.Left += OnLeft;
            countsScope.OnEnd(() =>
            {
                PlayerHandlers.Died -= OnDied;
                PlayerHandlers.Left -= OnLeft;
                ReadCounts.Clear();
            });
        }

        uint netId = reader.GetNetId();
        if (!ReadCounts.TryGetValue(netId, out ReadingProgress progress))
        {
            progress = new ReadingProgress();
            ReadCounts.Add(netId, progress);
        }

        byte count = progress.PagesRead;

        switch (count)
        {
            case 0:
                reader.ShowHint("<size=22>1ページ目\n壊れた星の五本の輻</size>", 5f);
                break;
            case 1:
                reader.ShowHint("<size=22>2ページ目\n永遠に争う五つの元素</size>", 5f);
                break;
            case 2:
                reader.ShowHint($"<size=22>3ページ目\n<color={ServerColors.Magenta}>精神を呼び起こす五つの感覚</color></size>", 5f);
                reader.EnableEffect<Flashed>(3f);
                ScheduleConversion<FifthistConvert>(reader, netId, progress);
                break;
            case 3:
                reader.ShowHint("<size=22>4ページ目\n五つの欠片が緩慢に解かれてゆく</size>", 5f);
                break;
            case 4:
                reader.ShowHint($"<size=22>5ページ目\n<b><color={ServerColors.Magenta}>咆哮する黒き月は、見るに値しない幻である</color></b></size>", 5f);
                reader.EnableEffect<Flashed>(3f);
                ScheduleConversion<FifthistMarionette>(reader, netId, progress);
                break;
            default:
                progress.PagesRead = 0;
                return;
        }

        progress.PagesRead = count >= 4 ? (byte)0 : (byte)(count + 1);
    }

    private static void ScheduleConversion<T>(Player reader, uint netId, ReadingProgress progress)
        where T : CustomRole, new()
    {
        PlayerScope.Of(reader).Delay(2.5f, player =>
        {
            if (ReadCounts.TryGetValue(netId, out ReadingProgress current) && ReferenceEquals(current, progress))
                CustomRole.Spawn<T>(player, RoleSpawnFlags.AssignInventory);
        });
    }

    private sealed class ReadingProgress
    {
        public byte PagesRead { get; set; }
    }
}
