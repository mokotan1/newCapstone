using System.Collections.Generic;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenarios;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Task 12 §Step 1: written before touching any scene asset. Verifies (a) the five initial scene
/// adapters (<see cref="MainMenuQaAdapter"/>, <see cref="KitchenQaAdapter"/>,
/// <see cref="HallQaAdapter"/>, <see cref="MaidRoomQaAdapter"/>, <see cref="TutorRoomQaAdapter"/>)
/// declare well-formed, non-conflicting, already-lowercase-dotted <see cref="QaTargetId"/>s and
/// register cleanly into a fresh <see cref="QaSceneRegistry"/> via
/// <see cref="QaSceneAdapterRegistration"/>, and (b) the six real scenario JSON resources under
/// <c>Resources/QA/Scenarios/2026-07/</c> validate against that exact registry -- the same
/// registry/validator pairing <c>QaCommandGateway.ListScenarios</c>/<c>RunScenarioAsync</c> use in
/// production. No Play Mode scene load is required: adapters resolve real MonoBehaviours via
/// <c>UnityEngine.Object.FindFirstObjectByType</c> at call time, not via serialized
/// <c>QaTargetIdBehaviour</c> components, so this task did not need to modify any .unity scene
/// asset (see Task 12 report for the explicit rationale).
/// </summary>
[TestFixture]
public sealed class InitialSceneAdapterSerializationTests
{
    private static readonly string[] ExpectedSceneNames =
    {
        "MainMenuScene", "Kitchen", "Hall_playerble", "MaidRoom", "TutorRoom"
    };

    private static readonly (string SceneName, string RawTargetId)[] ExpectedTargets =
    {
        ("MainMenuScene", "mainmenu.start-button"),
        ("Kitchen", "kitchen.sink.faucet"),
        ("Kitchen", "kitchen.parret"),
        ("Hall_playerble", "hall.kitchen-entry"),
        ("MaidRoom", "maidroom.food-tray"),
        ("TutorRoom", "tutorroom.quiz-input")
    };

    private static readonly string[] ExpectedScenarioIds =
    {
        "mainmenu.new-game-reset",
        "kitchen.faucet-key",
        "kitchen.cheshire-repeat",
        "hall.kitchen-quest",
        "maidroom.food-effect",
        "tutorroom.cheshire-quiz"
    };

    // -----------------------------------------------------------------------------------
    //  Registration / target id serialization
    // -----------------------------------------------------------------------------------

    [Test]
    public void RegisterAll_RegistersExactlyTheFiveExpectedScenesWithNoConflicts()
    {
        var registry = new QaSceneRegistry();

        QaSceneAdapterRegistration.RegisterAll(registry);

        CollectionAssert.AreEquivalent(ExpectedSceneNames, registry.RegisteredSceneNames);
    }

    [Test]
    public void RegisterAll_IsIdempotentAcrossIndependentFreshRegistries()
    {
        // No hidden static mutation should leak between independent registrations -- each fresh
        // registry must end up with the exact same scene coverage.
        var first = new QaSceneRegistry();
        var second = new QaSceneRegistry();

        QaSceneAdapterRegistration.RegisterAll(first);
        QaSceneAdapterRegistration.RegisterAll(second);

        CollectionAssert.AreEquivalent(first.RegisteredSceneNames, second.RegisteredSceneNames);
    }

    [Test]
    public void DeclaredTargetIds_AreAllAlreadyNormalizedLowercaseDottedStrings()
    {
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);

        foreach ((string sceneName, string rawTargetId) in ExpectedTargets)
        {
            Assert.IsTrue(
                QaTargetId.TryCreate(rawTargetId, out QaTargetId targetId, out string error),
                "Target id literal '" + rawTargetId + "' must itself satisfy QaTargetId's " +
                "normalization rules (lowercase, dotted, no whitespace/hierarchy separators): " + error);

            Assert.AreEqual(
                rawTargetId, targetId.Value,
                "Target id '" + rawTargetId + "' must already be declared in normalized form " +
                "(TryCreate must not need to change casing).");
        }
    }

    [Test]
    public void DeclaredTargetIds_EachResolveToTheirDeclaredOwningScene()
    {
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);

        foreach ((string sceneName, string rawTargetId) in ExpectedTargets)
        {
            QaTargetId targetId = QaTargetId.Create(rawTargetId);

            bool resolved = registry.TryResolveTarget(targetId, out QaResolvedTarget resolvedTarget);

            Assert.IsTrue(resolved, "Target '" + rawTargetId + "' must resolve through the registry.");
            Assert.AreEqual(sceneName, resolvedTarget.Adapter.SceneName);
        }
    }

    [Test]
    public void KitchenAdapter_DeclaresBothFaucetAndParretPresets()
    {
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);

        Assert.IsTrue(registry.TryResolveScene("Kitchen", out IQaSceneAdapter kitchen));
        CollectionAssert.Contains(kitchen.PresetIds, "before-faucet");
        CollectionAssert.Contains(kitchen.PresetIds, "before-parret");
    }

    [Test]
    public void AllFiveAdapters_CaptureSnapshotOutsidePlayMode_NeverThrows()
    {
        // CaptureSnapshot must be safe to call even when the declared scene is not currently
        // loaded (EditMode has no Play Mode scene at all) -- adapters must report "not found"
        // diagnostics rather than throwing NullReferenceException.
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);

        foreach (string sceneName in ExpectedSceneNames)
        {
            Assert.IsTrue(registry.TryResolveScene(sceneName, out IQaSceneAdapter adapter));

            QaSceneSnapshot snapshot = null;
            Assert.DoesNotThrow(() => snapshot = adapter.CaptureSnapshot());
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(sceneName, snapshot.SceneName);
        }
    }

    // -----------------------------------------------------------------------------------
    //  Scenario JSON validation against the real registry (Task 12 §Step 3 cross-check)
    // -----------------------------------------------------------------------------------

    [Test]
    public void AllSixScenarioJsonResources_ValidateAgainstTheRegisteredAdapters()
    {
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);
        var validator = new QaScenarioValidator(registry);

        TextAsset[] assets = Resources.LoadAll<TextAsset>("QA/Scenarios");
        Assert.IsNotEmpty(
            assets,
            "Expected the six Task 12 scenario JSON resources to be discoverable under " +
            "Resources/QA/Scenarios (including the 2026-07 subfolder) -- the same path " +
            "QaCommandGateway.ListScenarios()/RunScenarioAsync() load from.");

        var foundIds = new HashSet<string>();
        foreach (TextAsset asset in assets)
        {
            QaScenarioValidationResult result = validator.Validate(asset.text);
            Assert.IsTrue(
                result.IsValid,
                "Resource '" + asset.name + "' must validate: " + string.Join(" | ", result.Errors));
            foundIds.Add(result.Scenario.Id);
        }

        foreach (string expectedId in ExpectedScenarioIds)
        {
            CollectionAssert.Contains(foundIds, expectedId);
        }
    }

    [Test]
    public void AllSixScenarioJsonResources_HaveUniqueIds()
    {
        TextAsset[] assets = Resources.LoadAll<TextAsset>("QA/Scenarios");
        var seenIds = new List<string>();
        var registry = new QaSceneRegistry();
        QaSceneAdapterRegistration.RegisterAll(registry);
        var validator = new QaScenarioValidator(registry);

        foreach (TextAsset asset in assets)
        {
            QaScenarioValidationResult result = validator.Validate(asset.text);
            if (result.IsValid)
            {
                seenIds.Add(result.Scenario.Id);
            }
        }

        CollectionAssert.AllItemsAreUnique(seenIds);
    }
}
