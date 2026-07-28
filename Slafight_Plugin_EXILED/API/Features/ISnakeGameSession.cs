using System;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Common lifecycle used by interactive games rendered on a Chaos Keycard.
/// </summary>
public interface ISnakeGameSession : IDisposable
{
    ushort Serial { get; }
    int PlayerId { get; }
    bool IsRunning { get; }

    void Start();
    void HandleInput(Vector2Int direction);
    void Stop(bool restoreSnake);
}
