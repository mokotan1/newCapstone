using System;
using System.Collections.Generic;
using Godlotto.QA.Scenarios;
using Godlotto.QA.Scenes;
using NUnit.Framework;

/// <summary>
/// Task 9 §Step 1의 <see cref="QaScenarioValidator"/>를 검증합니다. 알려지지 않은
/// schemaVersion/명령/씬/프리셋/대상/어서션 종류, 중복 스텝 id, 0 이하의 timeoutMs를 Play Mode
/// mutation이 시작되기 전에 전부 거부해야 하며, 첫 오류에서 멈추지 않고 발견된 문제 전체를
/// 보고해야 합니다. 씬/대상/프리셋은 실제 씬을 로드하지 않고 <see cref="FakeSceneAdapter"/>를
/// 등록한 <see cref="QaSceneRegistry"/>로만 해석합니다(EditMode에서 결정적으로 실행 가능).
/// </summary>
[TestFixture]
public class QaScenarioValidatorTests
{
    private const string KnownScene = "Kitchen";
    private const string KnownTarget = "kitchen.sink.faucet";
    private const string KnownDestinationTarget = "kitchen.counter";
    private const string KnownPreset = "before-faucet";

    private QaSceneRegistry registry;
    private QaScenarioValidator validator;

    [SetUp]
    public void SetUp()
    {
        registry = new QaSceneRegistry();
        registry.Register(new FakeSceneAdapter(
            KnownScene,
            new[] { KnownTarget, KnownDestinationTarget },
            new[] { KnownPreset }));
        validator = new QaScenarioValidator(registry);
    }

    // ---------------------------------------------------------------
    //  Happy path
    // ---------------------------------------------------------------

    [Test]
    public void Validate_WellFormedScenario_Succeeds()
    {
        QaScenarioDefinition scenario = ValidScenario();

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.AreSame(scenario, result.Scenario);
        Assert.IsEmpty(result.Errors);
    }

