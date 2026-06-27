#nullable enable
using System;
using System.Collections.Generic;
using CameraShaking;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunRecoilRampRevolver : CItemHybrid
{
    private readonly RecoilRampSharedState _sharedState = new();

    public override string DisplayName => "Recoil Ramp Revolver";

    public override string Description =>
        "発射するたびにRecoilを増減させるテスト用リボルバー。モード切替で対象パラメータと方向を変えられる。";

    protected override string UniqueKey => "GunRecoilRampRevolver";

    protected override List<CItemHybridMode> BuildSubModes()
        => new()
        {
            new(new GunRecoilRampRevolverAllPlus(_sharedState), "All +", "全Recoilパラメータを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverAnimationTimePlus(_sharedState), "AnimationTime +", "AnimationTimeを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverZAxisPlus(_sharedState), "ZAxis +", "ZAxisを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverFovKickPlus(_sharedState), "FovKick +", "FovKickを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverUpKickPlus(_sharedState), "UpKick +", "UpKickを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverSideKickPlus(_sharedState), "SideKick +", "SideKickを発射ごとに+方向へ変化させる。"),
            new(new GunRecoilRampRevolverAllMinus(_sharedState), "All -", "全Recoilパラメータを発射ごとに-方向へ変化させる。"),
            new(new GunRecoilRampRevolverAnimationTimeMinus(_sharedState), "AnimationTime -", "AnimationTimeを発射ごとに-方向へ変化させる。"),
            new(new GunRecoilRampRevolverZAxisMinus(_sharedState), "ZAxis -", "ZAxisを発射ごとに-方向へ変化させる。"),
            new(new GunRecoilRampRevolverFovKickMinus(_sharedState), "FovKick -", "FovKickを発射ごとに-方向へ変化させる。"),
            new(new GunRecoilRampRevolverUpKickMinus(_sharedState), "UpKick -", "UpKickを発射ごとに-方向へ変化させる。"),
            new(new GunRecoilRampRevolverSideKickMinus(_sharedState), "SideKick -", "SideKickを発射ごとに-方向へ変化させる。"),
        };
}

public class GunRecoilRampRevolverAllPlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverAllPlus() : base(RecoilRampTarget.All, 1) { }
    internal GunRecoilRampRevolverAllPlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.All, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_AllPlus";
}

public class GunRecoilRampRevolverAllMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverAllMinus() : base(RecoilRampTarget.All, -1) { }
    internal GunRecoilRampRevolverAllMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.All, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_AllMinus";
}

public class GunRecoilRampRevolverAnimationTimePlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverAnimationTimePlus() : base(RecoilRampTarget.AnimationTime, 1) { }
    internal GunRecoilRampRevolverAnimationTimePlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.AnimationTime, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_AnimationTimePlus";
}

public class GunRecoilRampRevolverAnimationTimeMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverAnimationTimeMinus() : base(RecoilRampTarget.AnimationTime, -1) { }
    internal GunRecoilRampRevolverAnimationTimeMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.AnimationTime, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_AnimationTimeMinus";
}

public class GunRecoilRampRevolverZAxisPlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverZAxisPlus() : base(RecoilRampTarget.ZAxis, 1) { }
    internal GunRecoilRampRevolverZAxisPlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.ZAxis, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_ZAxisPlus";
}

public class GunRecoilRampRevolverZAxisMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverZAxisMinus() : base(RecoilRampTarget.ZAxis, -1) { }
    internal GunRecoilRampRevolverZAxisMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.ZAxis, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_ZAxisMinus";
}

public class GunRecoilRampRevolverFovKickPlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverFovKickPlus() : base(RecoilRampTarget.FovKick, 1) { }
    internal GunRecoilRampRevolverFovKickPlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.FovKick, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_FovKickPlus";
}

public class GunRecoilRampRevolverFovKickMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverFovKickMinus() : base(RecoilRampTarget.FovKick, -1) { }
    internal GunRecoilRampRevolverFovKickMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.FovKick, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_FovKickMinus";
}

public class GunRecoilRampRevolverUpKickPlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverUpKickPlus() : base(RecoilRampTarget.UpKick, 1) { }
    internal GunRecoilRampRevolverUpKickPlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.UpKick, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_UpKickPlus";
}

public class GunRecoilRampRevolverUpKickMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverUpKickMinus() : base(RecoilRampTarget.UpKick, -1) { }
    internal GunRecoilRampRevolverUpKickMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.UpKick, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_UpKickMinus";
}

