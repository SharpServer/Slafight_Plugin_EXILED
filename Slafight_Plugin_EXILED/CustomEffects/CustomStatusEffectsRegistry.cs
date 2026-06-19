using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomPlayerEffects;
using Exiled.API.Features;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// Plugin assembly 内の StatusEffectBase 継承クラスを自動登録する。
/// </summary>
public static class CustomStatusEffectsRegistry
{
    private static readonly HashSet<Type> RegisteredTypes = new();
    private static bool _sceneHooked;

    /// <summary>
    /// この Registry が入っている Assembly から StatusEffectBase 継承クラスを全登録する。
    /// 通常はこれだけ呼べばOK。
    /// </summary>
    public static int AllRegister()
    {
        return AllRegister(typeof(CustomStatusEffectsRegistry).Assembly);
    }

    /// <summary>
    /// 指定 Assembly から StatusEffectBase 継承クラスを全登録する。
    /// </summary>
    public static int AllRegister(Assembly assembly)
    {
        if (assembly == null)
            return 0;

        int count = 0;

        foreach (Type type in GetLoadableTypes(assembly))
        {
            if (!IsRegisterableEffect(type))
                continue;

            if (Register(type, false))
                count++;
        }

        HookSceneLoaded();
        TryInstallIntoPlayerPrefab();
        TryInstallIntoExistingPlayers();

        Log.Info($"[CustomStatusEffectRegistry] Auto registered {count} custom status effect(s) from {assembly.GetName().Name}.");

        return count;
    }

    /// <summary>
    /// 複数 Assembly から全登録する。
    /// </summary>
    public static int AllRegister(params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            return AllRegister();

        int total = 0;

        foreach (Assembly assembly in assemblies.Where(a => a != null))
            total += AllRegister(assembly);

        return total;
    }

    /// <summary>
    /// 単体登録。
    /// </summary>
    public static bool Register<T>() where T : StatusEffectBase
    {
        bool result = Register(typeof(T), true);

        HookSceneLoaded();
        TryInstallIntoPlayerPrefab();
        TryInstallIntoExistingPlayers();

        return result;
    }

    /// <summary>
    /// 単体登録。
    /// </summary>
    public static bool Register(Type type)
    {
        bool result = Register(type, true);

        HookSceneLoaded();
        TryInstallIntoPlayerPrefab();
        TryInstallIntoExistingPlayers();

        return result;
    }

    public static void Unhook()
    {
        if (!_sceneHooked)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        _sceneHooked = false;
    }

    private static bool Register(Type type, bool installImmediately)
    {
        if (!IsRegisterableEffect(type))
            return false;

        if (!RegisteredTypes.Add(type))
            return false;

        if (installImmediately)
        {
            HookSceneLoaded();
            TryInstallIntoPlayerPrefab();
            TryInstallIntoExistingPlayers();
        }

        Log.Info($"[CustomStatusEffectRegistry] Registered type: {type.FullName}");
        return true;
    }

    private static bool IsRegisterableEffect(Type type)
    {
        return type != null &&
               typeof(StatusEffectBase).IsAssignableFrom(type) &&
               !type.IsAbstract &&
               !type.IsInterface &&
               !type.IsGenericTypeDefinition &&
               type.GetCustomAttribute<IgnoreAutoRegisterStatusEffectAttribute>() == null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
        catch (Exception ex)
        {
            Log.Error($"[CustomStatusEffectRegistry] Failed to read types from {assembly.FullName}: {ex}");
            return Array.Empty<Type>();
        }
    }

    private static void HookSceneLoaded()
    {
        if (_sceneHooked)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        _sceneHooked = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstallIntoPlayerPrefab();
        TryInstallIntoExistingPlayers();
    }

    private static void TryInstallIntoPlayerPrefab()
    {
        try
        {
            GameObject playerPrefab = NetworkManager.singleton?.playerPrefab;
            if (playerPrefab == null)
                return;

            ReferenceHub hub = playerPrefab.GetComponent<ReferenceHub>();
            if (hub == null)
                return;

            InstallIntoHub(hub, true);
        }
        catch (Exception ex)
        {
            Log.Error($"[CustomStatusEffectRegistry] Failed to install into player prefab: {ex}");
        }
    }

    private static void TryInstallIntoExistingPlayers()
    {
        foreach (Player player in Player.List)
        {
            try
            {
                if (player?.ReferenceHub == null)
                    continue;

                InstallIntoHub(player.ReferenceHub, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[CustomStatusEffectRegistry] Failed to install into {player?.Nickname ?? "unknown player"}: {ex}");
            }
        }
    }

    private static void InstallIntoHub(ReferenceHub hub, bool isPrefab)
    {
        if (hub == null || hub.playerEffectsController == null)
            return;

        GameObject effectsObject = hub.playerEffectsController.effectsGameObject;
        if (effectsObject == null)
            return;

        foreach (Type type in RegisteredTypes)
        {
            StatusEffectBase effect = GetOrCreateEffect(effectsObject, type);

            if (effect == null)
                continue;

            if (!isPrefab)
                PatchControllerCache(hub.playerEffectsController, type, effect);
        }
    }

    private static StatusEffectBase GetOrCreateEffect(GameObject effectsObject, Type type)
    {
        StatusEffectBase existing = effectsObject.GetComponentInChildren(type, true) as StatusEffectBase;
        if (existing != null)
            return existing;

        GameObject effectObject = new(type.Name);
        effectObject.transform.SetParent(effectsObject.transform, false);

        StatusEffectBase created = effectObject.AddComponent(type) as StatusEffectBase;
        return created;
    }

    /// <summary>
    /// 既に生成済みの PlayerEffectsController に後入れするため、
    /// _effectsByType / name辞書 / AllEffects系配列があれば可能な範囲で更新する。
    /// </summary>
    private static void PatchControllerCache(PlayerEffectsController controller, Type effectType, StatusEffectBase effect)
    {
        if (controller == null || effect == null)
            return;

        try
        {
            // Publicized Assembly-CSharp なら直接触れる可能性が高い。
            if (controller._effectsByType != null)
                controller._effectsByType[effectType] = effect;
        }
        catch
        {
            // EXILED / Assembly-CSharp の publicize 状態差分対策。
        }

        Type controllerType = controller.GetType();

        foreach (FieldInfo field in controllerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            try
            {
                object value = field.GetValue(controller);

                if (value == null)
                    continue;

                if (value is IDictionary<Type, StatusEffectBase> typeDictionary)
                {
                    typeDictionary[effectType] = effect;
                    continue;
                }

                if (value is IDictionary<string, StatusEffectBase> stringDictionary)
                {
                    stringDictionary[effectType.Name] = effect;

                    if (!string.IsNullOrWhiteSpace(effectType.FullName))
                        stringDictionary[effectType.FullName] = effect;

                    continue;
                }

                if (value is IList<StatusEffectBase> list)
                {
                    if (!list.Any(e => e != null && e.GetType() == effectType))
                        list.Add(effect);

                    continue;
                }

                if (field.FieldType == typeof(StatusEffectBase[]))
                {
                    StatusEffectBase[] array = (StatusEffectBase[])value;

                    if (array.Any(e => e != null && e.GetType() == effectType))
                        continue;

                    StatusEffectBase[] newArray = array.Concat(new[] { effect }).ToArray();
                    field.SetValue(controller, newArray);
                }
            }
            catch
            {
                // Controller 内部構造差分を吸収するため、個別 field の失敗は無視。
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class IgnoreAutoRegisterStatusEffectAttribute : Attribute
{
}