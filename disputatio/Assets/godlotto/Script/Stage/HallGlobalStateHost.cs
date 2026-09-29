using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent Hall/global gameplay flags formerly owned by Variablemanager Flowchart.
/// Sole C# writer when present — not FlagStore, not Variablemanager dual-write.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class HallGlobalStateHost : MonoBehaviour
{
    public const string HostObjectName = "HallGlobalStateHost";

    static HallGlobalStateHost instance;

    readonly Dictionary<string, bool> bools = new Dictionary<string, bool>(StringComparer.Ordinal);
    readonly Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);
    readonly Dictionary<string, int> ints = new Dictionary<string, int>(StringComparer.Ordinal);

    public static HallGlobalStateHost Instance => instance;

    public static bool Exists => instance != null;

    public static HallGlobalStateHost EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject existing = GameObject.Find(HostObjectName);
        if (existing != null)
        {
            instance = existing.GetComponent<HallGlobalStateHost>() ?? existing.AddComponent<HallGlobalStateHost>();
            return instance;
        }

        var go = new GameObject(HostObjectName);
        instance = go.AddComponent<HallGlobalStateHost>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SeedDefaults();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void SeedDefaults()
    {
        foreach (string key in HallGlobalStateKeys.BooleanKeys)
        {
            if (!bools.ContainsKey(key))
                bools[key] = false;
        }

        if (!strings.ContainsKey(HallGlobalStateKeys.PrevScene))
            strings[HallGlobalStateKeys.PrevScene] = string.Empty;
        if (!ints.ContainsKey(HallGlobalStateKeys.AcquiredItemsMask))
            ints[HallGlobalStateKeys.AcquiredItemsMask] = 0;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;
        return bools.TryGetValue(key, out bool value) ? value : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        bools[key] = value;
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;
        return strings.TryGetValue(key, out string value) ? value : defaultValue;
    }

    public void SetString(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        strings[key] = value ?? string.Empty;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(key))
            return defaultValue;
        return ints.TryGetValue(key, out int value) ? value : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        ints[key] = value;
    }

    public static void ResetForTests()
    {
        if (instance != null)
        {
            DestroyImmediate(instance.gameObject);
            instance = null;
        }
    }
}

/// <summary>Keys formerly serialized on Variablemanager Flowchart.</summary>
public static class HallGlobalStateKeys
{
    public const string PrevScene = "PrevScene";
    public const string AcquiredItemsMask = "AcquiredItemsMask";
    public const string IsClicked = "isClicked";

    public static readonly string[] BooleanKeys =
    {
        "HavePrisonKey",
        "UsedPrisonKey",
        "UsedWifeKey",
        "HaveWifeKey",
        "HaveBedKey",
        "UsedBedKey",
        "HaveChildKey",
        "UsedChildKey",
        "PrisonOpen",
        "HaveBasementKey",
        "HaveTutorKey",
        "HaveStudyKey",
        "HaveMaidKey",
        "ElectricOn",
        "UsedTutorKey",
        "UsedBasementKey",
        "UsedStudyKey",
        "UsedMaidKey",
        "isClicked",
        "GetBottle",
        "pressTab",
    };
}
