using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenarios;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Task 12 §Step 4: proves the real scenario JSON shipped under
/// <c>Resources/QA/Scenarios/2026-07/</c> validates and runs end-to-end through the real
/// <see cref="QaScenarioRunner"/>/<see cref="QaScenarioValidator"/>/<see cref="QaSceneRegistry"/>
/// pipeline -- the exact engine <c>QaCommandGateway.RunScenarioAsync</c> (qa_run) uses in
/// production -- and that the runner always restores the QA profile, releases its execution
/// lease, and finalizes evidence, whether a run ends Passed or Failed.
///
/// Assembly boundary note: the real Task 12 scene adapters (<c>MainMenuQaAdapter</c>,
/// <c>KitchenQaAdapter</c>, etc.) intentionally live in the default assembly (Assembly-CSharp) --
/// see <c>Godlotto.QA.SceneAdapters.QaSceneAdapterRegistration</c>'s remarks for why. Unity's
/// one-directional assembly reference rule (predefined assemblies may reference custom asmdefs,
/// never the other way around, even for "Tests Assemblies") means this PlayMode test assembly
/// (<c>Disputatio.PlayModeTests.asmdef</c>) cannot reference those adapter types directly.
/// So this test instead registers <see cref="FakeSceneAdapter"/> instances that mirror the real
/// adapters' scene names/target ids/preset ids exactly (same convention as
/// <c>QaScenarioRunnerTests.FakeSceneAdapter</c>) -- real scenario JSON, real validator, real
/// registry lookups, real runner; only the "does clicking actually poke the right domain
/// controller" leaf is faked here (that part is covered instead by
/// <c>InitialSceneAdapterSerializationTests</c> (EditMode) and by manually invoking
/// qa_list/qa_run via the Unity CLI against the real Editor gateway).
/// </summary>
public sealed class July15RegressionScenarioTests
{
    private const string MainMenuScene = "MainMenuScene";
    private const string KitchenScene = "Kitchen";
    private const string HallScene = "Hall_playerble";
    private const string MaidRoomScene = "MaidRoom";
    private const string TutorRoomScene = "TutorRoom";

