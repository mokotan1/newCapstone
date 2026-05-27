using System.Collections;
using Fungus;
using UnityEngine;

public sealed class DeferredClickCleanup : MonoBehaviour
{
    public static void Run(Flowchart flowchart, bool resetWindowClicked = true)
    {
        GameObject runnerObject = new GameObject("DeferredClickCleanup");
        DeferredClickCleanup runner = runnerObject.AddComponent<DeferredClickCleanup>();
        runner.StartCoroutine(runner.CleanupThenDestroy(flowchart, resetWindowClicked));
    }

    private IEnumerator CleanupThenDestroy(Flowchart flowchart, bool resetWindowClicked)
    {
        yield return null;
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked);
        yield return null;
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked);
        Destroy(gameObject);
    }
}
