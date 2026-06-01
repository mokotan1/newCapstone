using NUnit.Framework;
using UnityEngine;

public class JumpscareGameOverLayoutTests
{
    [Test]
    public void ApplyHallPlayableRetryLayout_MatchesHallPlayerblePrefabScale()
    {
        GameObject parentGo = new GameObject("GameOver");
        GameObject retryGo = new GameObject("Retry");
        try
        {
            retryGo.transform.SetParent(parentGo.transform, false);
            parentGo.transform.localScale = JumpscareGameOverLayout.HallPlayableGameOverLocalScale;

            SpriteRenderer retryRenderer = retryGo.AddComponent<SpriteRenderer>();
            JumpscareGameOverLayout.ApplyHallPlayableRetryLayout(retryRenderer);

            Assert.AreEqual(JumpscareGameOverLayout.HallPlayableRetryLocalPosition, retryGo.transform.localPosition);
            Assert.AreEqual(JumpscareGameOverLayout.HallPlayableRetryLocalScale, retryGo.transform.localScale);
            Assert.AreEqual(new Vector3(384f, 108f, 1f), retryGo.transform.lossyScale);
        }
        finally
        {
            Object.DestroyImmediate(retryGo);
            Object.DestroyImmediate(parentGo);
        }
    }
}
