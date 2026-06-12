using NUnit.Framework;
using UnityEngine;

public class JumpscareGameOverLayoutTests
{
    [Test]
    public void FitGameOverScreen_PreservesAuthoredGameOverAndRetryTransforms()
    {
        GameObject parentGo = new GameObject("GameOver", typeof(SpriteRenderer));
        GameObject retryGo = new GameObject("Retry");
        try
        {
            retryGo.transform.SetParent(parentGo.transform, false);
            parentGo.transform.position = new Vector3(3f, 4f, 5f);
            parentGo.transform.localScale = new Vector3(0.5f, 0.75f, 1f);

            SpriteRenderer retryRenderer = retryGo.AddComponent<SpriteRenderer>();
            retryGo.transform.localPosition = new Vector3(0.25f, -0.5f, 0f);
            retryGo.transform.localScale = new Vector3(0.15f, 0.2f, 1f);
            retryRenderer.enabled = false;

            Vector3 parentPosition = parentGo.transform.position;
            Vector3 parentScale = parentGo.transform.localScale;
            Vector3 retryLocalPosition = retryGo.transform.localPosition;
            Vector3 retryLocalScale = retryGo.transform.localScale;

            JumpscareGameOverLayout.FitGameOverScreen(parentGo);

            Assert.AreEqual(parentPosition, parentGo.transform.position);
            Assert.AreEqual(parentScale, parentGo.transform.localScale);
            Assert.AreEqual(retryLocalPosition, retryGo.transform.localPosition);
            Assert.AreEqual(retryLocalScale, retryGo.transform.localScale);
            Assert.IsTrue(retryRenderer.enabled);
        }
        finally
        {
            Object.DestroyImmediate(retryGo);
            Object.DestroyImmediate(parentGo);
        }
    }
}
