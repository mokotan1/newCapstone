using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DeveloperOverlayPlayModeTests
{
    const string OverlayTypeName = "InGameDeveloperOverlay, Assembly-CSharp";

    [UnityTest]
    public IEnumerator AddingOverlay_OutsideOnGui_DoesNotLogGuiException()
    {
        LogAssert.NoUnexpectedReceived();

        var overlayType = Type.GetType(OverlayTypeName);
        Assert.IsNotNull(
            overlayType,
            $"Expected type '{OverlayTypeName}' to resolve at runtime.");

        var host = new GameObject("DeveloperOverlayTestHost");
        try
        {
            host.AddComponent(overlayType);
            yield return null;
        }
        finally
        {
            if (host != null)
                UnityEngine.Object.Destroy(host);
        }
    }
}
