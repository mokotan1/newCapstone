using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class LocalAiEditorBootstrap
{
    static LocalAiEditorBootstrap()
    {
        EditorApplication.delayCall += StartAfterEditorSettles;
        EditorApplication.quitting += LatchStopOnEditorQuit;
    }

    static void StartAfterEditorSettles()
    {
        LocalAiRuntimeHost.EnsureStarted(Application.isBatchMode);
    }

    static void LatchStopOnEditorQuit()
    {
        // Parent-pid watch in Supervisor tears the job down. Do not hold Job handles here.
    }

    [MenuItem("Disputatio/Local AI/Start")]
    static void MenuStart()
    {
        LocalAiRuntimeHost.ClearManualStopAndRestart(Application.isBatchMode);
    }

    [MenuItem("Disputatio/Local AI/Stop (this session)")]
    static void MenuStop()
    {
        LocalAiRuntimeHost.LatchManualStop();
    }
}
