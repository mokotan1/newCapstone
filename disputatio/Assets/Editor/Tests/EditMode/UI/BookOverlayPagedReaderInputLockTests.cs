using NUnit.Framework;
using UnityEngine;

public class BookOverlayPagedReaderInputLockTests
{
    private GameObject readerObject;

    [TearDown]
    public void TearDown()
    {
        if (readerObject != null)
            Object.DestroyImmediate(readerObject);

        InteractionLock.ForceUnlock();
    }

    [Test]
    public void OnEnable_LocksWorldInteractionUntilOverlayCloses()
    {
        readerObject = new GameObject("BookOverlayReader");
        readerObject.SetActive(false);
        readerObject.AddComponent<BookOverlayPagedReader>();

        readerObject.SetActive(true);

        Assert.IsTrue(InteractionLock.IsLocked);
    }

    [Test]
    public void OnDisable_UnlocksWorldInteraction()
    {
        readerObject = new GameObject("BookOverlayReader");
        readerObject.SetActive(false);
        readerObject.AddComponent<BookOverlayPagedReader>();

        readerObject.SetActive(true);
        readerObject.SetActive(false);

        Assert.IsFalse(InteractionLock.IsLocked);
    }
}
