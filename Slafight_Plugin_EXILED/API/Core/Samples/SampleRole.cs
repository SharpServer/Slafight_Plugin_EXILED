using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using VoiceChat;

namespace Slafight_Plugin_EXILED.API.Core.Samples;

/// <summary>
/// カスタム役職の書き方の見本です。
///
/// 見どころは 2 つあります。
/// <list type="number">
/// <item>
/// 装備・効果・体力・チームが、<b>処理ではなく宣言</b>で書けること。
/// スポーン時にそれを反映するのは基底の仕事です。
/// </item>
/// <item>
/// <see cref="usedCount"/> が<b>ただのフィールド</b>であること。
/// この役職はプレイヤー 1 人につき 1 インスタンス作られるので、
/// プレイヤー ID をキーにした static 辞書を用意する必要がありません。
/// </item>
/// </list>
/// </summary>
public sealed class SampleRole : CustomRole
{
    /// <summary>
    /// per-player 状態。static 辞書は要りません。
    /// </summary>
    private int tickCount;

    public override string Name => "Sample Role";

    public override string Description => "動作確認用の役職です。";

    public override CustomTeam Team => CustomTeam.Get<SampleTeam>();

    public override RoleTypeId BaseRole => RoleTypeId.ClassD;

    public override float? MaxHealth => 150f;

    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.Medkit,
        ItemType.Flashlight,
        ItemType.Radio,
    ];

    public override IReadOnlyDictionary<ItemType, ushort> Ammo =>
        new Dictionary<ItemType, ushort> { [ItemType.Ammo9x19] = 60 };

    public override IReadOnlyList<RoleEffect> Effects =>
    [
        RoleEffect.Of<MovementBoost>(intensity: 10),
    ];

    public override string CustomInfo => "Sample";

    /// <summary>
    /// 声の扱いも役職が名乗ります。配線側 (VoiceRoutingApi / ProximityChat) は触りません。
    /// </summary>
    /// <remarks>
    /// これは人間ベースの役職なので SCP チャットでは話せません。
    /// 代わりに<b>無線の声を周囲にも漏らす</b>形にしています。
    /// 流すチャンネルは、その土台の役職が実際に話せるものを指すのが要点です。
    /// </remarks>
    public override RoleVoiceSettings Voice =>
        RoleVoiceSettings.WithProximity(sourceChannel: VoiceChatChannel.Radio);

    protected override void OnSpawned()
    {
        // 能力は型で配ります。同じ能力を別の呼び名で使わせたいなら Rename を重ねます。
        AbilityBase.Give<SampleAbility>(Player);
        AbilityBase.Give<SampleChoiceAbility>(Player);

        // Scope に載せたものは、役職が外れた時点・退出・ラウンド再開のいずれでも必ず止まります。
        // コルーチンハンドルを自分で抱えて Dispose で消す、という手当ては要りません。
        Scope.RunLoop(5f, _ =>
        {
            tickCount++;
            ShowStatus($"経過: {tickCount * 5} 秒", 6f);
        });
    }
}
