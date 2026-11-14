using System;
using System.Collections.Generic;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomItems.SCPs;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

public sealed class FifthistConvert : FifthistRole
{
    public override string Name => "<color=#ff5ffa>第五教会 改宗者</color>";
    public override string Description => "貴方は新たに第五教会に加わった。全てを第五に捧げるのです。\nSCP-1425を使って、更に第五を広めろ！";
    protected override string InfoName => "Fifthist Convert";
    public override string HudLabel => "<color=#ff5ffa>Fifthist: Convert</color>";
    public override string Objective => "あなたは第五教会の新入りだ。第五とは何かについて考え、理解し、そして従いなさい。";
    public override float? MaxHealth => 100f;
    public override IReadOnlyList<ItemType> Items => [ItemType.GunCOM18, ItemType.Medkit, ItemType.ArmorLight];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistKeycard), typeof(Scp1425)];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo => new Dictionary<ItemType, ushort>
    {
        [ItemType.Ammo762x39] = 170,
    };
}
