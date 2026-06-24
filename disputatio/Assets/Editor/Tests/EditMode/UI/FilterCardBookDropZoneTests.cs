using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class FilterCardBookDropZoneTests
{
    const string BookmarkMirrorAssetPath = "Assets/godlotto/Item/BookmarkMirror.asset";
    const string FilterCardAssetPath = "Assets/godlotto/Item/FilterCard.asset";

    GameObject root;
    FilterCardBookDropZone dropZone;
    GameObject mirrorCardObject;
    StudyRoomDiaryMirrorPuzzleController mirrorController;
    Flowchart flowchart;
    Item bookmarkMirror;
    Item filterCard;

    [SetUp]
    public void SetUp()
    {
        InventorySlot.ClearDragState();
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();

        bookmarkMirror = AssetDatabase.LoadAssetAtPath<Item>(BookmarkMirrorAssetPath);
        filterCard = AssetDatabase.LoadAssetAtPath<Item>(FilterCardAssetPath);
        Assert.IsNotNull(bookmarkMirror);
        Assert.IsNotNull(filterCard);

        root = new GameObject("DropZoneTestRoot");
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var dropZoneObject = new GameObject("MirrorItemDropZone", typeof(RectTransform), typeof(Image));
        dropZoneObject.transform.SetParent(canvasObject.transform, false);
        dropZone = dropZoneObject.AddComponent<FilterCardBookDropZone>();
        dropZone.requiredItem = bookmarkMirror;
        dropZone.maxUses = 1;
        dropZone.hideRotateButtonsForDiaryMirror = true;

        var bookOverlayObject = new GameObject("BookOverlayPanelA", typeof(RectTransform));
        bookOverlayObject.transform.SetParent(dropZoneObject.transform, false);
        dropZone.bookOverlayInstance = bookOverlayObject.GetComponent<RectTransform>();

        mirrorCardObject = new GameObject("MirrorCardImage", typeof(RectTransform), typeof(Image));
        mirrorCardObject.transform.SetParent(bookOverlayObject.transform, false);
        mirrorCardObject.SetActive(false);
        dropZone.filterCardObject = mirrorCardObject;

        flowchart = root.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, "DiarySolved", false);
        AddBooleanVariable(flowchart, "HaveTutorKey", false);

        var codeView = root.AddComponent<StudyRoomDiaryMirrorCodeView>();
        mirrorController = root.AddComponent<StudyRoomDiaryMirrorPuzzleController>();
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("codeView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, codeView);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("bookOverlayRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, dropZone.bookOverlayInstance);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("flowchart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, flowchart);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("targetAnchoredPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, new Vector2(120f, 0f));
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("positionTolerance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, 45f);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("successDelaySeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, 0f);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("solvedBoolVariableName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, "DiarySolved");
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("setSolvedBoolBeforeSuccess", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, true);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("preferInteractionController", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, false);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("successFungusBlockName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, string.Empty);
        // 드롭 흐름/라우팅만 검증한다. 각도·반사 게이트는 거울 컨트롤러 전용 테스트에서 다룬다.
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("requireMirrorAngle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, false);
        typeof(StudyRoomDiaryMirrorPuzzleController)
            .GetField("requireReflection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(mirrorController, false);

        dropZone.diaryMirrorPuzzleController = mirrorController;
    }

    [TearDown]
    public void TearDown()
    {
        InventorySlot.ClearDragState();
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void OnDrop_BookmarkMirror_ActivatesMirrorCard_AndStartsPuzzle()
    {
        InventorySlot.draggedItem = bookmarkMirror;
        dropZone.consumeItemOnDrop = false;

        dropZone.OnDrop(new PointerEventData(EventSystem.current));

        Assert.IsTrue(mirrorCardObject.activeSelf, "BookmarkMirror drop should activate the mirror card image.");
        Assert.IsNull(InventorySlot.draggedItem, "Drop should clear drag state.");

        Transform overlay = mirrorCardObject.transform.Find("StudyRoomDiaryMirrorOverlay");
        Assert.IsNotNull(overlay, "Mirror puzzle overlay should be created after BookmarkMirror drop.");
        Assert.IsTrue(overlay.gameObject.activeSelf);
    }

    [Test]
    public void OnDrop_ReusableBookmarkMirror_AllowsSecondDropAfterPanelReset()
    {
        dropZone.consumeItemOnDrop = false;

        InventorySlot.draggedItem = bookmarkMirror;
        dropZone.OnDrop(new PointerEventData(EventSystem.current));
        mirrorCardObject.SetActive(false);

        InventorySlot.draggedItem = bookmarkMirror;
        dropZone.OnDrop(new PointerEventData(EventSystem.current));

        Assert.IsTrue(mirrorCardObject.activeSelf, "Reusable BookmarkMirror should be usable again after the panel is reset.");
        Assert.IsNull(InventorySlot.draggedItem, "Reusable drop should still clear drag state after a successful second drop.");
    }

    [Test]
    public void OnDrop_WrongItem_DoesNotActivateMirrorPuzzle()
    {
        InventorySlot.draggedItem = filterCard;

        dropZone.OnDrop(new PointerEventData(EventSystem.current));

        Assert.IsFalse(mirrorCardObject.activeSelf, "FilterCard must not activate the diary mirror card.");
        Assert.AreEqual(filterCard, InventorySlot.draggedItem, "Wrong-item drop should leave drag state untouched.");
        Assert.IsNull(mirrorCardObject.transform.Find("StudyRoomDiaryMirrorOverlay"));
    }

    [Test]
    public void OnDrop_BookmarkMirror_AtSolution_SetsDiarySolvedTrue()
    {
        string interactionId = null;
        StudyRoomMirrorPuzzleSuccessRouter.InteractionHandlerForTests = (controller, id) => interactionId = id;

        InventorySlot.draggedItem = bookmarkMirror;
        dropZone.OnDrop(new PointerEventData(EventSystem.current));

        var mirrorRect = mirrorCardObject.GetComponent<RectTransform>();
        mirrorRect.anchoredPosition = new Vector2(120f, 0f);
        mirrorController.EvaluateCurrentPlacementForTests();

        Assert.IsTrue(flowchart.GetBooleanVariable("DiarySolved"));
        Assert.IsNull(interactionId, "Router should set DiarySolved without requiring room controller in this harness.");
    }

    static void AddBooleanVariable(Flowchart targetFlowchart, string key, bool value)
    {
        BooleanVariable variable = targetFlowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        targetFlowchart.Variables.Add(variable);
    }
}
