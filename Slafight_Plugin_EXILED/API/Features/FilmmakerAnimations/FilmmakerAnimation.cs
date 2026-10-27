using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

/// <summary>
/// コードで組み立てるアニメーションです。派生するだけで、クラス名で保存済みの動きと同じように再生できます。
/// </summary>
/// <remarks>
/// 再生のたびに新しいインスタンスが作られ、<see cref="Build"/> → <see cref="OnPreparing"/> → <see cref="OnStarted"/> → <see cref="OnCue"/> → <see cref="OnFinished"/> の順に呼ばれます。
/// その時点の部屋の位置などを使え、Build で決めた値をフィールドに置いて後のフックで使えます。
/// </remarks>
public abstract class FilmmakerAnimation
{
    /// <summary>
    /// クリップ名です。既定はクラス名です。
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// キーフレームを並べます。
    /// </summary>
    protected abstract void Build(FilmmakerAnimationBuilder builder);

    /// <summary>
    /// 演出として再生するとき、動き出す前に最初の構図で待つ秒数です。ダミーの準備 (約 0.6 秒) を待つ場合などに使います。
    /// </summary>
    public virtual float PrepareSeconds => 0f;

    /// <summary>
    /// 全員の差し替えが済み、最初の構図で待っている間です。ダミーや小道具はここで出します。
    /// <see cref="FilmmakerCutscene.Preparing"/> より先に呼ばれます。
    /// </summary>
    protected internal virtual void OnPreparing(FilmmakerCutscenePreparingEventArgs ev)
    {
    }

    /// <summary>
    /// カメラが動き出した直後です。<see cref="FilmmakerCutscene.Started"/> より先に呼ばれます。
    /// </summary>
    protected internal virtual void OnStarted(FilmmakerCutsceneStartedEventArgs ev)
    {
    }

    /// <summary>
    /// 合図の時刻に達したときです。
    /// </summary>
    protected internal virtual void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
    }

    /// <summary>
    /// 演出が終わったときです。<see cref="FilmmakerCutscene.OnEnd"/> に預けた片付けは済んでいます。
    /// </summary>
    protected internal virtual void OnFinished(FilmmakerCutsceneFinishedEventArgs ev)
    {
    }

    public bool TryCreateClip(out FilmmakerAnimationClip clip, out string error)
    {
        clip = null;

        try
        {
            var builder = new FilmmakerAnimationBuilder(Name);
            Build(builder);
            clip = builder.Build();
        }
        catch (Exception exception)
        {
            error = $"{GetType().Name} の組み立てに失敗しました: {exception.Message}";
            return false;
        }

        if (!clip.HasPlayableMotion)
        {
            clip = null;
            error = $"{GetType().Name} にキーフレームがありません。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// コード定義のアニメーションのクラス名一覧です。
    /// </summary>
    public static IReadOnlyList<string> GetNames()
        => [.. TypeParser.FindTypes<FilmmakerAnimation>().Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal)];

    /// <summary>
    /// 名前からクリップを用意します。コード定義 (クラス名) を先に探し、無ければ保存済みファイルを読みます。
    /// </summary>
    public static bool TryResolve(string name, out FilmmakerAnimationClip clip, out string error)
        => TryResolve(name, out clip, out _, out error);

    /// <summary>
    /// <inheritdoc cref="TryResolve(string, out FilmmakerAnimationClip, out string)"/>
    /// コード定義なら、組み立てに使ったインスタンスも返します (ファイルなら null)。
    /// </summary>
    public static bool TryResolve(
        string name,
        out FilmmakerAnimationClip clip,
        out FilmmakerAnimation animation,
        out string error)
    {
        if (TypeParser.TryCreate(name, out animation))
            return animation.TryCreateClip(out clip, out error);

        animation = null;
        return FilmmakerAnimationStorage.TryLoad(name, out clip, out error);
    }
}
