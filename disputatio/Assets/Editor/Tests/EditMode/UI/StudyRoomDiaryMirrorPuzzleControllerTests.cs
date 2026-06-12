using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class StudyRoomDiaryMirrorPuzzleControllerTests
{
    GameObject root;
    StudyRoomDiaryMirrorPuzzleController mirrorController;
    StudyRoomDiaryMirrorCodeView codeView;
    StudyRoomPuzzleController roomController;
    Flowchart flowchart;
    RectTransform bookOverlayRect;
    RectTransform mirrorCardRect;

    [SetUp]
    public void SetUp()
    {
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();

        root = new GameObject("DiaryMirrorPuzzleTestRoot");
        var canvasObject = new GameObject("Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(root.transform);

        bookOverlayRect = CreateRect("BookOverlay", canvasObject.transform);
        mirrorCardRect = CreateRect("FilterCardImage", bookOverlayRect);
        mirrorCardRect.gameObject.AddComponent<FilterCardBoundedDrag>();

        flowchart = root.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, "DiarySolved", false);
        AddBooleanVariable(flowchart, "HaveTutorKey", false);

        roomController = root.AddComponent<StudyRoomPuzzleController>();
        typeof(RoomInteractionController)
            .GetField("flowchart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(roomController, flowchart);

        codeView = root.AddComponent<StudyRoomDiaryMirrorCodeView>();
        mirrorController = root.AddComponent<StudyRoomDiaryMirrorPuzzleController>();
        SetPrivateField(mirrorController, "mirrorCardRect", mirrorCardRect);
        SetPrivateField(mirrorController, "bookOverlayRect", bookOverlayRect);
        SetPrivateField(mirrorController, "codeView", codeView);
        SetPrivateField(mirrorController, "roomController", roomController);
        SetPrivateField(mirrorController, "flowchart", flowchart);
        SetPrivateField(mirrorController, "targetAnchoredPosition", new Vector2(120f, 0f));
        SetPrivateField(mirrorController, "positionTolerance", 45f);
        SetPrivateField(mirrorController, "successDelaySeconds", 0f);
        SetPrivateField(mirrorController, "successInteractionId", "unlock");
        SetPrivateField(mirrorController, "solvedBoolVariableName", "DiarySolved");
        SetPrivateField(mirrorController, "setSolvedBoolBeforeSuccess", true);
        SetPrivateField(mirrorController, "preferInteractionController", true);
    }

    [TearDown]
    public void TearDown()
    {
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Evaluator_PositionSolution_ReturnsTrue()
    {
        Assert.IsTrue(StudyRoomMirrorPuzzleEvaluator.IsPositionSolution(
            new Vector2(120f, 0f),
            new Vector2(120f, 0f),
            45f));
    }

    [Test]
    public void Evaluator_WrongPosition_ReturnsFalse()
    {
        Assert.IsFalse(StudyRoomMirrorPuzzleEvaluator.IsPositionSolution(
            new Vector2(300f, 120f),
            new Vector2(120f, 0f),
            45f));
    }

    [Test]
    public void NotifyMirrorCardActivated_WhenSolutionPlacement_TriggersUnlockInteraction()
    {
        string interactionId = null;
        StudyRoomMirrorPuzzleSuccessRouter.InteractionHandlerForTests = (controller, id) => interactionId = id;

        mirrorCardRect.anchoredPosition = new Vector2(120f, 0f);
        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Assert.AreEqual("unlock", interactionId);
        Assert.IsTrue(flowchart.GetBooleanVariable("DiarySolved"));
    }

    [Test]
    public void NotifyMirrorCardActivated_WhenWrongPlacement_DoesNotTriggerUnlock()
    {
        string interactionId = null;
        StudyRoomMirrorPuzzleSuccessRouter.InteractionHandlerForTests = (controller, id) => interactionId = id;

        mirrorCardRect.anchoredPosition = new Vector2(400f, 200f);
        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Assert.IsNull(interactionId);
        Assert.IsFalse(flowchart.GetBooleanVariable("DiarySolved"));
    }

    [Test]
    public void NotifyMirrorCardActivated_BuildsHalfCodeAndMirrorOverlay_With7337Text()
    {
        mirrorCardRect.anchoredPosition = new Vector2(400f, 200f);
        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Transform puzzlePanel = bookOverlayRect.Find("DiaryMirrorPuzzlePanel");
        Assert.NotNull(puzzlePanel);
        Assert.IsTrue(puzzlePanel.gameObject.activeSelf);

        Transform halfMask = puzzlePanel.Find("HalfCodeMask");
        Assert.NotNull(halfMask);
        Assert.NotNull(halfMask.GetComponent<RectMask2D>());

        TextMeshProUGUI halfText = halfMask.Find("HalfCodeText")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(halfText);
        Assert.AreEqual("7337", halfText.text);

        Transform overlay = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay");
        Assert.NotNull(overlay);
        Assert.IsTrue(overlay.gameObject.activeSelf);

        Transform viewport = overlay.Find("MirrorViewport");
        Assert.NotNull(viewport);
        Assert.NotNull(viewport.GetComponent<RectMask2D>());

        TextMeshProUGUI fullText = viewport.Find("FullCodeText")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(fullText);
        Assert.AreEqual("7337", fullText.text);
        Assert.NotNull(viewport.Find("MirrorGlint"));
        Assert.NotNull(overlay.Find("MirrorFrame/Top"));
    }

    [Test]
    public void ShowHalfCodeClue_BeforeFilterCardDrop_BuildsVisible7337ClueOnly()
    {
        mirrorController.ShowHalfCodeClue(bookOverlayRect);

        Transform puzzlePanel = bookOverlayRect.Find("DiaryMirrorPuzzlePanel");
        Assert.NotNull(puzzlePanel);
        Assert.IsTrue(puzzlePanel.gameObject.activeSelf);

        Transform halfMask = puzzlePanel.Find("HalfCodeMask");
        Assert.NotNull(halfMask);
        Assert.NotNull(halfMask.GetComponent<RectMask2D>());

        TextMeshProUGUI halfText = halfMask.Find("HalfCodeText")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(halfText);
        Assert.AreEqual("7337", halfText.text);

        Transform overlay = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay");
        Assert.IsTrue(overlay == null || !overlay.gameObject.activeSelf);
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    static void AddBooleanVariable(Flowchart targetFlowchart, string key, bool value)
    {
        BooleanVariable variable = targetFlowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        targetFlowchart.Variables.Add(variable);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }
}
