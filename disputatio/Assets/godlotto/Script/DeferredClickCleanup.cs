using System.Collections;
using Fungus;
using UnityEngine;

public sealed class DeferredClickCleanup : MonoBehaviour
{
    public static void Run(Flowchart flowchart)
    {
        GameObject runnerObject = new GameObject("DeferredClickCleanup");
        DeferredClickCleanup runner = runnerObject.AddComponent<DeferredClickCleanup>();
        runner.StartCoroutine(runner.CleanupThenDestroy(flowchart));
    }

    private IEnumerator CleanupThenDestroy(Flowchart flowchart)
    {
        yield return null;
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart);
        yield return null;
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart);
        Destroy(gameObject);
    }
}
