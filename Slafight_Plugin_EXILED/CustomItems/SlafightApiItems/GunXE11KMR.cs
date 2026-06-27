#nullable enable
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using MEC;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunXE11KMR : CItemHybrid
{
    public override string DisplayName => "XE-11K MR";
    public override string Description => 
        "<size=22>財団が開発した次世代兵装の試作型。\n" +
        "米軍のOICW計画や韓国のK-11複合小銃に触発され、\n" +
        "E-11をベースにライフルと弾倉式グレネードランチャーを合体させたマルチウェポンとなっている。\n" +
        "実際に試作がなされ実用に足る性能を発揮したものの、量産を前にして予算が尽きてしまい、\n" +
        "現在では数丁作られた試作品を一部の部隊が運用するに留まっている。</size>";
    protected override string UniqueKey => "GunXE11KMR";

    protected override List<CItemHybridMode> BuildSubModes()
        => [new(new GunXE11KMR_Normal(), "通常ライフル"), new(new GunXE11KMR_GL(), "グレネードランチャー")];
}

public class GunXE11KMR_Normal : CItemWeapon
{
    public override string DisplayName => "XE-11K MR - Normal Mode";
    public override string Description => "";
    protected override string UniqueKey => "GunXE11KMR_Normal";
    protected override ItemType BaseItem => ItemType.GunE11SR;
    protected override ushort MaxMagazineAmmo => 40;
    protected override bool AllowAttachmentChanges => false;
    protected override AttachmentName[] Attachments =>
    [
        AttachmentName.RifleBody,
        AttachmentName.SoundSuppressor,
        AttachmentName.AmmoCounter,
        AttachmentName.Laser,
        AttachmentName.NightVisionSight,
        AttachmentName.RecoilReducingStock,
        AttachmentName.StandardMagFMJ
    ];
}

public class GunXE11KMR_GL : CItemWeapon
{
    public override string DisplayName => "XE-11K MR - GL Mode";
    public override string Description => "";
    protected override string UniqueKey => "GunXE11KMR_GL";
    protected override ItemType BaseItem => ItemType.GunFRMG0;
    protected override ushort MaxMagazineAmmo => 4;
    protected override int ReloadAmmoMultiplier => 10;
    protected override bool AllowAttachmentChanges => false;
    protected override AttachmentName[] Attachments => 
    [
        AttachmentName.MuzzleBrake,
        AttachmentName.Laser,
        AttachmentName.NightVisionSight,
        AttachmentName.HeavyStock,
        AttachmentName.DrumMagAP
    ];

    private static readonly Dictionary<Player, int> CooldownStats = [];
    private static CoroutineHandle _handle;

    protected override void OnWaitingForPlayers()
    {
        Timing.KillCoroutines(_handle);
        CooldownStats.Clear();
        base.OnWaitingForPlayers();
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null) return;
        ev.Player.Explode(ProjectileType.FragGrenade);
        if (ev.Player.IsDead) ev.Attacker?.ShowHitMarker(1.5f);
        base.OnHurtingOthers(ev);
    }

    protected override void OnShooting(ShootingEventArgs ev)
    {
        if (CooldownStats.TryGetValue(ev.Player, out var cooldown) && cooldown > 0)
        {
            ev.IsAllowed = false;
        }
        base.OnShooting(ev);
    }
    
    protected override void OnShot(ShotEventArgs ev)
    {
        CooldownStats[ev.Player] = 5;
        base.OnShot(ev);
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        base.UnregisterEvents();
    }

    private static void OnRoundStarted()
    {
        _handle = Timing.RunCoroutine(Coroutine());
    }

    private static IEnumerator<float> Coroutine()
    {
        while (true)
        {
            if (Round.IsLobby) yield break;
            foreach (var pair in CooldownStats.ToList())
            {
                if (pair.Value > 0)
                    CooldownStats[pair.Key] = pair.Value - 1;
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }
}