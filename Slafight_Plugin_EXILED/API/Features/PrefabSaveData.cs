using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Exiled.API.Enums;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

[Serializable]
public class PrefabSaveData
{
    public string PrefabType;
    public string Tag = string.Empty;

    // JSON 上は文字列
    public string RoomTypeName;

    [JsonIgnore]
    public RoomType RoomType
    {
        get
        {
            if (Enum.TryParse<RoomType>(RoomTypeName, out var rt))
                return rt;
            return RoomType.Unknown;
        }
        set => RoomTypeName = value.ToString();
    }

    public Vector3 LocalPosition;
    public Vector3 LocalRotationEuler;
    public Vector3 Scale;
    public int MaxRooms = 1;
    public float AutoDestroyTime;
    public bool AutoDestroyEnabled;

    /// <summary>
    /// サブクラス固有のオプション。Save/Load時にObjectPrefab.CollectOptions/ApplyOptionsで読み書きされる。
    /// </summary>
    public Dictionary<string, string> Options = new();
}