public class GunRecoilRampRevolverSideKickPlus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverSideKickPlus() : base(RecoilRampTarget.SideKick, 1) { }
    internal GunRecoilRampRevolverSideKickPlus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.SideKick, 1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_SideKickPlus";
}

public class GunRecoilRampRevolverSideKickMinus : GunRecoilRampRevolverModeBase
{
    public GunRecoilRampRevolverSideKickMinus() : base(RecoilRampTarget.SideKick, -1) { }
    internal GunRecoilRampRevolverSideKickMinus(RecoilRampSharedState sharedState) : base(RecoilRampTarget.SideKick, -1, sharedState) { }
    protected override string UniqueKey => "GunRecoilRampRevolver_SideKickMinus";
}

public abstract class GunRecoilRampRevolverModeBase : CItemWeapon
{
    private const float AnimationTimeStep = 0.05f;
    private const float KickStep = 50f;
    private const float MinAnimationTime = 0.01f;
    private const float MaxAnimationTime = 5f;
    private const float MaxKick = 2000f;

    private readonly RecoilRampSharedState _sharedState;
    private readonly RecoilRampTarget _target;
    private readonly int _direction;

    protected GunRecoilRampRevolverModeBase(RecoilRampTarget target, int direction)
        : this(target, direction, new RecoilRampSharedState()) { }

    internal GunRecoilRampRevolverModeBase(
        RecoilRampTarget target,
        int direction,
        RecoilRampSharedState sharedState)
    {
        _target = target;
        _direction = direction >= 0 ? 1 : -1;
        _sharedState = sharedState;
    }

    public override string DisplayName => $"Recoil Ramp Revolver [{TargetLabel} {DirectionLabel}]";

    public override string Description =>
        $"発射ごとにRecoil {TargetLabel}を{DirectionLabel}方向へ変化させるテスト用リボルバー。";

    protected override ItemType BaseItem => ItemType.GunRevolver;
    protected override float Damage => 1f;
    protected override ushort MaxMagazineAmmo => 18;
    protected override ushort InitialMagazineAmmo => 18;
    protected override bool AllowAttachmentChanges => false;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(1f, 0.25f, 0.25f);

    private string TargetLabel => _target switch
    {
        RecoilRampTarget.All => "All",
        RecoilRampTarget.AnimationTime => "AnimationTime",
        RecoilRampTarget.ZAxis => "ZAxis",
        RecoilRampTarget.FovKick => "FovKick",
        RecoilRampTarget.UpKick => "UpKick",
        RecoilRampTarget.SideKick => "SideKick",
        _ => _target.ToString(),
    };

    private string DirectionLabel => _direction > 0 ? "+" : "-";

    protected override void ApplyFirearmCustomization(Item item)
    {
        base.ApplyFirearmCustomization(item);
        ApplySharedState(item, item.Owner, restorePendingWeaponState: true);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        base.OnAcquired(ev, displayMessage);
        ApplySharedState(ev.Item, ev.Player, restorePendingWeaponState: false);
    }

    protected override object? OnCaptureState(Item item)
    {
        var weaponState = base.OnCaptureState(item);
        _sharedState.CaptureTransfer(item, weaponState);
        return null;
    }

    protected override void OnShooting(ShootingEventArgs ev)
    {
        EnsureAmmo(ev.Firearm);
        base.OnShooting(ev);
    }

    protected override void OnShot(ShotEventArgs ev)
    {
        base.OnShot(ev);

        var state = _sharedState.Register(ev.Firearm, ev.Player, ev.Firearm.Recoil);
        state.Shots++;
        state.Recoil = Clamp(Adjust(state.Recoil));
        ev.Firearm.Recoil = state.Recoil;
        EnsureAmmo(ev.Firearm);
    }

    protected override void OnModeActivated(Item item)
    {
        base.OnModeActivated(item);
        ApplySharedState(item, item.Owner, restorePendingWeaponState: true);
    }

    protected override void OnWaitingForPlayers()
    {
        _sharedState.Clear();
        base.OnWaitingForPlayers();
    }

    private void ApplySharedState(Item item, Player? owner, bool restorePendingWeaponState)
    {
        if (item is not Firearm firearm)
            return;

        var state = _sharedState.Register(item, owner, firearm.Recoil);
        if (restorePendingWeaponState && state.HasPendingWeaponState)
        {
            base.OnRestoreState(item, state.PendingWeaponState);
            state.PendingWeaponState = null;
            state.HasPendingWeaponState = false;
        }

        firearm.Recoil = state.Recoil;
        EnsureAmmo(firearm);
    }

