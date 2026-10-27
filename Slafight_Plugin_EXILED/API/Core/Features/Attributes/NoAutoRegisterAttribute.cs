using System;

namespace Slafight_Plugin_EXILED.API.Core.Features.Attributes;

/// <summary>
/// 付けた <see cref="EventHandlerBase"/> 派生クラスを自動登録の対象から外します。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NoAutoRegisterAttribute : Attribute
{
}
