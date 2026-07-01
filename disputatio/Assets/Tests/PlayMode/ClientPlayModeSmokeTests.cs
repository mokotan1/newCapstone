using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ClientPlayModeSmokeTests
{
    [UnityTest]
    public IEnumerator PlayModeStartsAndAdvancesOneFrame()
    {
        yield return null;

        Assert.IsTrue(Application.isPlaying, "PlayMode test runner should enter play mode.");
    }
}