    private static IEnumerator ToCoroutine(Task task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            throw task.Exception.GetBaseException();
        }
    }

    /// <summary>
    /// Registers one <see cref="FakeSceneAdapter"/> per Task 12 scene, declaring exactly the same
    /// scene names/target ids/preset ids the real production adapters declare (see
    /// <c>MainMenuQaAdapter</c>/<c>KitchenQaAdapter</c>/<c>HallQaAdapter</c>/
    /// <c>MaidRoomQaAdapter</c>/<c>TutorRoomQaAdapter</c>), so the real scenario JSON validates
    /// identically to how it validates against the real registry.
    /// </summary>
    private static QaSceneRegistry BuildFakeRegistryMirroringProductionAdapters()
    {
        var registry = new QaSceneRegistry();
        registry.Register(new FakeSceneAdapter(
            MainMenuScene, new[] { "mainmenu.start-button" }, Array.Empty<string>()));
        registry.Register(new FakeSceneAdapter(
            KitchenScene, new[] { "kitchen.sink.faucet", "kitchen.parret" },
            new[] { "before-faucet", "before-parret" }));
        registry.Register(new FakeSceneAdapter(
            HallScene, new[] { "hall.kitchen-entry" }, Array.Empty<string>()));
        registry.Register(new FakeSceneAdapter(
            MaidRoomScene, new[] { "maidroom.food-tray" }, Array.Empty<string>()));
        registry.Register(new FakeSceneAdapter(
            TutorRoomScene, new[] { "tutorroom.quiz-input" }, Array.Empty<string>()));
        return registry;
    }

    private static QaScenarioDefinition LoadValidatedScenarioFromRealResources(
        QaScenarioValidator validator, string scenarioId, out QaScenarioValidationResult validationResult)
    {
        TextAsset[] assets = Resources.LoadAll<TextAsset>("QA/Scenarios");
        foreach (TextAsset asset in assets)
        {
            QaScenarioValidationResult result = validator.Validate(asset.text);
            if (result.IsValid && string.Equals(result.Scenario.Id, scenarioId, StringComparison.Ordinal))
            {
                validationResult = result;
                return result.Scenario;
            }
        }

        validationResult = null;
        return null;
    }

    // -----------------------------------------------------------------------------------
    //  JSON validates against a registry shaped exactly like the real production registry
    // -----------------------------------------------------------------------------------

    [Test]
    public void AllSixScenarioJsonResources_ValidateAgainstAProductionShapedRegistry()
    {
        QaSceneRegistry registry = BuildFakeRegistryMirroringProductionAdapters();
        var validator = new QaScenarioValidator(registry);

        TextAsset[] assets = Resources.LoadAll<TextAsset>("QA/Scenarios");
        Assert.IsNotEmpty(assets, "Expected scenario JSON under Resources/QA/Scenarios/2026-07/.");

        var expectedIds = new HashSet<string>
        {
            "mainmenu.new-game-reset", "kitchen.faucet-key", "kitchen.cheshire-repeat",
            "hall.kitchen-quest", "maidroom.food-effect", "tutorroom.cheshire-quiz"
        };
        var foundIds = new HashSet<string>();

        foreach (TextAsset asset in assets)
        {
            QaScenarioValidationResult result = validator.Validate(asset.text);
            Assert.IsTrue(result.IsValid, asset.name + ": " + string.Join(" | ", result.Errors));
            foundIds.Add(result.Scenario.Id);
        }

        foreach (string expectedId in expectedIds)
        {
            CollectionAssert.Contains(foundIds, expectedId);
        }
    }

    // -----------------------------------------------------------------------------------
    //  Full pass: real JSON + real runner + real registry/validator, fake leaf interaction
    // -----------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator RunAsync_MainMenuNewGameReset_RealJsonThroughRealRunner_PassesAndCleansUpFully()
    {
        QaSceneRegistry registry = BuildFakeRegistryMirroringProductionAdapters();
        var validator = new QaScenarioValidator(registry);
        QaScenarioDefinition scenario = LoadValidatedScenarioFromRealResources(
            validator, "mainmenu.new-game-reset", out _);
        Assert.IsNotNull(scenario, "mainmenu.new-game-reset must be discoverable and valid under Resources/QA/Scenarios.");

        var driver = new QaDriverCore();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var evidence = new FakeEvidenceRecorder();
        var inputDriver = new QaApiInputDriver(targetId => ResolveInteractable(registry, targetId));
        var probe = new MutableProbe { InputGateLocked = false };

        var runner = new QaScenarioRunner(
            driver, registry, profile, lease, inputDriver, evidence, probe.Capture,
            captureScreenshotPng: FakePngProvider);

        Task<QaScenarioRunOutcome> task = runner.RunAsync(scenario);
        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        // 3 original steps (click, inputUnlocked assert, noNewConsoleError assert) + the
        // evidence.capture checkpoint added by the QA manifest PASS root-cause fix.
        Assert.AreEqual(4, outcome.StepOutcomes.Count);
        foreach (QaScenarioStepOutcome stepOutcome in outcome.StepOutcomes)
        {
            Assert.IsTrue(stepOutcome.IsSuccess, stepOutcome.StepId + ": " + stepOutcome.Message);
        }

        AssertRunnerAlwaysCleansUp(profile, lease, evidence);
    }

    [UnityTest]
    public IEnumerator RunAsync_KitchenFaucetKey_RealJsonThroughRealRunner_PassesAndCleansUpFully()
    {
        QaSceneRegistry registry = BuildFakeRegistryMirroringProductionAdapters();
        var validator = new QaScenarioValidator(registry);
        QaScenarioDefinition scenario = LoadValidatedScenarioFromRealResources(
            validator, "kitchen.faucet-key", out _);
        Assert.IsNotNull(scenario, "kitchen.faucet-key must be discoverable and valid under Resources/QA/Scenarios.");

        var driver = new QaDriverCore();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var evidence = new FakeEvidenceRecorder();
        var inputDriver = new QaApiInputDriver(targetId => ResolveInteractable(registry, targetId));
        var probe = new MutableProbe { InputGateLocked = false };

        var runner = new QaScenarioRunner(
            driver, registry, profile, lease, inputDriver, evidence, probe.Capture,
            captureScreenshotPng: FakePngProvider);

        Task<QaScenarioRunOutcome> task = runner.RunAsync(scenario);
        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);

        // Root-cause fix: the manifest verdict aggregated by Finalize (not just the runner's own
        // outcome code) must actually reach Pass now that a screenshot provider is wired in and
        // the JSON declares an evidence.capture checkpoint.
        Assert.IsNotNull(evidence.LastManifest, "Finalize must produce a manifest.");
        Assert.AreEqual(
            QaRunVerdictCode.Pass, evidence.LastManifest.Verdict, evidence.LastManifest.VerdictReason);
        Assert.GreaterOrEqual(evidence.LastManifest.ScreenshotCount, 1);

        AssertRunnerAlwaysCleansUp(profile, lease, evidence);
    }

    // -----------------------------------------------------------------------------------
    //  Step failure still finishes and cleans up (runner cleanup contract from the task brief)
    // -----------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator RunAsync_KitchenCheshireRepeat_ClickFails_StillFinishesAndCleansUpFully()
    {
        QaSceneRegistry registry = BuildFakeRegistryMirroringProductionAdapters();
        var validator = new QaScenarioValidator(registry);
        QaScenarioDefinition scenario = LoadValidatedScenarioFromRealResources(
            validator, "kitchen.cheshire-repeat", out _);
        Assert.IsNotNull(scenario, "kitchen.cheshire-repeat must be discoverable and valid under Resources/QA/Scenarios.");

        var driver = new QaDriverCore();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var evidence = new FakeEvidenceRecorder();
        Assert.IsTrue(registry.TryResolveScene(KitchenScene, out IQaSceneAdapter kitchenAdapter));
        ((FakeSceneAdapter)kitchenAdapter).ClickShouldSucceed = false;
        var inputDriver = new QaApiInputDriver(targetId => ResolveInteractable(registry, targetId));
        var probe = new MutableProbe { InputGateLocked = false };

        var runner = new QaScenarioRunner(driver, registry, profile, lease, inputDriver, evidence, probe.Capture);

        Task<QaScenarioRunOutcome> task = runner.RunAsync(scenario);
        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Failed, outcome.Code);
        Assert.AreEqual(1, outcome.StepOutcomes.Count, "Execution must stop at the first failing step.");

        AssertRunnerAlwaysCleansUp(profile, lease, evidence);
    }

    // -----------------------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------------------

    /// <summary>
    /// Minimal non-empty PNG-shaped byte stub for the fake evidence recorder -- these tests never
    /// decode the bytes as a real image, they only assert that a non-empty payload reached
    /// <see cref="IQaEvidenceRecorder.AttachScreenshot"/>.
    /// </summary>
    private static byte[] FakePngProvider()
    {
        return new byte[] { 0x89, 0x50, 0x4E, 0x47 };
    }

    private static IQaApiInteractable ResolveInteractable(QaSceneRegistry registry, QaTargetId targetId)
    {
        // Identical to QaCommandGateway.ResolveInteractable -- kept in lock-step deliberately so
        // this test exercises the same resolution path production uses.
        return registry.TryResolveTarget(targetId, out QaResolvedTarget resolved)
            ? resolved.Adapter as IQaApiInteractable
            : null;
    }

    private static void AssertRunnerAlwaysCleansUp(
        FakeProfileService profile, QaLeaseService lease, FakeEvidenceRecorder evidence)
    {
        Assert.AreEqual(1, profile.BeginCount, "Profile must have been begun exactly once.");
        Assert.AreEqual(1, profile.RestoreCount, "Profile must be restored regardless of pass/fail.");
        Assert.IsFalse(profile.IsQaProfileActive, "No QA profile must remain active after the run finishes.");

        QaLeaseAcquireResult reacquire = lease.TryAcquire("someone-else", QaRunId.NewId(), TimeSpan.FromMinutes(1));
        Assert.IsTrue(reacquire.IsAcquired, "Lease must have been released by the runner: " + reacquire.Message);
        lease.Release(reacquire.Lease.LeaseId);

        Assert.IsTrue(evidence.BeginRunCalled, "Evidence BeginRun must always be called.");
        Assert.IsTrue(evidence.FinalizeCalled, "Evidence Finalize must always be called, even on FAIL.");
    }

    // -----------------------------------------------------------------------------------
    //  Test doubles (same conventions as Godlotto.QA.Scenarios.QaScenarioRunnerTests)
    // -----------------------------------------------------------------------------------

    private sealed class FakeSceneAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        private readonly List<QaTargetId> targetIds;
        private readonly List<string> presetIds;

        public FakeSceneAdapter(string sceneName, IEnumerable<string> rawTargetIds, IEnumerable<string> presetIds)
        {
            SceneName = sceneName;
            targetIds = new List<QaTargetId>();
            foreach (string raw in rawTargetIds)
            {
                if (QaTargetId.TryCreate(raw, out QaTargetId id, out _))
                {
                    targetIds.Add(id);
                }
            }

            this.presetIds = new List<string>(presetIds);
        }

        public string SceneName { get; }

        public IReadOnlyCollection<QaTargetId> TargetIds => targetIds;

        public IReadOnlyCollection<string> PresetIds => presetIds;

        /// <summary>Configurable per test so failure-cleanup paths can be exercised deterministically.</summary>
        public bool ClickShouldSucceed { get; set; } = true;

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return presetIds.Contains(presetId)
                ? QaScenePresetResult.Success()
                : QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (ClickShouldSucceed)
            {
                error = null;
                return true;
            }

            error = "Simulated click failure for '" + targetId + "'.";
            return false;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = null;
            return true;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = null;
            return true;
        }
    }

    private sealed class FakeProfileService : IQaProfileService
    {
        public int BeginCount { get; private set; }

        public int RestoreCount { get; private set; }

        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            BeginCount++;
            IsQaProfileActive = true;
            return QaProfileOperationResult.Success("Fake QA profile began.");
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.Success("Fake QA profile reset.");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            RestoreCount++;
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("Fake QA profile restored.");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("Nothing to recover.");
        }
    }

    private sealed class FakeEvidenceRecorder : IQaEvidenceRecorder
    {
        public bool BeginRunCalled { get; private set; }

        public bool FinalizeCalled { get; private set; }

        public List<QaEvidenceEvent> Events { get; } = new List<QaEvidenceEvent>();

        /// <summary>
        /// Manifest produced by the most recent <see cref="Finalize"/> call -- exposed so tests can
        /// assert against <see cref="QaRunManifest.Verdict"/> directly, the same value
        /// <c>QaCommandGateway</c> writes into <c>manifest.json</c> for a real <c>qa_run</c>.
        /// </summary>
        public QaRunManifest LastManifest { get; private set; }

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            BeginRunCalled = true;
            return QaEvidenceOperationResult.Success("Fake evidence run began.");
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            Events.Add(evidenceEvent);
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return QaEvidenceOperationResult.Invalid("pngBytes must not be null or empty.");
            }

            Events.Add(QaEvidenceEvent.Create(
                QaEvidenceEventType.ScreenshotAttached, commandId: commandId, message: "Fake screenshot attached."));
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            Events.Add(QaEvidenceEvent.Create(QaEvidenceEventType.ConsoleRecorded, message: "Fake console recorded."));
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            FinalizeCalled = true;
            QaRunManifest manifest = QaRunManifest.Create(
                "fake-run", "fake-dir", DateTime.UtcNow, DateTime.UtcNow, Events,
                "events.jsonl", "console.log", "screenshots", "report.md");
            LastManifest = manifest;
            return QaEvidenceFinalizeResult.Success(manifest, "fake-dir");
        }
    }

    /// <summary>Mutable fake state probe (same role as <c>QaScenarioRunnerTests.MutableProbe</c>).</summary>
    private sealed class MutableProbe
    {
        public bool InputGateLocked { get; set; }

        public QaDriverSnapshot Capture()
        {
            return QaDriverSnapshot.Create(inputGateLocked: InputGateLocked);
        }
    }
}
