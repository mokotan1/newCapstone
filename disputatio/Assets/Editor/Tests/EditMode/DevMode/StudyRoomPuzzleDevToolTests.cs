using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

public class StudyRoomPuzzleDevToolTests
{
    GameObject flowchartObject;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(true);
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();

        flowchartObject = new GameObject("Variablemanager");
        flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, StudyRoomPuzzleDevTool.DiarySolvedKey, false);
        AddBooleanVariable(flowchart, StudyRoomPuzzleDevTool.HaveTutorKeyKey, false);
    }

    [TearDown]
    public void TearDown()
    {
        StudyRoomMirrorPuzzleSuccessRouter.ResetForTests();
        DeveloperModeController.ResetTestOverrides();
        ItemRegistry.ResetCacheForTest();

        if (flowchartObject != null)
            Object.DestroyImmediate(flowchartObject);
    }

    [Test]
    public void ResetPuzzle_SetsDiarySolvedAndHaveTutorKeyFalse()
    {
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, true);
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.HaveTutorKeyKey, true);

        bool result = StudyRoomPuzzleDevTool.ResetPuzzle(flowchart);

        Assert.IsTrue(result);
        Assert.IsFalse(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey));
        Assert.IsFalse(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.HaveTutorKeyKey));
    }

    [Test]
    public void ResetPuzzle_Blocked_WhenDeveloperModeDisabled()
    {
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(false);
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, true);

        bool result = StudyRoomPuzzleDevTool.ResetPuzzle(flowchart);

        Assert.IsFalse(result);
        Assert.IsTrue(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey),
            "Reset must not mutate variables when developer mode is off.");
    }

    [Test]
    public void ForceSolve_VariablesOnly_SetsDiarySolvedTrue()
    {
        bool result = StudyRoomPuzzleDevTool.ForceSolve(
            roomController: null,
            flowchart: flowchart,
            runUnlockRouting: false);

        Assert.IsTrue(result);
        Assert.IsTrue(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey));
    }

    [Test]
    public void ForceSolve_ViaSuccessRouter_SetsDiarySolvedTrue()
    {
        // 라우터의 블록 실행을 가로채 실제 Fungus 블록 실행 부작용을 막는다.
        bool blockExecuted = false;
        StudyRoomMirrorPuzzleSuccessRouter.ExecuteBlockHandlerForTests = (fc, block) =>
        {
            blockExecuted = true;
            return true;
        };

        bool result = StudyRoomPuzzleDevTool.ForceSolve(
            roomController: null,
            flowchart: flowchart,
            runUnlockRouting: true);

        Assert.IsTrue(result);
        Assert.IsTrue(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey));
        Assert.IsTrue(blockExecuted, "Force solve should reuse the existing UnlockSuccess routing.");
    }

    [Test]
    public void ForceSolve_ViaRouter_ReturnsFalse_WhenNoFlowchartAndNoController()
    {
        // "Variablemanager" Flowchart를 제거하면 FlowchartLocator.Resolve(null)가 null을 반환하고,
        // 씬에 StudyRoomPuzzleController도 없으므로 실제 라우팅/변수 변경이 일어나지 않는다.
        Object.DestroyImmediate(flowchartObject);
        flowchartObject = null;
        flowchart = null;

        bool result = StudyRoomPuzzleDevTool.ForceSolve(
            roomController: null,
            flowchart: null,
            runUnlockRouting: true);

        Assert.IsFalse(result, "Force solve must report failure when nothing could be changed.");
    }

    [Test]
    public void ForceSolve_Blocked_WhenDeveloperModeDisabled()
    {
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(false);

        bool result = StudyRoomPuzzleDevTool.ForceSolve(
            roomController: null,
            flowchart: flowchart,
            runUnlockRouting: false);

        Assert.IsFalse(result);
        Assert.IsFalse(flowchart.GetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey));
    }

    [Test]
    public void CaptureDebugInfo_ReflectsFlowchartValuesAndScene()
    {
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, true);
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.HaveTutorKeyKey, false);

        StudyRoomPuzzleDebugInfo info =
            StudyRoomPuzzleDevTool.CaptureDebugInfo(flowchart, SceneNames.StudyRoom);

        Assert.IsTrue(info.IsStudyRoomScene);
        Assert.IsTrue(info.DiarySolved);
        Assert.IsFalse(info.HaveTutorKey);
        Assert.IsFalse(info.HasBookmarkMirror, "No inventory in this harness.");
        Assert.IsFalse(info.HasPlacement, "No mirror puzzle controller in this harness.");
    }

    [Test]
    public void CaptureDebugInfo_NonStudyRoomScene_FlagsSceneFalse()
    {
        StudyRoomPuzzleDebugInfo info =
            StudyRoomPuzzleDevTool.CaptureDebugInfo(flowchart, SceneNames.Kitchen);

        Assert.IsFalse(info.IsStudyRoomScene);
    }

    [Test]
    public void ResolveBookmarkMirrorItem_FindsBookmarkMirrorFromCatalog()
    {
        Item mirror = StudyRoomPuzzleDevTool.ResolveBookmarkMirrorItem();

        Assert.IsNotNull(mirror, "BookmarkMirror should be resolvable from the production item catalog.");
        Assert.AreEqual(StudyRoomPuzzleDevTool.BookmarkMirrorItemName, mirror.itemName);
    }

    [Test]
    public void GrantBookmarkMirror_Blocked_WhenDeveloperModeDisabled()
    {
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(false);

        DeveloperModeItemSelectionGrantResult result = StudyRoomPuzzleDevTool.GrantBookmarkMirror();

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Succeeded);
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