    [Test]
    public void Validate_WellFormedJsonString_Succeeds()
    {
        string json = "{"
            + "\"schemaVersion\":1,"
            + "\"id\":\"kitchen.faucet-key\","
            + "\"scene\":\"" + KnownScene + "\","
            + "\"preset\":\"" + KnownPreset + "\","
            + "\"steps\":["
            + "{\"id\":\"s1\",\"command\":\"interaction.pointer\",\"target\":\"" + KnownTarget + "\",\"timeoutMs\":10000},"
            + "{\"id\":\"s2\",\"command\":\"state.assert\",\"timeoutMs\":10000,"
            + "\"assertion\":{\"kind\":\"inputUnlocked\"}}"
            + "]}";

        QaScenarioValidationResult result = validator.Validate(json);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Scenario);
        Assert.AreEqual(KnownScene, result.Scenario.Scene);
    }

    // ---------------------------------------------------------------
    //  Malformed JSON never throws
    // ---------------------------------------------------------------

    [Test]
    public void Validate_MalformedJson_ReturnsFailureWithoutThrowing()
    {
        QaScenarioValidationResult result = null;

        Assert.DoesNotThrow(() => result = validator.Validate("{ not valid json"));

        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Errors);
    }

    [Test]
    public void Validate_BlankJson_ReturnsFailure()
    {
        QaScenarioValidationResult result = validator.Validate("   ");

        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Errors);
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown schemaVersion
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownSchemaVersion_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.SchemaVersion = 2;

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "schemaVersion");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown command
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownCommand_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Steps[0].Command = "system.deleteEverything";

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "unknown command");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown scene
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownScene_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Scene = "NeverRegisteredScene";

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "Unknown scene");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown preset
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownPreset_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Preset = "never-registered-preset";

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "Unknown preset");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown target
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownTarget_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Steps[0].Target = "never.registered.target";

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "unknown target");
    }

    [Test]
    public void Validate_UnknownDestinationTargetForDrag_Fails()
    {
        var scenario = ValidScenario();
        scenario.Steps.Add(new QaScenarioStepDefinition
        {
            Id = "dragStep",
            Command = "interaction.drag",
            Target = KnownTarget,
            DestinationTarget = "never.registered.destination",
            TimeoutMs = 5000
        });

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "unknown destinationTarget");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject unknown assertion kind
    // ---------------------------------------------------------------

    [Test]
    public void Validate_UnknownAssertionKind_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Steps.Add(new QaScenarioStepDefinition
        {
            Id = "assertStep",
            Command = "state.assert",
            TimeoutMs = 5000,
            Assertion = new QaScenarioAssertionDefinition { Kind = "makeUpSomeKind" }
        });

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "unknown assertion kind");
    }

    [Test]
    public void Validate_StateAssertStepWithoutAssertionObject_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Steps.Add(new QaScenarioStepDefinition
        {
            Id = "assertStep",
            Command = "state.assert",
            TimeoutMs = 5000,
            Assertion = null
        });

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "'assertion' is required");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject duplicate step id
    // ---------------------------------------------------------------

    [Test]
    public void Validate_DuplicateStepId_Fails()
    {
        QaScenarioDefinition scenario = ValidScenario();
        QaScenarioStepDefinition duplicate = Clone(scenario.Steps[0]);
        scenario.Steps.Add(duplicate);

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "duplicate step id");
    }

    // ---------------------------------------------------------------
    //  Step 1: reject non-positive timeout
    // ---------------------------------------------------------------

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-1000)]
    public void Validate_NonPositiveTimeout_Fails(int timeoutMs)
    {
        QaScenarioDefinition scenario = ValidScenario();
        scenario.Steps[0].TimeoutMs = timeoutMs;

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        AssertAnyErrorContains(result, "timeoutMs");
    }

    // ---------------------------------------------------------------
    //  Comprehensive reporting: multiple problems are all surfaced at once.
    // ---------------------------------------------------------------

    [Test]
    public void Validate_MultipleProblems_ReportsAllOfThemNotJustTheFirst()
    {
        var scenario = new QaScenarioDefinition
        {
            SchemaVersion = 99,
            Id = "broken",
            Scene = "NeverRegisteredScene",
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition { Id = "a", Command = "bogus.command", TimeoutMs = -1 },
                new QaScenarioStepDefinition { Id = "a", Command = "interaction.pointer", Target = "unknown.target", TimeoutMs = 1000 }
            }
        };

        QaScenarioValidationResult result = validator.Validate(scenario);

        Assert.IsFalse(result.IsValid);
        Assert.GreaterOrEqual(
            result.Errors.Count, 5,
            "schemaVersion, scene, command, timeout, duplicate id, and unknown target should all be reported together.");
    }

    [Test]
    public void Validate_NullScenario_ReturnsFailureWithoutThrowing()
    {
        QaScenarioValidationResult result = null;

        Assert.DoesNotThrow(() => result = validator.Validate((QaScenarioDefinition)null));

        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Errors);
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static void AssertAnyErrorContains(QaScenarioValidationResult result, string expectedSubstring)
    {
        foreach (string error in result.Errors)
        {
            if (error.IndexOf(expectedSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }
        }

        Assert.Fail("Expected an error containing '" + expectedSubstring + "' but got: " + string.Join(" | ", result.Errors));
    }

    private static QaScenarioStepDefinition Clone(QaScenarioStepDefinition source)
    {
        return new QaScenarioStepDefinition
        {
            Id = source.Id,
            Command = source.Command,
            Target = source.Target,
            DestinationTarget = source.DestinationTarget,
            Text = source.Text,
            Assertion = source.Assertion,
            TimeoutMs = source.TimeoutMs
        };
    }

    private static QaScenarioDefinition ValidScenario()
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.faucet-key",
            Scene = KnownScene,
            Preset = KnownPreset,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "s1",
                    Command = "interaction.pointer",
                    Target = KnownTarget,
                    TimeoutMs = 10000
                },
                new QaScenarioStepDefinition
                {
                    Id = "s2",
                    Command = "state.assert",
                    TimeoutMs = 10000,
                    Assertion = new QaScenarioAssertionDefinition
                    {
                        Kind = "inventoryContains",
                        Value = "3"
                    }
                }
            }
        };
    }

    // ---------------------------------------------------------------
    //  Test double: no concrete adapters exist yet (Task 12), so validation is verified against
    //  a minimal stand-in that implements the Task 5 IQaSceneAdapter contract.
    // ---------------------------------------------------------------

    private sealed class FakeSceneAdapter : IQaSceneAdapter
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
    }
}
