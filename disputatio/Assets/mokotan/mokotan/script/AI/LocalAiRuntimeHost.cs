using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>Reads the Supervisor session file. Does not start or kill processes.</summary>
public static class LocalAiSessionStore
{
    const string SessionIdPrefsKey = "LocalAi.SessionId";
    const string ParentStartPrefsKey = "LocalAi.ParentStart";
    const string ManualStopPrefsKey = "LocalAi.ManualStop";
    const string AutoStartPrefsKey = "LocalAi.AutoStart";

    internal static string DirectoryOverride;
    internal static bool IgnorePersistedSessionForTests;

    public static string SessionDirectory()
    {
        if (!string.IsNullOrEmpty(DirectoryOverride))
            return DirectoryOverride;
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "Disputatio", "local-ai", "sessions");
    }

    public static string CurrentSessionId()
    {
        return PlayerPrefs.GetString(SessionIdPrefsKey, "");
    }

    public static string CurrentParentStart()
    {
        return PlayerPrefs.GetString(ParentStartPrefsKey, "");
    }

    public static void RememberSession(string sessionId, string parentStart)
    {
        PlayerPrefs.SetString(SessionIdPrefsKey, sessionId ?? "");
        PlayerPrefs.SetString(ParentStartPrefsKey, parentStart ?? "");
        PlayerPrefs.Save();
    }

    public static bool AutoStartEnabled()
    {
        return PlayerPrefs.GetInt(AutoStartPrefsKey, 1) != 0;
    }

    public static bool ManualStopLatched()
    {
        return PlayerPrefs.GetInt(ManualStopPrefsKey, 0) != 0;
    }

    public static void SetManualStop(bool latched)
    {
        PlayerPrefs.SetInt(ManualStopPrefsKey, latched ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static LocalAiSessionSnapshot TryLoadCurrent()
    {
        if (IgnorePersistedSessionForTests)
            return null;
        string sessionId = CurrentSessionId();
        if (string.IsNullOrEmpty(sessionId))
            return null;
        string jsonPath = Path.Combine(SessionDirectory(), sessionId + ".json");
        if (!File.Exists(jsonPath))
            return null;
        string json;
        try
        {
            json = File.ReadAllText(jsonPath);
        }
        catch
        {
            return null;
        }

        string token = "";
        string tokenPath = Path.Combine(SessionDirectory(), sessionId + ".token");
        if (File.Exists(tokenPath))
        {
            try
            {
                token = File.ReadAllText(tokenPath).Trim();
            }
            catch
            {
                token = "";
            }
        }

        return LocalAiSessionSnapshot.TryParse(json, token, out LocalAiSessionSnapshot snapshot)
            ? snapshot
            : null;
    }
}

/// <summary>Starts the Supervisor process. Chat UI never calls this.</summary>
public static class LocalAiRuntimeHost
{
    static bool s_startRequested;

    public static void EnsureStarted(bool isBatchMode)
    {
        if (!LocalAiEndpointResolver.ShouldAutoStart(
                isBatchMode,
                LocalAiSessionStore.AutoStartEnabled(),
                LocalAiSessionStore.ManualStopLatched()))
            return;

        LocalAiSessionSnapshot existing = LocalAiSessionStore.TryLoadCurrent();
        string parentStart = LocalAiSessionStore.CurrentParentStart();
        if (string.IsNullOrEmpty(parentStart))
        {
            parentStart = DateTime.UtcNow.ToString("o");
            LocalAiSessionStore.RememberSession(
                string.IsNullOrEmpty(LocalAiSessionStore.CurrentSessionId())
                    ? Guid.NewGuid().ToString("N")
                    : LocalAiSessionStore.CurrentSessionId(),
                parentStart);
        }

        if (existing != null
            && existing.IsUsable
            && existing.SessionId == LocalAiSessionStore.CurrentSessionId()
            && existing.ParentPid == Process.GetCurrentProcess().Id
            && existing.ParentStart == parentStart)
            return;

        if (s_startRequested)
            return;
        s_startRequested = true;

        string sessionId = LocalAiSessionStore.CurrentSessionId();
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            LocalAiSessionStore.RememberSession(sessionId, parentStart);
        }

        if (!TryResolveBackendDir(out string backendDir) || !TryResolvePython(out string pythonExe))
        {
            GameLog.LogWarning("[LocalAiRuntimeHost] Python or backend_ai path missing — supervisor not started.");
            s_startRequested = false;
            return;
        }

        string[] argv = {
            "-m", "services.local_ai.supervisor",
            "--session-id", sessionId,
            "--session-dir", LocalAiSessionStore.SessionDirectory(),
            "--parent-pid", Process.GetCurrentProcess().Id.ToString(),
            "--parent-start", parentStart,
            "--project-id", "disputatio",
        };
        var start = new ProcessStartInfo
        {
            FileName = pythonExe,
            WorkingDirectory = backendDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        foreach (string part in argv)
            start.ArgumentList.Add(part);
        start.Environment["AI_PROVIDER"] = "local";
        try
        {
            Process.Start(start);
        }
        catch (Exception ex)
        {
            s_startRequested = false;
            GameLog.LogWarning("[LocalAiRuntimeHost] supervisor start failed: " + ex.Message);
        }
    }

    public static void LatchManualStop()
    {
        LocalAiSessionStore.SetManualStop(true);
    }

    public static void ClearManualStopAndRestart(bool isBatchMode)
    {
        LocalAiSessionStore.SetManualStop(false);
        s_startRequested = false;
        EnsureStarted(isBatchMode);
    }

    static bool TryResolvePython(out string pythonExe)
    {
        pythonExe = Environment.GetEnvironmentVariable("LOCAL_AI_PYTHON");
        if (!string.IsNullOrWhiteSpace(pythonExe) && File.Exists(pythonExe))
            return true;
        pythonExe = "python";
        return true;
    }

    static bool TryResolveBackendDir(out string backendDir)
    {
        backendDir = "";
        string dataPath = Application.dataPath;
        if (string.IsNullOrEmpty(dataPath))
            return false;
        string editorCandidate = Path.GetFullPath(Path.Combine(dataPath, "..", "..", "backend_ai"));
        if (Directory.Exists(editorCandidate))
        {
            backendDir = editorCandidate;
            return true;
        }

        string playerCandidate = Path.GetFullPath(Path.Combine(dataPath, "..", "backend_ai"));
        if (Directory.Exists(playerCandidate))
        {
            backendDir = playerCandidate;
            return true;
        }

        return false;
    }
}
