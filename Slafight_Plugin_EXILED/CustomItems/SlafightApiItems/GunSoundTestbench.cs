#nullable enable
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// 全銃器をモード切替で巡回し、鳴った音の AudioIndex / クリップ名 / 種類をログへ出す採取用アイテム。
/// </summary>
/// <remarks>
/// <para>
/// 使い方は <c>docs/internal/gun-sound-capture-checklist.md</c> を参照。
/// 音は一切差し替えないので、バニラ音を聞きながらログを取れる。
/// </para>
/// <para>
/// モードを切り替えるたびに新しい銃が手に入り、抜き出し音が鳴った時点で
/// その銃の登録クリップ一覧が 1 度だけダンプされる。切替を一周させれば全銃分そろう。
/// </para>
/// </remarks>
public class GunSoundTestbench : CItemHybrid
{
    /// <summary>採取対象の銃。<c>FirearmType</c> に対応する ItemType 全種。</summary>
    private static readonly ItemType[] Firearms =
    [
        ItemType.GunCOM15,
        ItemType.GunCOM18,
        ItemType.GunCom45,
        ItemType.GunFSP9,
        ItemType.GunCrossvec,
        ItemType.GunE11SR,
        ItemType.GunAK,
        ItemType.GunA7,
        ItemType.GunFRMG0,
        ItemType.GunLogicer,
        ItemType.GunShotgun,
        ItemType.GunRevolver,
        ItemType.GunSCP127,
        ItemType.ParticleDisruptor,
    ];

    public override string DisplayName => "Gun Sound Testbench";

    public override string Description =>
        "モード切替で全銃器を巡回する採取用アイテム。鳴った音の AudioIndex / クリップ名 / 種類がログに出る。";

    protected override string UniqueKey => "GunSoundTestbench";

    protected override List<CItemHybridMode> BuildSubModes()
        => Firearms
            .Select(firearm => new CItemHybridMode(
                new TestbenchMode(firearm),
                firearm.ToString(),
                "撃つ / 空撃ち / リロード / リロードキー長押しでアンロード、を一通り試す。"))
            .ToList();

    /// <summary>
    /// 1 銃種ぶんの採取モード。音の差し替えはせず <see cref="CItemWeapon.LogGunSoundClips"/> だけ有効にする。
    /// </summary>
    /// <remarks>
    /// <c>[CItemAutoRegisterIgnore]</c> を付けているのは、自動登録で作られる単体インスタンスに
    /// イベントを購読させないため。Hybrid が <see cref="BuildSubModes"/> で作るインスタンス側は
    /// <c>CItemHybrid.RegisterEvents</c> から購読されるので、これで二重購読を避けられる。
    /// </remarks>
    [CItemAutoRegisterIgnore]
    private sealed class TestbenchMode : CItemWeapon
    {
        private readonly ItemType _firearm;

        /// <summary>自動登録用（実際には使われない）。</summary>
        public TestbenchMode() : this(ItemType.GunCOM15) { }

        public TestbenchMode(ItemType firearm) => _firearm = firearm;

        public override string DisplayName => $"Gun Sound Testbench [{_firearm}]";

        public override string Description =>
            "鳴った音がログへ出る。差し替えは行わないのでバニラ音がそのまま聞こえる。";

        protected override string UniqueKey => "GunSoundTestbench." + _firearm;
        protected override ItemType BaseItem => _firearm;

        // 採取が目的なので差し替え定義は持たない。これだけでイベント購読が有効になる。
        protected override bool LogGunSoundClips => true;

        // サプレッサーの付け外しで発砲音の差を確認したいので変更を許可する（既定のまま明示）。
        protected override bool AllowAttachmentChanges => true;

        protected override bool PickupLightEnabled => true;
        protected override Color PickupLightColor => Color.yellow;
    }
}
