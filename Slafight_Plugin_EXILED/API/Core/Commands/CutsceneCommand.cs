using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// プレイヤーの動きを記録・保存し、演出として再生します。
/// </summary>
public sealed class CutsceneCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "cutscene";

    public override string[] Aliases { get; } = ["cs"];

    public override string Usage =>
        "cutscene <rec <名前> [間隔] [対象]|save|cancel|filmmaker <名前>|play <名前> [対象|all] [速度]|preview <名前> [速度]|path <名前> [対象]" +
        "|act <名前> <ダミー> [rel] [loop] [速度]|actions <ダミー> [絞込]|do <ダミー> <Module/Action>|stop|list [絞込]|info <名前>>";

    public override string Description => "動きの記録と、役職を一時的に差し替える演出再生。";

    protected override bool OnExecute(out string response)
    {
        if (!TryGetArgument(0, out string action))
        {
            response = $"使い方: {Command} {Usage}\n保存先: {FilmmakerAnimationStorage.DirectoryPath}";

            return false;
        }

        switch (action.ToLowerInvariant())
        {
            case "rec":
            case "record":
                return Record(out response);

            case "save":
                return Save(out response);

            case "cancel":
                return Cancel(out response);

            case "filmmaker":
            case "film":
                return SaveFilmmaker(out response);

            case "play":
                return Play(out response);

            case "preview":
                return Preview(out response);

            case "path":
                return PathOnly(out response);

            case "act":
                return Act(out response);

            case "actions":
                return Actions(out response);

            case "do":
                return Do(out response);

            case "stop":
                return Stop(out response);

            case "list":
            case "ls":
                return List(out response);

            case "info":
                return Info(out response);

            default:
                response = $"不明な操作です: {action}\n使い方: {Command} {Usage}";

                return false;
        }
    }

    private bool Record(out string response)
    {
        if (!TryGetSenderPlayer(out response)) return false;

        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} rec <名前> [間隔秒] [対象]";

            return false;
        }

        // 間隔は省略できるので、数値でなければ対象とみなす。
        bool hasInterval = TryGetFloat(2, out float value);
        float interval = hasInterval ? value : FilmmakerAnimationRecorder.DefaultInterval;

        if (!TryGetPlayer(hasInterval ? 3 : 2, out Player target))
        {
            response = "対象が見つかりません。";

            return false;
        }

        if (!FilmmakerAnimationRecorder.TryStart(target, name, interval, out response))
            return false;

        FilmmakerAnimationRecorder.SetOwner(target, Sender);

        if (target.IsNPC)
            response += $"\nRA でこのダミーに押したアクションも記録します。止めるときは {Command} save。";

        return true;
    }

    private bool Save(out string response)
    {
        if (!TryGetSenderPlayer(out response)) return false;

        Player target = FilmmakerAnimationRecorder.FindRecordingTarget(Sender) ?? Sender;
        if (!FilmmakerAnimationRecorder.TryStop(target, out FilmmakerAnimationClip clip, out response))
            return false;

        return SaveClip(clip, out response);
    }

    private bool Cancel(out string response)
    {
        if (!TryGetSenderPlayer(out response)) return false;

        bool canceled = FilmmakerAnimationRecorder.Cancel(FilmmakerAnimationRecorder.FindRecordingTarget(Sender) ?? Sender);
        response = canceled ? "記録を破棄しました。" : "記録中ではありません。";

        return canceled;
    }

    private bool SaveFilmmaker(out string response)
    {
        if (!TryGetArgument(1, out string raw))
        {
            response = $"使い方: {Command} filmmaker <名前>";

            return false;
        }

        if (!FilmmakerAnimationStorage.TryNormalizeName(raw, out string name, out response))
            return false;

        FilmmakerAnimationClip clip = FilmmakerAnimationClip.FromCurrentTimeline(name);
        clip.CreatedBy = Sender is null ? "Server" : $"{Sender.Nickname} ({Sender.UserId})";

        if (!clip.HasPlayableMotion)
        {
            response = "サーバーの Filmmaker タイムラインに位置・回転のキーフレームがありません。";

            return false;
        }

        return SaveClip(clip, out response);
    }

    private bool Play(out string response)
    {
        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} play <名前> [対象|all] [速度]";

            return false;
        }

        IEnumerable<Player> targets = null;

        if (TryGetArgument(2, out string target) && !string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetPlayer(2, out Player player))
            {
                response = $"対象が見つかりません: {target}";

                return false;
            }

            targets = [player];
        }

        var options = new FilmmakerCutsceneOptions();

        if (TryGetFloat(3, out float speed))
            options.SpeedScale = speed;

        return FilmmakerCutscene.Play(name, targets, options, null, out response) is not null;
    }

    /// <summary>
    /// 自分だけに演出を通しで流します (役職の差し替え・小道具・後片付けを含む)。
    /// </summary>
    private bool Preview(out string response)
    {
        if (!TryGetSenderPlayer(out response)) return false;

        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} preview <名前> [速度]";

            return false;
        }

        var options = new FilmmakerCutsceneOptions();

        if (TryGetFloat(2, out float speed))
            options.SpeedScale = speed;

        return FilmmakerCutscene.Play(name, [Sender], options, null, out response) is not null;
    }

    /// <summary>
    /// 役職を変えずにカメラの軌道だけを流します。小道具や合図は動きません。
    /// </summary>
    private bool PathOnly(out string response)
    {
        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} path <名前> [対象]";

            return false;
        }

        if (!TryGetPlayer(2, out Player target))
        {
            response = "対象が見つかりません。";

            return false;
        }

        if (!FilmmakerAnimationPlayer.TryPlay(target, name, new FilmmakerPlaybackOptions(), out response))
            return false;

        response += "\n(軌道だけの確認です。役職の差し替え・ダミー・ライト・後片付けは行いません。通しは preview を使ってください。)";

        return true;
    }

    /// <summary>
    /// ダミーを役者として、クリップどおりに動かします (演出なし)。
    /// </summary>
    private bool Act(out string response)
    {
        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} act <名前> <ダミー> [rel] [loop] [速度]";

            return false;
        }

        if (!TryGetDummy(2, out Npc dummy, out response))
            return false;

        var options = new FilmmakerActorOptions();
        for (int i = 3; TryGetArgument(i, out string option); i++)
        {
            switch (option.ToLowerInvariant())
            {
                case "rel":
                case "relative":
                    options.Mode = FilmmakerPlaybackMode.Relative;
                    break;

                case "loop":
                    options.Loop = true;
                    break;

                default:
                    if (TryGetFloat(i, out float speed))
                    {
                        options.SpeedScale = speed;
                        break;
                    }

                    response = $"不明なオプションです: {option}";

                    return false;
            }
        }

        return FilmmakerActor.TryPlay(dummy, name, options, out response);
    }

    /// <summary>
    /// ダミーで今実行できるアクションを、合図やクリップに書ける形で並べます。
    /// </summary>
    private bool Actions(out string response)
    {
        if (!TryGetDummy(1, out Npc dummy, out response))
            return false;

        string filter = TryGetArgument(2, out string value) ? value : null;
        var paths = DummyActions.List(dummy)
            .Select(entry => entry.Path)
            .Where(path => filter is null || path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        response = paths.Count > 0
            ? $"{dummy.Nickname} のアクション ({paths.Count}):\n  " + string.Join("\n  ", paths)
            : $"{dummy.Nickname} に一致するアクションはありません。";

        return true;
    }

    private bool Do(out string response)
    {
        if (!TryGetDummy(1, out Npc dummy, out response))
            return false;

        string path = GetRemainder(2);
        if (string.IsNullOrWhiteSpace(path))
        {
            response = $"使い方: {Command} do <ダミー> <Module/Action>  (* が使えます)";

            return false;
        }

        if (!DummyActions.TryInvoke(dummy, path, Sender, out response))
            return false;

        response = $"{dummy.Nickname} で '{path}' を実行しました。";

        return true;
    }

    private bool TryGetDummy(int index, out Npc dummy, out string response)
    {
        dummy = null;
        response = null;

        if (!TryGetArgument(index, out string raw))
        {
            response = "ダミーを指定してください。";

            return false;
        }

        if (!TryGetPlayer(index, out Player player) || player.ReferenceHub is not { IsDummy: true } ||
            Npc.Get(player.ReferenceHub) is not { } npc)
        {
            response = $"ダミーが見つかりません: {raw}";

            return false;
        }

        dummy = npc;

        return true;
    }

    private bool Stop(out string response)
    {
        int count = FilmmakerCutscene.Active.Count;

        FilmmakerCutscene.StopAll();
        FilmmakerAnimationPlayer.StopAll();

        response = $"演出 {count} 件とプレビューを止めました。";

        return true;
    }

    private bool List(out string response)
    {
        string filter = TryGetArgument(1, out string value) ? value : null;
        List<string> names =
        [
            .. FilmmakerAnimation.GetNames()
                .Where(name => filter is null || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(name => $"{name} [コード]"),
            .. FilmmakerAnimationStorage.GetAnimationNames(filter),
        ];

        response = names.Count > 0
            ? "動き:\n  " + string.Join("\n  ", names)
            : $"動きはありません ({FilmmakerAnimationStorage.DirectoryPath})。";

        return true;
    }

    private bool Info(out string response)
    {
        if (!TryGetArgument(1, out string name))
        {
            response = $"使い方: {Command} info <名前>";

            return false;
        }

        if (!FilmmakerAnimation.TryResolve(name, out FilmmakerAnimationClip clip, out response))
            return false;

        response =
            $"'{clip.Name}'\n" +
            $"  作成: {(string.IsNullOrWhiteSpace(clip.CreatedBy) ? "不明" : clip.CreatedBy)} ({clip.CreatedUtc:yyyy-MM-dd HH:mm} UTC)\n" +
            $"  記録元: {clip.Source}\n" +
            $"  長さ: {clip.DurationSeconds:0.##}s ({clip.EffectiveFrameRate:0.##}fps)\n" +
            $"  補間: {clip.Interpolation}\n" +
            $"  キーフレーム: 位置 {clip.Positions.Count} / 回転 {clip.Rotations.Count} / 注視点 {clip.LookTargets.Count} / ズーム {clip.Zooms.Count}\n" +
            $"  役者: {clip.ActorRole?.ToString() ?? "指定なし"} / 所持品 {(clip.ActorItems.Count > 0 ? string.Join(", ", clip.ActorItems) : "なし")} / アクション {clip.Actions.Count}\n" +
            $"  合図: {(clip.Cues.Count > 0 ? string.Join(", ", clip.Cues.Select(cue => $"{cue.Name}@{cue.TimeFrames / clip.EffectiveFrameRate:0.##}s")) : "なし")}";

        return true;
    }

    private static bool SaveClip(FilmmakerAnimationClip clip, out string response)
    {
        if (!FilmmakerAnimationStorage.TrySave(clip, out string path, out response))
            return false;

        response =
            $"'{clip.Name}' を保存しました ({clip.DurationSeconds:0.##}s, " +
            $"位置 {clip.Positions.Count} / 回転 {clip.Rotations.Count})。\n{path}";

        return true;
    }

    private bool TryGetSenderPlayer(out string response)
    {
        response = Sender is null ? "ゲーム内から実行してください。" : string.Empty;

        return Sender is not null;
    }
}
