using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godlotto.QA.Evidence;
using Godlotto.QA.Scenarios;
using Newtonsoft.Json;
using NUnit.Framework;

/// <summary>
/// Task 8을 검증합니다: (1) <see cref="QaDriverSnapshot"/>이 허용목록 필드만 노출하고 원본
/// 프롬프트/응답/토큰류 필드를 절대 갖지 않는지, (2) <see cref="QaStateProbe"/>가 주입된
/// 콜백만으로(씬 로드 없이) 스냅샷을 조립하고 콜백 예외를 안전한 기본값으로 흡수하는지,
/// (3) <see cref="QaAssertion"/>의 초기 어서션 종류(동등성, 불리언, 인벤토리, 대상
/// 활성/상호작용, 퀘스트 현재/완료, 입력 잠금 해제, Flowchart idle, 콘솔 오류 없음)가 각각
/// 통과/실패를 올바르게 산출하는지를 검증합니다. QA Evidence/Scenarios 타입은
/// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만 컴파일되며, 본 EditMode 테스트는 항상
/// 에디터에서 실행되므로 해당 타입을 볼 수 있습니다.
/// </summary>
[TestFixture]
public class QaStateProbeTests
{
    // ---------------------------------------------------------------
    //  Step 1a: QaDriverSnapshot allow-list surface (no raw prompts/responses/tokens)
    // ---------------------------------------------------------------

    [Test]
    public void Snapshot_PublicPropertySurface_MatchesExplicitAllowList()
    {
        var allowedPropertyNames = new HashSet<string>
        {
            nameof(QaDriverSnapshot.RunId),
            nameof(QaDriverSnapshot.CapturedAtUtc),
            nameof(QaDriverSnapshot.SceneName),
            nameof(QaDriverSnapshot.InventoryItemIds),
            nameof(QaDriverSnapshot.QuestCurrentStepId),
            nameof(QaDriverSnapshot.QuestCompletedStepIds),
            nameof(QaDriverSnapshot.TargetActiveStates),
            nameof(QaDriverSnapshot.TargetInteractableStates),
            nameof(QaDriverSnapshot.InputGateLocked),
            nameof(QaDriverSnapshot.FlowchartIdleStates),
            nameof(QaDriverSnapshot.AiConnectionState),
            nameof(QaDriverSnapshot.ConsoleErrorCount),
            nameof(QaDriverSnapshot.Values)
        };

        IEnumerable<string> actualPropertyNames = typeof(QaDriverSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name);

        CollectionAssert.AreEquivalent(
            allowedPropertyNames, actualPropertyNames,
            "QaDriverSnapshot must expose exactly the allow-listed fields; if a new field is " +
            "legitimately needed, add it to this test's allow list explicitly (drift guard).");
    }

    [Test]
    public void Snapshot_AiConnectionState_IsAClosedEnumNeverFreeText()
    {
        // Structural guarantee: AI status can only ever be one of these four values, so no raw
        // prompt/response text can ever flow through this field.
        Assert.IsTrue(typeof(QaAiConnectionState).IsEnum);
        CollectionAssert.AreEquivalent(
            new[] { "Idle", "Connecting", "Connected", "Error" },
            Enum.GetNames(typeof(QaAiConnectionState)));
    }

    [Test]
    public void Snapshot_SerializedJson_NeverContainsForbiddenSubstrings()
    {
        var probe = new QaStateProbe(
            sceneNameProvider: () => "Kitchen",
            inventoryItemIdsProvider: () => new[] { 3, 7 },
            questCurrentStepIdProvider: () => "kitchen.electric_on",
            questCompletedStepIdsProvider: () => new[] { "intro.completed" },
            targetActiveStatesProvider: () => new Dictionary<string, bool> { ["kitchen.panel"] = true },
            targetInteractableStatesProvider: () => new Dictionary<string, bool> { ["kitchen.sink"] = false },
            inputGateLockedProvider: () => true,
            flowchartIdleStatesProvider: () => new Dictionary<string, bool> { ["Variablemanager"] = false },
            aiConnectionStateProvider: () => QaAiConnectionState.Connected,
            consoleErrorCountProvider: () => 2);

        QaDriverSnapshot snapshot = probe.Capture("run-1");
        string json = JsonConvert.SerializeObject(snapshot).ToLowerInvariant();

        string[] forbiddenSubstrings =
        {
            "prompt", "response", "token", "apikey", "api_key", "authorization", "secret", "cookie"
        };

        foreach (string forbidden in forbiddenSubstrings)
        {
            StringAssert.DoesNotContain(forbidden, json,
                "Serialized QaDriverSnapshot must never contain '" + forbidden + "'.");
        }
    }

