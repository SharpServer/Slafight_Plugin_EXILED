using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Enums;

public readonly struct CustomColor(byte r, byte g, byte b, byte a = 255)
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;
    public byte A { get; } = a;
    public Color ToUnityColor() => new(R / 255f, G / 255f, B / 255f, A / 255f);

    public static readonly CustomColor Purple = new(185, 75, 255);
    public static readonly CustomColor NinetailedBlue = new(0, 0, 180);
    public static readonly CustomColor ChaoticGreen = new(0, 75, 0);
    public static readonly CustomColor Gold = new(255, 195, 75);
}