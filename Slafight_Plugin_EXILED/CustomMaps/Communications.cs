using System.Collections.Generic;
using AdminToys;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using MEC;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps;

public static class Communications
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += Setup;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= Setup;
    }
    
    public static InteractableToy? InteractableToy { get; private set; }
    public static Text? TextToy { get; private set; }

    public static string MonitorText
    {
        get => TextToy?.TextFormat ?? string.Empty; 
        set => TextToy?.TextFormat = value;
    }

    private static readonly List<string> MonitorTexts = 
    [
        "Scanning...","Calling Mobile Task Force...","Reporting to Overseer Council...","Updating Defence Model...","ERROR: 0x55555\nDESC: UNKNOWN ERROR"
    ];

    private static void Setup()
    {
        Timing.CallDelayed(2.25f, () =>
        {
            SetupInteractable();
            SetupText();

            MonitorText = $"<size=10>{MonitorTexts.RandomItem()}</size>";
            InteractableToy?.Base.OnSearched +=
                p => Player.Get(p).ShowRueiPlus("[アクセス拒否]", 10f);
        });
    }
    
    private static void SetupInteractable()
    {
        if (MapFlags.EzcInteractablePoint == Vector3.zero) return;
        var interactableToy = InteractableToy.Create();
        interactableToy.Position = MapFlags.EzcInteractablePoint;
        interactableToy.Rotation = Quaternion.identity;
        interactableToy.Scale = Vector3.one;
        interactableToy.Shape = InvisibleInteractableToy.ColliderShape.Box;
        interactableToy.InteractionDuration = 3f;
        InteractableToy = interactableToy;
    }

    private static void SetupText()
    {
        if (MapFlags.EzcScreenPoint == Vector3.zero) return;
        var textToy = Text.Create();
        textToy.Scale = new Vector3(0.55f, 0.65f, 1f);
        textToy.Position = MapFlags.EzcScreenPoint;
        textToy.Rotation = MapFlags.EzcScreenRotation;
        textToy.Rotation = StaticUtils
            .GetWorldFromRoomLocal(RoomType.EzDownstairsPcs, Vector3.zero, new Vector3(-10f, 0f, 0f)).worldRotation;
        textToy.DisplaySize = new Vector2(120f, 20f);
        TextToy = textToy;
    }
}
