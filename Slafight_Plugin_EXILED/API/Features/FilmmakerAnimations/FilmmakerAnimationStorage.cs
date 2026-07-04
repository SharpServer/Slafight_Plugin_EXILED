using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using Newtonsoft.Json;

namespace Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;

public static class FilmmakerAnimationStorage
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static string DirectoryPath =>
        Path.Combine(Paths.Configs, "Slafight_Plugin_Exiled", "animations");

    public static string GetFilePath(string name)
        => Path.Combine(DirectoryPath, $"{name}.json");

    public static bool TrySave(FilmmakerAnimationClip clip, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;

        if (clip == null)
        {
            error = "Animation clip is null.";
            return false;
        }

        if (!TryNormalizeName(clip.Name, out string normalizedName, out error))
            return false;

        clip.Name = normalizedName;
        clip.Normalize();

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            path = Path.GetFullPath(GetFilePath(normalizedName));
            if (!IsInsideAnimationDirectory(path))
            {
                error = "Animation path escaped the animations directory.";
                return false;
            }

            string json = JsonConvert.SerializeObject(clip, JsonSettings);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryLoad(string name, out FilmmakerAnimationClip clip, out string error)
    {
        clip = null;
        error = string.Empty;

        if (!TryNormalizeName(name, out string normalizedName, out error))
            return false;

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string path = Path.GetFullPath(GetFilePath(normalizedName));
            if (!IsInsideAnimationDirectory(path))
            {
                error = "Animation path escaped the animations directory.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"Animation not found: {normalizedName}";
                return false;
            }

            clip = JsonConvert.DeserializeObject<FilmmakerAnimationClip>(File.ReadAllText(path), JsonSettings);
            if (clip == null)
            {
                error = $"Animation file is empty or invalid: {path}";
                return false;
            }

            clip.Name = normalizedName;
            clip.Normalize();
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static IReadOnlyList<string> GetAnimationNames(string filter = null)
    {
        Directory.CreateDirectory(DirectoryPath);

        return Directory.GetFiles(DirectoryPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => string.IsNullOrWhiteSpace(filter) ||
                           name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(name => name)
            .ToArray();
    }

    public static bool TryNormalizeName(string input, out string name, out string error)
    {
        name = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Animation name cannot be empty.";
            return false;
        }

        string trimmed = input.Trim();
        if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - ".json".Length);

        if (trimmed == "." || trimmed == ".." || trimmed.Contains(".."))
        {
            error = "Animation name cannot contain relative path segments.";
            return false;
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            trimmed.Contains(Path.DirectorySeparatorChar.ToString()) ||
            trimmed.Contains(Path.AltDirectorySeparatorChar.ToString()))
        {
            error = "Animation name must be a file name, not a path.";
            return false;
        }

        name = trimmed;
        return true;
    }

    private static bool IsInsideAnimationDirectory(string fullPath)
    {
        string root = Path.GetFullPath(DirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
