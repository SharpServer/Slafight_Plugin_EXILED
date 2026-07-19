using System;

namespace Slafight_Plugin_EXILED.API.Features.Attributes;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class ReasonAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}