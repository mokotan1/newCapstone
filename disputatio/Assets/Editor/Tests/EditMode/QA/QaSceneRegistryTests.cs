using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// <see cref="QaSceneRegistry"/>의 씬 이름 등록/조회, 대상 ID 전역 고유성 검증(등록 원자성 및
/// 두 어댑터 모두의 진단 정보 포함), "이름 유사도로 대체 추측하지 않음" 규칙, 그리고
/// Build Settings 커버리지 감사(소프트, Task 13에서 하드 게이트로 전환)를 검증합니다.
/// QA Scenes 타입은 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만 컴파일되며, 본 EditMode
/// 테스트는 항상 에디터에서 실행되므로 해당 타입을 볼 수 있습니다.
/// </summary>
[TestFixture]
public class QaSceneRegistryTests
{
    private QaSceneRegistry registry;

    [SetUp]
    public void SetUp()
    {
        registry = new QaSceneRegistry();
    }

    // ---------------------------------------------------------------
    //  Registration: happy path
    // ---------------------------------------------------------------

    [Test]
    public void Register_ValidAdapter_SucceedsAndSceneBecomesResolvable()
    {
        var kitchen = new TestSceneAdapter("Kitchen", new[] { "kitchen.sink.faucet", "kitchen.maid-key" });

        QaSceneRegistrationResult result = registry.Register(kitchen);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(registry.TryResolveScene("Kitchen", out IQaSceneAdapter resolved));
        Assert.AreSame(kitchen, resolved);
    }

    [Test]
    public void Register_ValidAdapter_MakesDeclaredTargetIdsResolvable()
    {
        var kitchen = new TestSceneAdapter("Kitchen", new[] { "kitchen.sink.faucet" });
        registry.Register(kitchen);

        bool resolved = registry.TryResolveTarget(QaTargetId.Create("kitchen.sink.faucet"), out QaResolvedTarget target);

        Assert.IsTrue(resolved);
        Assert.AreSame(kitchen, target.Adapter);
        Assert.AreEqual(QaTargetId.Create("kitchen.sink.faucet"), target.TargetId);
    }

    [Test]
    public void Register_NullAdapter_ReturnsFailure()
    {
        QaSceneRegistrationResult result = registry.Register(null);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotEmpty(result.Message);
    }

    [Test]
    public void Register_BlankSceneName_ReturnsFailure()
    {
        var adapter = new TestSceneAdapter("   ", Array.Empty<string>());

        QaSceneRegistrationResult result = registry.Register(adapter);

        Assert.IsFalse(result.IsSuccess);
    }

    // ---------------------------------------------------------------
    //  Step 1: duplicate active IDs must fail registry validation with
    //  both hierarchy diagnostics (both conflicting registrations named).
    // ---------------------------------------------------------------

    [Test]
    public void Register_DuplicateTargetIdAcrossDifferentAdapters_FailsAndMentionsBothScenes()
    {
        var kitchen = new TestSceneAdapter("Kitchen", new[] { "shared.target" });
        var maidRoom = new TestSceneAdapter("MaidRoom", new[] { "shared.target" });

        QaSceneRegistrationResult first = registry.Register(kitchen);
        QaSceneRegistrationResult second = registry.Register(maidRoom);

        Assert.IsTrue(first.IsSuccess);
        Assert.IsFalse(second.IsSuccess);
        StringAssert.Contains("Kitchen", second.Message);
        StringAssert.Contains("MaidRoom", second.Message);
        StringAssert.Contains("shared.target", second.Message);
    }

    [Test]
    public void Register_DuplicateTargetIdAcrossDifferentAdapters_DoesNotPartiallyRegisterTheConflictingAdapter()
    {
        var kitchen = new TestSceneAdapter("Kitchen", new[] { "shared.target" });
        var maidRoom = new TestSceneAdapter("MaidRoom", new[] { "shared.target", "maidroom.food" });

        registry.Register(kitchen);
        registry.Register(maidRoom);

        // The conflicting adapter must not be resolvable at all -- not by scene name, and not
        // even for its own non-conflicting target id -- because registration is atomic.
        Assert.IsFalse(registry.TryResolveScene("MaidRoom", out _));
        Assert.IsFalse(registry.TryResolveTarget(QaTargetId.Create("maidroom.food"), out _));

        // The original registration is untouched.
        Assert.IsTrue(registry.TryResolveTarget(QaTargetId.Create("shared.target"), out QaResolvedTarget target));
        Assert.AreSame(kitchen, target.Adapter);
    }

    [Test]
    public void Register_AdapterWithInternalDuplicateTargetId_FailsBeforeRegisteringScene()
    {
        var adapter = new TestSceneAdapter("Kitchen", new[] { "kitchen.sink.faucet", "kitchen.sink.faucet" });

        QaSceneRegistrationResult result = registry.Register(adapter);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsFalse(registry.TryResolveScene("Kitchen", out _));
    }

