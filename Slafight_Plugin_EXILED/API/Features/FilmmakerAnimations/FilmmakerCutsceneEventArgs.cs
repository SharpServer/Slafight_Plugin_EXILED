using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// 演出のイベントに共通する情報です。
/// </summary>
/// <remarks>出した小道具は <see cref="FilmmakerCutscene.OnEnd"/> に片付けを預けると、打ち切りやラウンド再開でも残りません。</remarks>
public abstract class FilmmakerCutsceneEventArgs(FilmmakerCutscene cutscene) : EventArgs
{
    public FilmmakerCutscene Cutscene { get; } = cutscene;

    public FilmmakerAnimationClip Clip => Cutscene.Clip;

    /// <summary>
    /// コード定義のアニメーションです。保存済みファイルからの再生なら null。
    /// </summary>
    public FilmmakerAnimation Animation => Cutscene.Animation;

    public string Name => Cutscene.Clip.Name;

    /// <summary>
    /// 演出を見ているプレイヤーです。
    /// </summary>
    public IReadOnlyList<Player> Players => Cutscene.Players;

    /// <summary>
    /// 実時間での長さ (秒) です。速度倍率を反映しています。
    /// </summary>
    public float Duration => Cutscene.Duration;

    /// <summary>
    /// クリップ上の経過秒です。
    /// </summary>
    public float Elapsed => Cutscene.Elapsed;
}

/// <summary>
/// 全員の差し替えが済み、最初の構図で止まっている間です。ダミーや小道具はここで出すと、動き出す前に揃います。
/// </summary>
public sealed class FilmmakerCutscenePreparingEventArgs(FilmmakerCutscene cutscene)
    : FilmmakerCutsceneEventArgs(cutscene)
{
    /// <summary>
    /// 動き出すまでの秒数です。
    /// </summary>
    public float PrepareSeconds => Cutscene.PrepareSeconds;
}

/// <summary>
/// 準備が終わり、カメラが動き出した直後です。
/// </summary>
public sealed class FilmmakerCutsceneStartedEventArgs(FilmmakerCutscene cutscene)
    : FilmmakerCutsceneEventArgs(cutscene);

/// <summary>
/// クリップに置いた合図の時刻に達したときです。
/// </summary>
public sealed class FilmmakerCutsceneCueEventArgs(FilmmakerCutscene cutscene, FilmmakerCue cue)
    : FilmmakerCutsceneEventArgs(cutscene)
{
    public string Cue { get; } = cue.Name;

    /// <summary>
    /// 合図を置いたクリップ上の秒です。
    /// </summary>
    public float CueSeconds { get; } = cue.TimeFrames / cutscene.Clip.EffectiveFrameRate;
}

/// <summary>
/// 全員を元に戻し、小道具を片付けた後です。ラウンド再開で捨てたときは起きません。
/// </summary>
public sealed class FilmmakerCutsceneFinishedEventArgs(FilmmakerCutscene cutscene, bool interrupted)
    : FilmmakerCutsceneEventArgs(cutscene)
{
    /// <summary>
    /// <see cref="FilmmakerCutscene.Stop"/> などで途中で打ち切られたかどうか。
    /// </summary>
    public bool Interrupted { get; } = interrupted;
}
