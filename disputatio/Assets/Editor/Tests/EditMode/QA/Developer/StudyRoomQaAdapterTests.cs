#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fungus;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Task 4: StudyRoom diary-mirror developer capabilities (grant/reset/probe/assert/capture).
/// </summary>
public class StudyRoomQaAdapterTests
{
    private static readonly string[] ExpectedCapabilityIds =
    {
        StudyRoomQaAdapter.GrantBookmarkCapabilityId,
        StudyRoomQaAdapter.ResetCapabilityId,
        StudyRoomQaAdapter.ProbeCapabilityId,
        StudyRoomQaAdapter.AssertSolvedCapabilityId,
        StudyRoomQaAdapter.CaptureCapabilityId
    };

    private GameObject flowchartObject;
    private Flowchart flowchart;
    private DeveloperQaCapabilityRegistry registry;
    private DeveloperQaService service;

    [SetUp]
    public void SetUp()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(true);

        flowchartObject = new GameObject("Variablemanager");
        flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, StudyRoomPuzzleDevTool.DiarySolvedKey, false);
        AddBooleanVariable(flowchart, StudyRoomPuzzleDevTool.HaveTutorKeyKey, false);

        registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapter.RegisterCapabilities(registry);
        service = new DeveloperQaService(registry);
    }

    [TearDown]
    public void TearDown()
    {
        DeveloperModeController.ResetTestOverrides();
        if (flowchartObject != null)
            Object.DestroyImmediate(flowchartObject);
    }

    [Test]
    public void RegisterCapabilities_MakesListCapabilitiesContainAllFiveIds()
    {
        IReadOnlyCollection<DeveloperQaCapability> listed = service.ListCapabilities();
        string[] ids = listed.Select(c => c.Id).ToArray();
        CollectionAssert.AreEquivalent(ExpectedCapabilityIds, ids);
    }

    [Test]
    public async Task CapabilityDescribe_Probe_ReturnsOk()
    {
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c-describe",
                "capability",
                "describe",
                StudyRoomQaAdapter.ProbeCapabilityId),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual(StudyRoomQaAdapter.ProbeCapabilityId, result.Data["id"]);
        Assert.AreEqual(SceneNames.StudyRoom, result.Data["scene_id"]);
    }

    [Test]
    public async Task InteractionInvoke_Unknown_ReturnsMissingCapability()
    {
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c-unknown",
                "interaction",
                "invoke",
                "studyroom.mirror.place-bookmark"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
        Assert.AreEqual("studyroom.mirror.place-bookmark", result.MissingCapabilityId);
    }

    [Test]
    public async Task InteractionInvoke_Reset_WhenDevModeBlocked_ReturnsEnvironmentBlocked()
    {
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(false);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c-reset",
                "interaction",
                "invoke",
                StudyRoomQaAdapter.ResetCapabilityId),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        Assert.IsFalse(
            string.IsNullOrEmpty(result.Message),
            "Blocked reset should explain the environment gate.");
    }

    [Test]
    public async Task StateAssert_AssertSolved_WhenDiarySolvedFalse_ReturnsAssertionFailed()
    {
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, false);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c-assert",
                "state",
                "assert",
                StudyRoomQaAdapter.AssertSolvedCapabilityId),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
        Assert.AreEqual("False", result.Data["diarySolved"]);
    }

    [Test]
    public void EvaluateAssertSolved_WithFlowchart_DiarySolvedFalse_ReturnsAssertionFailed()
    {
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, false);

        DeveloperQaResult result = StudyRoomMirrorQaHelpers.EvaluateAssertSolved(flowchart);

        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
        Assert.AreEqual("False", result.Data["diarySolved"]);
    }

    [Test]
    public void RegisterAll_IncludesStudyRoomSceneAdapter()
    {
        var sceneRegistry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(sceneRegistry);

        Assert.IsTrue(sceneRegistry.TryResolveScene(SceneNames.StudyRoom, out IQaSceneAdapter adapter));
        Assert.IsInstanceOf<StudyRoomQaAdapter>(adapter);
    }

    [Test]
    public async Task StateCapture_Probe_ReturnsOkWithProbeDataKeys()
    {
        flowchart.SetBooleanVariable(StudyRoomPuzzleDevTool.DiarySolvedKey, true);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c-probe",
                "state",
                "capture",
                StudyRoomQaAdapter.ProbeCapabilityId),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.IsTrue(result.Data.ContainsKey("diarySolved"));
        Assert.IsTrue(result.Data.ContainsKey("haveTutorKey"));
        Assert.IsTrue(result.Data.ContainsKey("hasBookmarkMirror"));
        Assert.AreEqual("True", result.Data["diarySolved"]);
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
#endif
