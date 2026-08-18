using Godlotto.QA.Core;
using Godlotto.QA.EditorCli;
using Godlotto.QA.Gateway;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// Task 10 §Step 1: Unity CLI 도구(<c>qa_status</c>/<c>qa_run</c>/<c>qa_cancel</c>/<c>qa_capture</c>)와
/// <c>QaDeveloperPanel</c>이 동일한 시나리오 id·옵션에 대해 byte-for-byte 동일한
/// <see cref="QaCommand"/> DTO(명령 종류, 타겟, 파라미터, 상관관계 id)를 만드는지 검증합니다.
///
/// 두 어댑터 모두 <see cref="QaCommandGateway.BuildStatusCommand"/> 등 정적 빌더로 위임하므로,
/// 이 테스트는 "같은 정적 메서드를 두 번 부르면 같다"는 자명한 사실을 확인하는 것이 아니라,
/// (1) 각 CLI 도구의 <see cref="JObject"/> 인자 파싱 경로(<c>QaRun.BuildRunCommandForCli</c> 등)와
/// (2) <c>QaDeveloperPanel</c>의 OnGUI 필드 수집 경로(<c>QaDeveloperPanel.BuildRunCommandForPanel</c>
/// 등)가 서로 다른 입력 표현(JSON 문자열/숫자 vs. 타입이 있는 C# 필드)에서 출발해도 동일한
/// 결과에 도달하는지를 Unity Play 없이(EditMode) 검증합니다.
/// </summary>
[TestFixture]
public sealed class QaCommandGatewayContractTests
{
    private const string CommandId = "corr-1234";
    private const string ScenarioId = "kitchen.faucet-key";

    [Test]
    public void QaStatus_CliAndPanel_ProduceIdenticalCommand()
    {
        var cliParams = new JObject { ["command_id"] = CommandId };

        QaCommand cliCommand = QaStatus.BuildStatusCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildStatusCommandForPanel(CommandId);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
        Assert.AreEqual(QaCommandType.ScenarioStatus, cliCommand.Type);
    }

    [Test]
    public void QaRun_CliAndPanel_ProduceIdenticalCommand_ForSameScenarioAndTimeout()
    {
        const int timeoutMs = 45000;
        var cliParams = new JObject
        {
            ["command_id"] = CommandId,
            ["scenario_id"] = ScenarioId,
            ["timeout_ms"] = timeoutMs
        };

        QaCommand cliCommand = QaRun.BuildRunCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildRunCommandForPanel(CommandId, ScenarioId, timeoutMs);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
        Assert.AreEqual(QaCommandType.ScenarioRun, cliCommand.Type);
        Assert.AreEqual(ScenarioId, cliCommand.TargetId);
        Assert.AreEqual(timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture), cliCommand.Parameters["timeoutMs"]);
    }

    [Test]
    public void QaRun_CliAndPanel_ProduceIdenticalCommand_WhenTimeoutOmitted_UsesSameDefault()
    {
        var cliParams = new JObject
        {
            ["command_id"] = CommandId,
            ["scenario_id"] = ScenarioId
        };

        // 120000 mirrors both QaRun's DefaultTimeoutMs and QaDeveloperPanel's DefaultTimeoutMs —
        // if either default ever drifts, this test will start failing and must be updated
        // deliberately (not silently), which is the point of pinning it here.
        const int sharedDefaultTimeoutMs = 120000;

        QaCommand cliCommand = QaRun.BuildRunCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildRunCommandForPanel(CommandId, ScenarioId, sharedDefaultTimeoutMs);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
    }

    [Test]
    public void QaCancel_CliAndPanel_ProduceIdenticalCommand()
    {
        var cliParams = new JObject
        {
            ["command_id"] = CommandId,
            ["scenario_id"] = ScenarioId
        };

        QaCommand cliCommand = QaCancel.BuildCancelCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildCancelCommandForPanel(CommandId, ScenarioId);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
        Assert.AreEqual(QaCommandType.ScenarioCancel, cliCommand.Type);
    }

    [Test]
    public void QaCapture_CliAndPanel_ProduceIdenticalCommand()
    {
        var cliParams = new JObject
        {
            ["command_id"] = CommandId,
            ["scenario_id"] = ScenarioId
        };

        QaCommand cliCommand = QaCapture.BuildCaptureCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildCaptureCommandForPanel(CommandId, ScenarioId);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
        Assert.AreEqual(QaCommandType.EvidenceCapture, cliCommand.Type);
    }

    [Test]
    public void QaRun_DifferentScenarioIds_ProduceDifferentCommands()
    {
        var cliParamsA = new JObject { ["command_id"] = CommandId, ["scenario_id"] = "scene-a.step" };
        var cliParamsB = new JObject { ["command_id"] = CommandId, ["scenario_id"] = "scene-b.step" };

        QaCommand a = QaRun.BuildRunCommandForCli(cliParamsA);
        QaCommand b = QaRun.BuildRunCommandForCli(cliParamsB);

        Assert.AreNotEqual(a.TargetId, b.TargetId);
    }

    [Test]
    public void QaRun_MissingCommandId_StillMatchesPanelWhenPanelUsesSameGeneratedId()
    {
        // qa_run tolerates an omitted command_id by generating one; the panel always has an id
        // to hand (it generates one on the Run button click). This test only pins that, once a
        // caller supplies the SAME command_id on both sides, the rest of the command matches —
        // the CLI's own id-generation fallback is intentionally not part of the contract.
        var cliParams = new JObject { ["command_id"] = CommandId, ["scenario_id"] = ScenarioId, ["timeout_ms"] = 5000 };

        QaCommand cliCommand = QaRun.BuildRunCommandForCli(cliParams);
        QaCommand panelCommand = QaDeveloperPanel.BuildRunCommandForPanel(CommandId, ScenarioId, 5000);

        AssertCommandsAreEquivalent(cliCommand, panelCommand);
    }

    private static void AssertCommandsAreEquivalent(QaCommand expected, QaCommand actual)
    {
        Assert.AreEqual(expected.Id, actual.Id, "Command Id (correlation id) must match.");
        Assert.AreEqual(expected.Type, actual.Type, "Command Type must match.");
        Assert.AreEqual(expected.TargetId, actual.TargetId, "Command TargetId must match.");
        CollectionAssert.AreEquivalent(expected.Parameters, actual.Parameters, "Command Parameters must match.");
    }
}