    [Test]
    public void Register_DuplicateSceneName_ReturnsFailureAndKeepsOriginalAdapter()
    {
        var first = new TestSceneAdapter("Kitchen", new[] { "kitchen.sink.faucet" });
        var second = new TestSceneAdapter("Kitchen", new[] { "kitchen.other" });

        registry.Register(first);
        QaSceneRegistrationResult result = registry.Register(second);

        Assert.IsFalse(result.IsSuccess);
        registry.TryResolveScene("Kitchen", out IQaSceneAdapter resolved);
        Assert.AreSame(first, resolved);
        Assert.IsFalse(registry.TryResolveTarget(QaTargetId.Create("kitchen.other"), out _));
    }

    // ---------------------------------------------------------------
    //  Step 1: unsupported scenes return failure; no best-effort name search.
    // ---------------------------------------------------------------

    [Test]
    public void TryResolveScene_UnknownScene_ReturnsFalseWithoutAdapter()
    {
        bool resolved = registry.TryResolveScene("NeverRegisteredScene", out IQaSceneAdapter adapter);

        Assert.IsFalse(resolved);
        Assert.IsNull(adapter);
    }

    [Test]
    public void TryResolveScene_CaseMismatch_DoesNotFallBackToBestEffortSearch()
    {
        registry.Register(new TestSceneAdapter("Kitchen", Array.Empty<string>()));

        // Only the exact registered name resolves; a differently-cased lookup must not silently
        // match via a best-effort/fuzzy search.
        bool resolved = registry.TryResolveScene("kitchen", out IQaSceneAdapter adapter);

        Assert.IsFalse(resolved);
        Assert.IsNull(adapter);
    }

    [Test]
    public void TryResolveScene_BlankName_ReturnsFalse()
    {
        Assert.IsFalse(registry.TryResolveScene(string.Empty, out _));
        Assert.IsFalse(registry.TryResolveScene(null, out _));
    }

    [Test]
    public void TryResolveTarget_UnknownId_ReturnsFalse()
    {
        bool resolved = registry.TryResolveTarget(QaTargetId.Create("never.registered"), out QaResolvedTarget target);

        Assert.IsFalse(resolved);
        Assert.IsNull(target);
    }

    // ---------------------------------------------------------------
    //  Step 3: Build Settings coverage audit (soft during rollout).
    //  This is a pure-logic audit over an injected scene-name list; the companion test below
    //  additionally exercises the real UnityEditor.EditorBuildSettings enabled-scene list.
    // ---------------------------------------------------------------

    [Test]
    public void AuditMissingAdapterScenes_ReturnsOnlyScenesWithoutRegisteredAdapters()
    {
        registry.Register(new TestSceneAdapter("Kitchen", Array.Empty<string>()));

        IReadOnlyList<string> missing = registry.AuditMissingAdapterScenes(
            new[] { "Kitchen", "MaidRoom", "TutorRoom" });

        Assert.IsNotNull(missing);
        CollectionAssert.DoesNotContain(missing, "Kitchen");
        CollectionAssert.Contains(missing, "MaidRoom");
        CollectionAssert.Contains(missing, "TutorRoom");
    }

    [Test]
    public void AuditMissingAdapterScenes_NullInput_ReturnsEmptyListNotNull()
    {
        IReadOnlyList<string> missing = registry.AuditMissingAdapterScenes(null);

        Assert.IsNotNull(missing);
        Assert.IsEmpty(missing);
    }

    /// <summary>
    /// TODO(Task 13): today this only asserts the audit API is callable and returns a non-null
    /// list of missing scenes -- it structurally always passes and never fails the build. Once
    /// adapters exist for every gameplay scene (design doc §10 rollout step 6), this test (or a
    /// successor `BuildSceneQaCoverageTests`, per the plan's Task 13 file list) must assert the
    /// missing list is empty and fail the suite otherwise.
    /// </summary>
    [Test]
    public void BuildSettingsCoverage_ReportsMissingAdapters()
    {
        List<string> enabledSceneNames = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
            .ToList();

        IReadOnlyList<string> missing = registry.AuditMissingAdapterScenes(enabledSceneNames);

        Assert.IsNotNull(missing);
    }

    // ---------------------------------------------------------------
    //  Test double: no concrete adapters exist yet (Task 12), so registry behavior is verified
    //  against a minimal stand-in that implements the Task 5 IQaSceneAdapter contract.
    // ---------------------------------------------------------------

    private sealed class TestSceneAdapter : IQaSceneAdapter
    {
        private readonly List<QaTargetId> targetIds;

        public TestSceneAdapter(string sceneName, IEnumerable<string> rawTargetIds)
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
        }

        public string SceneName { get; }

        public IReadOnlyCollection<QaTargetId> TargetIds => targetIds;

        public IReadOnlyCollection<string> PresetIds => Array.Empty<string>();

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow);
        }
    }
}
