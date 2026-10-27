using System.Linq;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

public sealed class FilmmakerActorOptions
{
    /// <summary>
    /// <see cref="FilmmakerPlaybackMode.Relative"/> なら、ダミーの今の位置と向きを起点に動きを置き直します。
    /// </summary>
    public FilmmakerPlaybackMode Mode { get; set; } = FilmmakerPlaybackMode.Absolute;

    public float SpeedScale { get; set; } = 1f;

    public bool Loop { get; set; }

    /// <summary>
    /// クリップに残っている所持品 (<see cref="FilmmakerAnimationClip.ActorItems"/>) のうち、持っていない物を渡すかどうか。
    /// </summary>
    public bool GiveItems { get; set; } = true;
}

/// <summary>
/// ダミーを役者として、記録・組み立てたクリップどおりに動かします。位置・視点に加えて姿勢と操作も再現します。
/// </summary>
public static class FilmmakerActor
{
    public static bool TryPlay(Npc dummy, string animationName, FilmmakerActorOptions options, out string response)
    {
        if (!FilmmakerAnimation.TryResolve(animationName, out FilmmakerAnimationClip clip, out response))
            return false;

        return TryPlay(dummy, clip, options, out response);
    }

    public static bool TryPlay(Npc dummy, FilmmakerAnimationClip clip, FilmmakerActorOptions options, out string response)
    {
        options ??= new FilmmakerActorOptions();

        if (dummy?.ReferenceHub is not { IsDummy: true })
        {
            response = "役者にできるのはダミーだけです。";
            return false;
        }

        if (clip == null)
        {
            response = "Animation clip is null.";
            return false;
        }

        clip.Normalize();

        if (options.GiveItems)
        {
            foreach (ItemType item in clip.ActorItems.Distinct())
            {
                if (dummy.Items.All(owned => owned.Type != item))
                    dummy.AddItem(item);
            }
        }

        var playback = new FilmmakerPlaybackOptions
        {
            Mode = options.Mode,
            SpeedScale = options.SpeedScale,
            Loop = options.Loop,
            LockMovement = false,
            InvokeActions = true,
        };

        if (!FilmmakerAnimationPlayer.TryPlay(dummy, clip, playback, out response))
            return false;

        response = $"{dummy.Nickname} が '{clip.Name}' を演じます ({clip.DurationSeconds:0.##}s, アクション {clip.Actions.Count})。";
        return true;
    }

    public static bool Stop(Npc dummy) => FilmmakerAnimationPlayer.Stop(dummy);
}