    // ---------------------------------------------------------------
    //  Step 1b: QaStateProbe assembles a snapshot from injectable providers (no scene needed)
    // ---------------------------------------------------------------

    [Test]
    public void Capture_NoProvidersConfigured_ReturnsSafeDefaults()
    {
        var probe = new QaStateProbe();

        QaDriverSnapshot snapshot = probe.Capture("run-defaults");

        Assert.AreEqual("run-defaults", snapshot.RunId);
        Assert.AreEqual(string.Empty, snapshot.SceneName);
        Assert.IsEmpty(snapshot.InventoryItemIds);
        Assert.AreEqual(string.Empty, snapshot.QuestCurrentStepId);
        Assert.IsEmpty(snapshot.QuestCompletedStepIds);
        Assert.IsEmpty(snapshot.TargetActiveStates);
        Assert.IsEmpty(snapshot.TargetInteractableStates);
        Assert.IsFalse(snapshot.InputGateLocked);
        Assert.IsEmpty(snapshot.FlowchartIdleStates);
        Assert.AreEqual(QaAiConnectionState.Idle, snapshot.AiConnectionState);
        Assert.AreEqual(0, snapshot.ConsoleErrorCount);
    }

    [Test]
    public void Capture_AllProvidersConfigured_PopulatesEveryField()
    {
        var probe = new QaStateProbe(
            sceneNameProvider: () => "StudyRoom",
            inventoryItemIdsProvider: () => new[] { 1, 2, 3 },
            questCurrentStepIdProvider: () => "study.diary_read",
            questCompletedStepIdsProvider: () => new[] { "intro.completed", "kitchen.electric_on" },
            targetActiveStatesProvider: () => new Dictionary<string, bool> { ["study.bookcase"] = true },
            targetInteractableStatesProvider: () => new Dictionary<string, bool> { ["study.bible"] = true },
            inputGateLockedProvider: () => false,
            flowchartIdleStatesProvider: () => new Dictionary<string, bool> { ["Variablemanager"] = true },
            aiConnectionStateProvider: () => QaAiConnectionState.Connecting,
            consoleErrorCountProvider: () => 5);

        QaDriverSnapshot snapshot = probe.Capture("run-full");

        Assert.AreEqual("StudyRoom", snapshot.SceneName);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, snapshot.InventoryItemIds);
        Assert.AreEqual("study.diary_read", snapshot.QuestCurrentStepId);
        CollectionAssert.AreEqual(new[] { "intro.completed", "kitchen.electric_on" }, snapshot.QuestCompletedStepIds);
        Assert.IsTrue(snapshot.TargetActiveStates["study.bookcase"]);
        Assert.IsTrue(snapshot.TargetInteractableStates["study.bible"]);
        Assert.IsFalse(snapshot.InputGateLocked);
        Assert.IsTrue(snapshot.FlowchartIdleStates["Variablemanager"]);
        Assert.AreEqual(QaAiConnectionState.Connecting, snapshot.AiConnectionState);
        Assert.AreEqual(5, snapshot.ConsoleErrorCount);
    }

    [Test]
    public void Capture_ProviderThrows_FallsBackToSafeDefaultForThatFieldOnly()
    {
        var probe = new QaStateProbe(
            sceneNameProvider: () => throw new InvalidOperationException("boom"),
            questCurrentStepIdProvider: () => "still.works",
            inputGateLockedProvider: () => throw new NullReferenceException("boom2"));

        QaDriverSnapshot snapshot = null;
        Assert.DoesNotThrow(() => snapshot = probe.Capture("run-fault"));

        Assert.AreEqual(string.Empty, snapshot.SceneName, "A throwing provider must fall back, not propagate.");
        Assert.AreEqual("still.works", snapshot.QuestCurrentStepId, "Other providers must be unaffected by a sibling's fault.");
        Assert.IsFalse(snapshot.InputGateLocked);
    }

    [Test]
    public void Capture_TwoCalls_ReturnIndependentSnapshotsNotAliasedCollections()
    {
        var backingList = new List<int> { 1 };
        var probe = new QaStateProbe(inventoryItemIdsProvider: () => backingList);

        QaDriverSnapshot first = probe.Capture("run-1");
        backingList.Add(2);
        QaDriverSnapshot second = probe.Capture("run-2");

        CollectionAssert.AreEqual(new[] { 1 }, first.InventoryItemIds,
            "A snapshot must defensively copy provided collections so later caller-side mutation cannot retroactively change it.");
        CollectionAssert.AreEqual(new[] { 1, 2 }, second.InventoryItemIds);
    }

    // ---------------------------------------------------------------
    //  Values flattening (evidence log + QaAssertion.FieldEquals/FieldBoolean integration)
    // ---------------------------------------------------------------

    [Test]
    public void Values_FlattensTypedFieldsIntoStringKeyValuePairs()
    {
        var probe = new QaStateProbe(
            sceneNameProvider: () => "Kitchen",
            inputGateLockedProvider: () => true,
            aiConnectionStateProvider: () => QaAiConnectionState.Error,
            consoleErrorCountProvider: () => 3,
            targetActiveStatesProvider: () => new Dictionary<string, bool> { ["kitchen.panel"] = true });

        QaDriverSnapshot snapshot = probe.Capture();

        Assert.AreEqual("Kitchen", snapshot.Values[QaDriverSnapshot.SceneNameKey]);
        Assert.AreEqual("True", snapshot.Values[QaDriverSnapshot.InputGateLockedKey]);
        Assert.AreEqual("Error", snapshot.Values[QaDriverSnapshot.AiConnectionStateKey]);
        Assert.AreEqual("3", snapshot.Values[QaDriverSnapshot.ConsoleErrorCountKey]);
        Assert.AreEqual("True", snapshot.Values[QaDriverSnapshot.TargetActivePrefix + "kitchen.panel"]);
    }

    // ---------------------------------------------------------------
    //  Step 2: typed assertion evaluators
    // ---------------------------------------------------------------

    private static QaDriverSnapshot MakeSnapshot(
        string sceneName = "Kitchen",
        IReadOnlyList<int> inventoryItemIds = null,
        string questCurrentStepId = "kitchen.electric_on",
        IReadOnlyList<string> questCompletedStepIds = null,
        IReadOnlyDictionary<string, bool> targetActiveStates = null,
        IReadOnlyDictionary<string, bool> targetInteractableStates = null,
        bool inputGateLocked = false,
        IReadOnlyDictionary<string, bool> flowchartIdleStates = null,
        int consoleErrorCount = 0)
    {
        return QaDriverSnapshot.Create(
            runId: "run-1",
            sceneName: sceneName,
            inventoryItemIds: inventoryItemIds ?? new[] { 3, 7 },
            questCurrentStepId: questCurrentStepId,
            questCompletedStepIds: questCompletedStepIds ?? new[] { "intro.completed" },
            targetActiveStates: targetActiveStates ?? new Dictionary<string, bool> { ["kitchen.panel"] = true },
            targetInteractableStates: targetInteractableStates ?? new Dictionary<string, bool> { ["kitchen.sink"] = true },
            inputGateLocked: inputGateLocked,
            flowchartIdleStates: flowchartIdleStates ?? new Dictionary<string, bool> { ["Variablemanager"] = true },
            consoleErrorCount: consoleErrorCount);
    }

    [Test]
    public void FieldEquals_MatchingField_Passes()
    {
        QaAssertionResult result = QaAssertion
            .FieldEquals(QaDriverSnapshot.SceneNameKey, "Kitchen")
            .Evaluate(MakeSnapshot(sceneName: "Kitchen"));

        Assert.IsTrue(result.Passed, result.Message);
        Assert.AreEqual("Kitchen", result.ObservedValue);
    }

    [Test]
    public void FieldEquals_MismatchedField_FailsWithObservedValue()
    {
        QaAssertionResult result = QaAssertion
            .FieldEquals(QaDriverSnapshot.SceneNameKey, "StudyRoom")
            .Evaluate(MakeSnapshot(sceneName: "Kitchen"));

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("Kitchen", result.ObservedValue);
    }

    [Test]
    public void FieldEquals_UnknownField_FailsWithoutThrowing()
    {
        QaAssertionResult result = QaAssertion.FieldEquals("NoSuchField", "x").Evaluate(MakeSnapshot());

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void FieldBoolean_MatchingBoolean_Passes()
    {
        QaAssertionResult result = QaAssertion
            .FieldBoolean(QaDriverSnapshot.InputGateLockedKey, expected: true)
            .Evaluate(MakeSnapshot(inputGateLocked: true));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void FieldBoolean_MismatchedBoolean_Fails()
    {
        QaAssertionResult result = QaAssertion
            .FieldBoolean(QaDriverSnapshot.InputGateLockedKey, expected: true)
            .Evaluate(MakeSnapshot(inputGateLocked: false));

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("False", result.ObservedValue);
    }

    [Test]
    public void InventoryContains_ItemPresent_Passes()
    {
        QaAssertionResult result = QaAssertion.InventoryContains(7).Evaluate(MakeSnapshot(inventoryItemIds: new[] { 3, 7 }));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void InventoryContains_ItemAbsent_Fails()
    {
        QaAssertionResult result = QaAssertion.InventoryContains(99).Evaluate(MakeSnapshot(inventoryItemIds: new[] { 3, 7 }));

        Assert.IsFalse(result.Passed);
        StringAssert.Contains("3,7", result.ObservedValue);
    }

    [Test]
    public void TargetActive_ExpectedTrueAndActive_Passes()
    {
        QaAssertionResult result = QaAssertion.TargetActive("kitchen.panel").Evaluate(
            MakeSnapshot(targetActiveStates: new Dictionary<string, bool> { ["kitchen.panel"] = true }));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void TargetActive_NotActive_Fails()
    {
        QaAssertionResult result = QaAssertion.TargetActive("kitchen.panel").Evaluate(
            MakeSnapshot(targetActiveStates: new Dictionary<string, bool> { ["kitchen.panel"] = false }));

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void TargetActive_UnknownTarget_FailsWithoutThrowing()
    {
        QaAssertionResult result = QaAssertion.TargetActive("never.registered").Evaluate(MakeSnapshot());

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void TargetInteractable_ExpectedTrueAndInteractable_Passes()
    {
        QaAssertionResult result = QaAssertion.TargetInteractable("kitchen.sink").Evaluate(
            MakeSnapshot(targetInteractableStates: new Dictionary<string, bool> { ["kitchen.sink"] = true }));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void TargetInteractable_NotInteractable_Fails()
    {
        QaAssertionResult result = QaAssertion.TargetInteractable("kitchen.sink").Evaluate(
            MakeSnapshot(targetInteractableStates: new Dictionary<string, bool> { ["kitchen.sink"] = false }));

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void QuestCurrentStepEquals_Matching_Passes()
    {
        QaAssertionResult result = QaAssertion.QuestCurrentStepEquals("kitchen.electric_on")
            .Evaluate(MakeSnapshot(questCurrentStepId: "kitchen.electric_on"));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void QuestCurrentStepEquals_Mismatched_Fails()
    {
        QaAssertionResult result = QaAssertion.QuestCurrentStepEquals("study.diary_read")
            .Evaluate(MakeSnapshot(questCurrentStepId: "kitchen.electric_on"));

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("kitchen.electric_on", result.ObservedValue);
    }

    [Test]
    public void QuestStepCompleted_StepInCompletedList_Passes()
    {
        QaAssertionResult result = QaAssertion.QuestStepCompleted("intro.completed")
            .Evaluate(MakeSnapshot(questCompletedStepIds: new[] { "intro.completed" }));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void QuestStepCompleted_StepNotCompleted_Fails()
    {
        QaAssertionResult result = QaAssertion.QuestStepCompleted("study.diary_read")
            .Evaluate(MakeSnapshot(questCompletedStepIds: new[] { "intro.completed" }));

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void InputUnlocked_GateOpen_Passes()
    {
        QaAssertionResult result = QaAssertion.InputUnlocked().Evaluate(MakeSnapshot(inputGateLocked: false));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void InputUnlocked_GateLocked_Fails()
    {
        QaAssertionResult result = QaAssertion.InputUnlocked().Evaluate(MakeSnapshot(inputGateLocked: true));

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("True", result.ObservedValue);
    }

    [Test]
    public void FlowchartIdle_Idle_Passes()
    {
        QaAssertionResult result = QaAssertion.FlowchartIdle("Variablemanager").Evaluate(
            MakeSnapshot(flowchartIdleStates: new Dictionary<string, bool> { ["Variablemanager"] = true }));

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void FlowchartIdle_NotIdle_Fails()
    {
        QaAssertionResult result = QaAssertion.FlowchartIdle("Variablemanager").Evaluate(
            MakeSnapshot(flowchartIdleStates: new Dictionary<string, bool> { ["Variablemanager"] = false }));

        Assert.IsFalse(result.Passed);
    }

    [Test]
    public void NoNewConsoleError_SameCountAsBaseline_Passes()
    {
        QaDriverSnapshot baseline = MakeSnapshot(consoleErrorCount: 2);
        QaDriverSnapshot current = MakeSnapshot(consoleErrorCount: 2);

        QaAssertionResult result = QaAssertion.NoNewConsoleError().Evaluate(current, baseline);

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void NoNewConsoleError_CountIncreased_Fails()
    {
        QaDriverSnapshot baseline = MakeSnapshot(consoleErrorCount: 2);
        QaDriverSnapshot current = MakeSnapshot(consoleErrorCount: 5);

        QaAssertionResult result = QaAssertion.NoNewConsoleError().Evaluate(current, baseline);

        Assert.IsFalse(result.Passed);
        StringAssert.Contains("5", result.ObservedValue);
        StringAssert.Contains("2", result.ObservedValue);
    }

    [Test]
    public void NoNewConsoleError_NoBaselineProvided_TreatsZeroErrorsAsPassingFloor()
    {
        QaDriverSnapshot current = MakeSnapshot(consoleErrorCount: 0);

        QaAssertionResult result = QaAssertion.NoNewConsoleError().Evaluate(current, baseline: null);

        Assert.IsTrue(result.Passed, result.Message);
    }

    [Test]
    public void NoNewConsoleError_NoBaselineProvidedButErrorsPresent_Fails()
    {
        QaDriverSnapshot current = MakeSnapshot(consoleErrorCount: 1);

        QaAssertionResult result = QaAssertion.NoNewConsoleError().Evaluate(current, baseline: null);

        Assert.IsFalse(result.Passed,
            "Without a baseline, any recorded console error must fail-safe to a failing assertion.");
    }

    [Test]
    public void Evaluate_NullSnapshot_FailsWithoutThrowing()
    {
        QaAssertionResult result = null;
        Assert.DoesNotThrow(() => result = QaAssertion.InputUnlocked().Evaluate(null));

        Assert.IsFalse(result.Passed);
        Assert.AreEqual("(null snapshot)", result.ObservedValue);
    }

    [Test]
    public void FactoryMethods_BlankRequiredArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => QaAssertion.FieldEquals(" ", "x"));
        Assert.Throws<ArgumentException>(() => QaAssertion.TargetActive(string.Empty));
        Assert.Throws<ArgumentException>(() => QaAssertion.QuestCurrentStepEquals(null));
        Assert.Throws<ArgumentException>(() => QaAssertion.FlowchartIdle("   "));
    }
}
