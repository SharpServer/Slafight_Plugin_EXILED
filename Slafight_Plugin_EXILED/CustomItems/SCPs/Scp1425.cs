using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SCPs;

/// <summary>
/// 第五教会の聖典です。使うたびに 1 ページずつ読み進め、3 ページ目で改宗者に、5 ページ目でマリオネットになります。
/// </summary>
public sealed class Scp1425 : CustomUsableItem
{
    // 読んだページ数は本ではなく読者が持つ。死ぬと最初から。
    private static readonly Dictionary<uint, byte> ReadCounts = [];
    private static RoundScope countsScope;

    public override string Name => "SCP-1425";
    public override string Description => "第五的な力を感じる・・・";
    public override ItemType BaseType => ItemType.Medkit;
    protected override bool SuppressVanillaEffects => true;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;
    protected override string PickupSchematicName => "Scp1425Model";

    protected override void OnOwnerDying(PlayerDyingEventArgs ev)
    {
        if (Player.Get(ev.Player?.ReferenceHub) is { } player)
            ReadCounts[player.GetNetId()] = 0;

        base.OnOwnerDying(ev);
    }

    protected override void OnCustomUse()
    {
        if (Owner is not { } reader) return;

        if (!ReferenceEquals(countsScope, RoundScope.Current))
        {
            countsScope = RoundScope.Current;
            ReadCounts.Clear();
            countsScope.OnEnd(ReadCounts.Clear);
        }

        uint netId = reader.GetNetId();
        byte count = ReadCounts.TryGetValue(netId, out byte read) ? read : (byte)0;

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
                PlayerScope.Of(reader).Delay(2.5f, player => CustomRole.Spawn<FifthistConvert>(player));
                break;
            case 3:
                reader.ShowHint("<size=22>4ページ目\n五つの欠片が緩慢に解かれてゆく</size>", 5f);
                break;
            case 4:
                reader.ShowHint($"<size=22>5ページ目\n<b><color={ServerColors.Magenta}>咆哮する黒き月は、見るに値しない幻である</color></b></size>", 5f);
                reader.EnableEffect<Flashed>(3f);
                PlayerScope.Of(reader).Delay(2.5f, player => CustomRole.Spawn<FifthistMarionette>(player));
                break;
            default:
                ReadCounts[netId] = 0;
                return;
        }

        ReadCounts[netId] = count >= 4 ? (byte)0 : (byte)(count + 1);
    }
}
