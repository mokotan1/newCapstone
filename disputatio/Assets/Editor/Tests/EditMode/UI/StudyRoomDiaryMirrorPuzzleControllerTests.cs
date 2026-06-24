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
        SetPrivateField(mirrorController, "targetAnchoredPosition", new Vector2(10f, -10f));
        SetPrivateField(mirrorController, "positionTolerance", 46f);
        SetPrivateField(mirrorController, "successDelaySeconds", 0f);
        SetPrivateField(mirrorController, "successInteractionId", "unlock");
        SetPrivateField(mirrorController, "solvedBoolVariableName", "DiarySolved");
        SetPrivateField(mirrorController, "setSolvedBoolBeforeSuccess", true);
        SetPrivateField(mirrorController, "preferInteractionController", true);

        // 기본 라우팅/위치 판정 테스트는 위치만 본다. 각도·반사 게이트는 전용 테스트에서 켠다.
        SetPrivateField(mirrorController, "requireMirrorAngle", false);
        SetPrivateField(mirrorController, "requireReflection", false);
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

        mirrorCardRect.anchoredPosition = new Vector2(10f, -10f);
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
    public void NotifyMirrorCardActivated_BuildsHalfCodeAndMirrorOverlay_WithScatteredDigitPieces()
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

        Transform bookDigitField = puzzlePanel.Find("BookDigitField");
        Assert.NotNull(bookDigitField, "Book surface should use individual scattered digit pieces, not only one straight text.");

        string[] expectedGlyphs = { "7", "3", "3", "7" };
        for (int i = 0; i < expectedGlyphs.Length; i++)
        {
            TextMeshProUGUI bookPiece = bookDigitField.Find("BookDigitPiece" + i)?.GetComponent<TextMeshProUGUI>();
            Assert.NotNull(bookPiece, $"BookDigitPiece{i} should exist.");
            Assert.AreEqual(expectedGlyphs[i], bookPiece.text);
            Assert.Greater(Mathf.Abs(bookPiece.rectTransform.anchoredPosition.y), 25f,
                $"BookDigitPiece{i} should be visibly off the straight solved line.");
        }

        Transform overlay = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay");
        Assert.NotNull(overlay);
        Assert.IsTrue(overlay.gameObject.activeSelf);

        Transform viewport = overlay.Find("MirrorViewport");
        Assert.NotNull(viewport);
        Assert.NotNull(viewport.GetComponent<RectMask2D>());

        // 단일 7337 텍스트 대신 7,3,3,7 개별 조각 4개가 생성된다.
        Transform digitField = viewport.Find("DigitField");
        Assert.NotNull(digitField, "DigitField should hold the 4 number pieces.");

        for (int i = 0; i < expectedGlyphs.Length; i++)
        {
            TextMeshProUGUI piece = digitField.Find("DigitPiece" + i)?.GetComponent<TextMeshProUGUI>();
            Assert.NotNull(piece, $"DigitPiece{i} should exist.");
            Assert.AreEqual(expectedGlyphs[i], piece.text);

            float zRotation = NormalizeSignedAngle(piece.rectTransform.localEulerAngles.z);
            Assert.LessOrEqual(Mathf.Abs(zRotation), 15.001f, $"DigitPiece{i} initial tilt must be within -15..+15.");
        }

        Assert.NotNull(viewport.Find("ReflectionBeam"));
        Assert.NotNull(puzzlePanel.Find("LightSource"));
        Assert.NotNull(puzzlePanel.Find("ReflectionTarget"));
        Assert.NotNull(puzzlePanel.Find("IncomingLightBeam"));
        Assert.NotNull(puzzlePanel.Find("ReflectedLightBeam"));
        Assert.NotNull(viewport.Find("MirrorGlint"));
        Assert.NotNull(overlay.Find("MirrorFrame/Top"));
    }

    [Test]
    public void NotifyMirrorCardActivated_WhenWrongPlacement_DigitPiecesStayScatteredNotAligned()
    {
        mirrorCardRect.anchoredPosition = new Vector2(400f, 200f);
        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Transform digitField = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay/MirrorViewport/DigitField");
        Assert.NotNull(digitField);

        TextMeshProUGUI piece0 = digitField.Find("DigitPiece0")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(piece0);

        float solvedX = -129f;
        Assert.Greater(Mathf.Abs(piece0.rectTransform.anchoredPosition.x - solvedX), 25f,
            "At puzzle start, digit pieces must not sit on the solved 7337 line.");
    }

    [Test]
    public void NotifyMirrorCardActivated_RandomizesDigitScatterCoordinatesWithinPuzzleBand()
    {
        mirrorCardRect.anchoredPosition = new Vector2(400f, 200f);
        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Transform digitField = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay/MirrorViewport/DigitField");
        Assert.NotNull(digitField);

        for (int i = 0; i < 4; i++)
        {
            TextMeshProUGUI piece = digitField.Find("DigitPiece" + i)?.GetComponent<TextMeshProUGUI>();
            Assert.NotNull(piece);
            Assert.GreaterOrEqual(piece.rectTransform.anchoredPosition.x, -145f);
            Assert.LessOrEqual(piece.rectTransform.anchoredPosition.x, 145f);
            Assert.GreaterOrEqual(piece.rectTransform.anchoredPosition.y, -205f);
            Assert.LessOrEqual(piece.rectTransform.anchoredPosition.y, -105f);
            Assert.LessOrEqual(Mathf.Abs(NormalizeSignedAngle(piece.rectTransform.localEulerAngles.z)), 15.001f);
        }
    }

    [Test]
    public void CodeView_ShowScattered_RerollsDigitScatterCoordinates()
    {
        mirrorController.ShowHalfCodeClue(bookOverlayRect);
        codeView.Configure(bookOverlayRect, mirrorCardRect);
        codeView.EnsureMirrorOverlay();
        codeView.ShowScattered();

        Transform digitField = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay/MirrorViewport/DigitField");
        TextMeshProUGUI piece0 = digitField.Find("DigitPiece0")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(piece0);
        Vector2 first = piece0.rectTransform.anchoredPosition;
        float firstRotation = NormalizeSignedAngle(piece0.rectTransform.localEulerAngles.z);

        codeView.ShowSolved();
        codeView.ShowScattered();

        Vector2 second = piece0.rectTransform.anchoredPosition;
        float secondRotation = NormalizeSignedAngle(piece0.rectTransform.localEulerAngles.z);

        bool changed = Vector2.Distance(first, second) > 0.01f || Mathf.Abs(firstRotation - secondRotation) > 0.01f;
        Assert.IsTrue(changed, "Starting a new scatter state should reroll digit position or angle.");
    }

    [Test]
    public void SetReflectionIntensity_LerpsDigitPiecesTowardAlignedCode()
    {
        mirrorController.ShowHalfCodeClue(bookOverlayRect);
        codeView.Configure(bookOverlayRect, mirrorCardRect);
        codeView.EnsureMirrorOverlay();
        codeView.ShowScattered();

        Transform digitField = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay/MirrorViewport/DigitField");
        TextMeshProUGUI piece0 = digitField.Find("DigitPiece0")?.GetComponent<TextMeshProUGUI>();
        Assert.NotNull(piece0);

        codeView.SetReflectionIntensity(0f);
        Vector2 scattered = piece0.rectTransform.anchoredPosition;

        codeView.SetReflectionIntensity(1f);
        Vector2 aligned = piece0.rectTransform.anchoredPosition;

        Assert.AreNotEqual(scattered, aligned);
        Assert.Less(Mathf.Abs(aligned.x - (-129f)), 1f);
        Assert.Less(Mathf.Abs(NormalizeSignedAngle(piece0.rectTransform.localEulerAngles.z)), 0.01f);
    }

    [Test]
    public void FullSolution_WithCorrectPositionAngleAndReflection_TriggersUnlock()
    {
        string interactionId = null;
        StudyRoomMirrorPuzzleSuccessRouter.InteractionHandlerForTests = (controller, id) => interactionId = id;

        ConfigureFullReflectionPuzzle();

        // 위치·각도·반사 정답: 광원과 표식을 같은 점에 두어 반사각 오차를 0으로 만든다.
        mirrorCardRect.anchoredPosition = new Vector2(10f, -10f);
        SetPrivateField(mirrorController, "lightSourceAnchoredPosition", new Vector2(10f, 90f));
        SetPrivateField(mirrorController, "reflectionTargetAnchoredPosition", new Vector2(10f, 90f));

        var rotator = mirrorCardRect.gameObject.AddComponent<FilterCardRotator>();
        rotator.RotateRight(); // 시각 각도 90 → 아래 targetVisualAngleDegrees와 맞춘다.
        SetPrivateField(mirrorController, "targetVisualAngleDegrees", rotator.CurrentVisualAngleDegrees);

        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, rotator);

        Assert.AreEqual("unlock", interactionId);
        Assert.IsTrue(flowchart.GetBooleanVariable("DiarySolved"));
    }

    [Test]
    public void FullSolution_WhenReflectionMisses_DoesNotTrigger()
    {
        string interactionId = null;
        StudyRoomMirrorPuzzleSuccessRouter.InteractionHandlerForTests = (controller, id) => interactionId = id;

        ConfigureFullReflectionPuzzle();

        mirrorCardRect.anchoredPosition = new Vector2(10f, -10f);
        SetPrivateField(mirrorController, "lightSourceAnchoredPosition", new Vector2(-200f, 0f));
        SetPrivateField(mirrorController, "reflectionTargetAnchoredPosition", new Vector2(300f, 250f));
        SetPrivateField(mirrorController, "targetVisualAngleDegrees", 0f);

        mirrorController.NotifyMirrorCardActivated(mirrorCardRect, null);

        Assert.IsNull(interactionId);
        Assert.IsFalse(flowchart.GetBooleanVariable("DiarySolved"));
    }

    [Test]
    public void CodeView_ShowSolved_AlignsDigitPiecesToZeroRotation()
    {
        mirrorController.ShowHalfCodeClue(bookOverlayRect);
        codeView.Configure(bookOverlayRect, mirrorCardRect);
        codeView.EnsureMirrorOverlay();
        codeView.ShowSolved();

        Transform digitField = mirrorCardRect.Find("StudyRoomDiaryMirrorOverlay/MirrorViewport/DigitField");
        Assert.NotNull(digitField);

        for (int i = 0; i < 4; i++)
        {
            Transform piece = digitField.Find("DigitPiece" + i);
            Assert.NotNull(piece);
            float zRotation = NormalizeSignedAngle(piece.localEulerAngles.z);
            Assert.LessOrEqual(Mathf.Abs(zRotation), 0.01f, $"Solved DigitPiece{i} should align to 0 rotation.");
        }
    }

    void ConfigureFullReflectionPuzzle()
    {
        SetPrivateField(mirrorController, "requireMirrorAngle", true);
        SetPrivateField(mirrorController, "requireReflection", true);
        SetPrivateField(mirrorController, "targetAnchoredPosition", new Vector2(10f, -10f));
        SetPrivateField(mirrorController, "positionTolerance", 46f);
        SetPrivateField(mirrorController, "angleToleranceDegrees", 10f);
        SetPrivateField(mirrorController, "reflectionToleranceDegrees", 12f);
        SetPrivateField(mirrorController, "mirrorBaseNormal", new Vector2(1f, 0f));
    }

    static float NormalizeSignedAngle(float zDegrees)
    {
        float angle = zDegrees % 360f;
        if (angle > 180f)
            angle -= 360f;
        else if (angle < -180f)
            angle += 360f;

        return angle;
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
