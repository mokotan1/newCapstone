using Mokotan.StandingDialogue;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class StandingDialogueManagerTests
{
    [TearDown]
    public void TearDown()
    {
        StandingDialogueManager.ActiveStandingDialogue = null;
        StandingDialogueManager[] all = Object.FindObjectsByType<StandingDialogueManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Object.DestroyImmediate(all[i].gameObject);
        }
    }

    [Test]
    public void Instance_IsNull_WhenNoComponents()
    {
        Assert.IsNull(StandingDialogueManager.Instance);
    }

    [Test]
    public void AfterAwake_Instance_ReturnsFirstRegistered()
    {
        var go = new GameObject(nameof(AfterAwake_Instance_ReturnsFirstRegistered));
        StandingDialogueManager mgr = go.AddComponent<StandingDialogueManager>();
        Assert.AreSame(mgr, StandingDialogueManager.Instance);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ActiveStandingDialogue_OverridesInstance()
    {
        var go1 = new GameObject("SD_A");
        var go2 = new GameObject("SD_B");
        go1.AddComponent<StandingDialogueManager>();
        StandingDialogueManager second = go2.AddComponent<StandingDialogueManager>();
        StandingDialogueManager.ActiveStandingDialogue = second;
        Assert.AreSame(second, StandingDialogueManager.Instance);
        Object.DestroyImmediate(go1);
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void ClearStandingCharacterSlots_DoesNotThrow_WhenImagesUnset()
    {
        var go = new GameObject(nameof(ClearStandingCharacterSlots_DoesNotThrow_WhenImagesUnset));
        StandingDialogueManager mgr = go.AddComponent<StandingDialogueManager>();
        Assert.DoesNotThrow(() => mgr.ClearStandingCharacterSlots());
        Object.DestroyImmediate(go);
    }
}
