using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BasementHallwayEnterFadeTests
{
    [Test]
    public void BasementRoomEnterFade_Defaults_MatchHallwayStartFadeScreen()
    {
        var go = new GameObject("HallwayEnterFadeTest");
        try
        {
            BasementRoomEnterFade fade = go.AddComponent<BasementRoomEnterFade>();
            var so = new UnityEditor.SerializedObject(fade);
            Assert.AreEqual(
                GameplayScreenFade.BasementTransitionDurationSeconds,
                so.FindProperty("durationSeconds").floatValue);
            Assert.AreEqual(
                GameplayScreenFade.BasementDoorFadeTargetAlpha,
                so.FindProperty("targetAlpha").floatValue);
            Assert.AreEqual(1f, so.FindProperty("durationSeconds").floatValue);
            Assert.AreEqual(0f, so.FindProperty("targetAlpha").floatValue);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
