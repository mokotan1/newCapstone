#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Kitchen room happy-path encodes RealInput then API (§6.2); no force-solve PASS.
/// </summary>
[TestFixture]
public sealed class KitchenRealInputHappyPathTests
{
    private const string HappyPathRelative =
        "Assets/Resources/QA/Scenarios/Rooms/first-floor/kitchen/happy-path.json";

    [Test]
    public void HappyPathJson_StepOrder_IsPointerThenResetThenApiInvoke()
    {
        string path = LocateHappyPathJson();
        Assert.IsTrue(File.Exists(path), "Expected happy-path at " + path);

        string json = File.ReadAllText(path);
        Assert.IsFalse(json.Contains("force-solve"), "Force-solve must not be used for PASS.");
        Assert.IsFalse(json.Contains("forceSolve"), "Force-solve must not be used for PASS.");

        var validator = new DeveloperQaScenarioValidator();
        DeveloperQaScenarioValidationResult result = validator.Validate(json);
        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Scenario);
        Assert.AreEqual("room.kitchen.happy-path", result.Scenario.Id);

        IList<DeveloperQaScenarioStepDefinition> steps = result.Scenario.Steps;
        int beforeFillIndex = IndexOfTarget(steps, "kitchen.sink.preset.before-bottle-fill");
        int fillIndex = IndexOfTarget(steps, "kitchen.sink.fill-bottle");
        int pointerIndex = IndexOf(steps, "interaction", "pointer");
        int exitIndex = IndexOfTarget(steps, "kitchen.exit.assert");
        int apiClickIndex = IndexOfTarget(steps, "kitchen.faucet.click");
        int evidenceIndex = IndexOf(steps, "evidence", "capture");

        Assert.GreaterOrEqual(beforeFillIndex, 0, "Expected before-bottle-fill preset.");
        Assert.GreaterOrEqual(fillIndex, 0, "Expected kitchen.sink.fill-bottle.");
        Assert.GreaterOrEqual(pointerIndex, 0, "Expected interaction.pointer RealInput step.");
        Assert.GreaterOrEqual(exitIndex, 0, "Expected kitchen.exit.assert.");
        Assert.GreaterOrEqual(apiClickIndex, 0, "Expected kitchen.faucet.click API invoke.");
        Assert.GreaterOrEqual(evidenceIndex, 0, "Expected evidence.capture.");

        Assert.Less(beforeFillIndex, fillIndex, "before-bottle-fill must precede fills-bottle.");
        Assert.Less(fillIndex, pointerIndex, "fills-bottle must precede RealInput faucet.");
        Assert.Less(pointerIndex, exitIndex, "RealInput faucet must precede exit.assert.");
        Assert.Less(pointerIndex, apiClickIndex, "RealInput pointer must precede API faucet click.");
        Assert.Less(apiClickIndex, evidenceIndex, "API invoke must precede evidence.capture.");

        DeveloperQaScenarioStepDefinition pointer = steps[pointerIndex];
        Assert.AreEqual("kitchen.sink.faucet", pointer.TargetId);
        Assert.IsNotNull(pointer.Parameters);
        Assert.IsTrue(
            pointer.Parameters.TryGetValue("mode", out string mode)
            && string.Equals(mode, "realInput", System.StringComparison.OrdinalIgnoreCase),
            "pointer step must request mode=realInput");
    }

    [Test]
    public async Task Pointer_WithInjectedRealInputDriver_InvokesClickAsync()
    {
        var recording = new RecordingQaInputDriver(QaInteractionMode.RealInput);
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry, null, null, recording);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "click-faucet-real",
                "interaction",
                "pointer",
                "kitchen.sink.faucet",
                new Dictionary<string, string> { ["mode"] = "realInput" }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        Assert.AreEqual(1, recording.ClickCount);
        Assert.AreEqual("kitchen.sink.faucet", recording.LastTargetId.Value);
        Assert.AreEqual(QaInteractionMode.RealInput, recording.Mode);
    }

    [Test]
    public async Task Pointer_WithoutRealInputDriver_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "click-faucet-real",
                "interaction",
                "pointer",
                "kitchen.sink.faucet",
                new Dictionary<string, string> { ["mode"] = "realInput" }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code, result.Message);
    }

    [Test]
    public async Task Invoke_FaucetClick_StillDispatchesCapabilityApiPath()
    {
        var recording = new RecordingQaInputDriver(QaInteractionMode.RealInput);
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry, null, null, recording);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "click-faucet-api",
                "interaction",
                "invoke",
                "kitchen.faucet.click"),
            CancellationToken.None);

        // API path uses KitchenQaAdapter.TryClick (capability), not RealInput driver.
        Assert.AreEqual(0, recording.ClickCount);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code, result.Message);
    }

    private static string LocateHappyPathJson()
    {
        string dataPath = Application.dataPath;
        if (!string.IsNullOrEmpty(dataPath))
        {
            string underAssets = Path.GetFullPath(Path.Combine(dataPath, "..", HappyPathRelative));
            if (File.Exists(underAssets))
            {
                return underAssets;
            }
        }

        string cwd = Directory.GetCurrentDirectory();
        string[] candidates =
        {
            Path.Combine(cwd, "disputatio", HappyPathRelative),
            Path.Combine(cwd, HappyPathRelative)
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            string full = Path.GetFullPath(candidates[i]);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return Path.GetFullPath(Path.Combine(cwd, "disputatio", HappyPathRelative));
    }

    private static int IndexOf(
        IList<DeveloperQaScenarioStepDefinition> steps,
        string family,
        string name)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            DeveloperQaScenarioStepDefinition step = steps[i];
            if (step != null
                && string.Equals(step.Family, family, System.StringComparison.Ordinal)
                && string.Equals(step.Name, name, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfTarget(
        IList<DeveloperQaScenarioStepDefinition> steps,
        string targetId)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            DeveloperQaScenarioStepDefinition step = steps[i];
            if (step != null
                && string.Equals(step.TargetId, targetId, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class RecordingQaInputDriver : IQaInputDriver
    {
        public RecordingQaInputDriver(QaInteractionMode mode)
        {
            Mode = mode;
        }

        public QaInteractionMode Mode { get; }

        public int ClickCount { get; private set; }

        public QaTargetId LastTargetId { get; private set; }

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            ClickCount++;
            LastTargetId = targetId;
            return Task.FromResult(QaInputResult.Success(targetId, Mode, "recorded"));
        }

        public Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId,
            QaTargetId destinationTargetId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Failure(
                sourceTargetId, Mode, QaInputResultCode.UnsupportedInteraction, "not used"));
        }

        public Task<QaInputResult> KeyAsync(
            QaTargetId targetId,
            string text,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Failure(
                targetId, Mode, QaInputResultCode.UnsupportedInteraction, "not used"));
        }
    }
}
#endif
