using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickInteractionCleanupTests
{
    private GameObject eventSystemObject;
    private GameObject selectedObject;

    [SetUp]
    public void SetUp()
    {
        eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

        selectedObject = new GameObject("SelectedUi");
        EventSystem.current.SetSelectedGameObject(selectedObject);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(selectedObject);
        Object.DestroyImmediate(eventSystemObject);
    }

    [Test]
    public void ResetAfterUiBoundary_ClearsCurrentEventSystemSelection()
    {
        ClickInteractionCleanup.ResetAfterUiBoundary();

        Assert.IsNull(EventSystem.current.currentSelectedGameObject);
    }
}