    private static void EnsureAmmo(Firearm firearm)
    {
        firearm.MaxMagazineAmmo = Math.Max(firearm.MaxMagazineAmmo, 18);
        firearm.MagazineAmmo = firearm.MaxMagazineAmmo;
    }

    private RecoilSettings Adjust(RecoilSettings recoil)
    {
        if (_target is RecoilRampTarget.All or RecoilRampTarget.AnimationTime)
            recoil.AnimationTime += AnimationTimeStep * _direction;

        if (_target is RecoilRampTarget.All or RecoilRampTarget.ZAxis)
            recoil.ZAxis += KickStep * _direction;

        if (_target is RecoilRampTarget.All or RecoilRampTarget.FovKick)
            recoil.FovKick += KickStep * _direction;

        if (_target is RecoilRampTarget.All or RecoilRampTarget.UpKick)
            recoil.UpKick += KickStep * _direction;

        if (_target is RecoilRampTarget.All or RecoilRampTarget.SideKick)
            recoil.SideKick += KickStep * _direction;

        return recoil;
    }

    private static RecoilSettings Clamp(RecoilSettings recoil)
        => new(
            Mathf.Clamp(recoil.AnimationTime, MinAnimationTime, MaxAnimationTime),
            Mathf.Clamp(recoil.ZAxis, -MaxKick, MaxKick),
            Mathf.Clamp(recoil.FovKick, -MaxKick, MaxKick),
            Mathf.Clamp(recoil.UpKick, -MaxKick, MaxKick),
            Mathf.Clamp(recoil.SideKick, -MaxKick, MaxKick));
}

public enum RecoilRampTarget
{
    All,
    AnimationTime,
    ZAxis,
    FovKick,
    UpKick,
    SideKick,
}

internal sealed class RecoilRampSharedState
{
    private readonly Dictionary<ushort, RecoilRampRuntimeState> _statesBySerial = new();
    private readonly Dictionary<string, RecoilRampTransfer> _transfersByOwner = new();

    public RecoilRampRuntimeState Register(Item item, Player? owner, RecoilSettings fallbackRecoil)
    {
        if (_statesBySerial.TryGetValue(item.Serial, out var state))
            return state;

        string? ownerKey = GetOwnerKey(owner);
        if (ownerKey != null && _transfersByOwner.TryGetValue(ownerKey, out var transfer))
        {
            state = transfer.State.Clone();
            state.PendingWeaponState = transfer.WeaponState;
            state.HasPendingWeaponState = transfer.HasWeaponState;

            _transfersByOwner.Remove(ownerKey);
            _statesBySerial.Remove(transfer.OldSerial);
            _statesBySerial[item.Serial] = state;
            return state;
        }

        state = new RecoilRampRuntimeState
        {
            Recoil = fallbackRecoil,
            Shots = 0,
        };

        _statesBySerial[item.Serial] = state;
        return state;
    }

    public void CaptureTransfer(Item item, object? weaponState)
    {
        var fallbackRecoil = item is Firearm firearm ? firearm.Recoil : default;
        var state = Register(item, item.Owner, fallbackRecoil);
        if (item is Firearm currentFirearm)
            state.Recoil = currentFirearm.Recoil;

        string? ownerKey = GetOwnerKey(item.Owner);
        if (ownerKey == null)
            return;

        _transfersByOwner[ownerKey] = new RecoilRampTransfer(
            item.Serial,
            state.Clone(),
            weaponState,
            weaponState != null);
    }

    public void Clear()
    {
        _statesBySerial.Clear();
        _transfersByOwner.Clear();
    }

    private static string? GetOwnerKey(Player? player)
        => player == null || player == Server.Host || string.IsNullOrEmpty(player.UserId)
            ? null
            : player.UserId;
}

internal sealed class RecoilRampRuntimeState
{
    public RecoilSettings Recoil;
    public int Shots;
    public object? PendingWeaponState;
    public bool HasPendingWeaponState;

    public RecoilRampRuntimeState Clone()
        => new()
        {
            Recoil = Recoil,
            Shots = Shots,
        };
}

internal sealed record RecoilRampTransfer(
    ushort OldSerial,
    RecoilRampRuntimeState State,
    object? WeaponState,
    bool HasWeaponState);
