using System;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using NUnit.Framework;

/// <summary>
/// <see cref="QaDriverCore"/>의 명령 검증, 단일 실행 직렬화, 동시 세션 시작 거부를 검증합니다.
/// QA 코어는 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만 컴파일되며, 본 EditMode 테스트는
/// 항상 에디터에서 실행되므로 해당 타입을 볼 수 있습니다.
/// </summary>
[TestFixture]
public class QaDriverCoreTests
{
    private QaDriverCore driver;

    [SetUp]
    public void SetUp()
    {
        driver = new QaDriverCore();
    }

    [TearDown]
    public void TearDown()
    {
        driver?.Dispose();
        driver = null;
    }

    // ---------------------------------------------------------------
    //  Validation
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_NullCommand_ReturnsInvalidCommand()
    {
        QaCommandResult result = await driver.ExecuteAsync(null, CancellationToken.None);

        Assert.AreEqual(QaResultCode.InvalidCommand, result.Code);
    }

    [Test]
    public async Task ExecuteAsync_BlankCommandId_ReturnsInvalidCommand()
    {
        QaCommand command = QaCommand.BeginSession("   ");

        QaCommandResult result = await driver.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual(QaResultCode.InvalidCommand, result.Code);
    }

    [Test]
    public async Task ExecuteAsync_NullCommandId_ReturnsInvalidCommand()
    {
        QaCommand command = QaCommand.Create(null, QaCommandType.SessionBegin);

        QaCommandResult result = await driver.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual(QaResultCode.InvalidCommand, result.Code);
    }

    [Test]
    public async Task ExecuteAsync_UnsupportedCommandType_ReturnsUnsupportedCommand()
    {
        QaCommand command = QaCommand.Create("cmd-1", QaCommandType.SceneLoad);

        QaCommandResult result = await driver.ExecuteAsync(command, CancellationToken.None);

        Assert.AreEqual(QaResultCode.UnsupportedCommand, result.Code);
    }

    // ---------------------------------------------------------------
    //  Session lifecycle
    // ---------------------------------------------------------------

    [Test]
    public async Task BeginSession_WithValidId_ReturnsSuccessAndActivatesRun()
    {
        QaCommandResult result = await driver.ExecuteAsync(
            QaCommand.BeginSession("first"), CancellationToken.None);

        Assert.AreEqual(QaResultCode.Success, result.Code);
        Assert.IsTrue(driver.CurrentRun.IsActive);
        Assert.AreNotEqual(QaRunId.None, driver.CurrentRun.RunId);
    }

    [Test]
    public async Task SecondBeginSession_WhileActive_ReturnsRunAlreadyActiveWithoutChangingFirstRun()
    {
        QaCommandResult first = await driver.ExecuteAsync(
            QaCommand.BeginSession("first"), CancellationToken.None);
        Assert.AreEqual(QaResultCode.Success, first.Code);
        QaRunId firstRunId = driver.CurrentRun.RunId;

        QaCommandResult second = await driver.ExecuteAsync(
            QaCommand.BeginSession("second"), CancellationToken.None);
        Assert.AreEqual(QaResultCode.RunAlreadyActive, second.Code);

        Assert.AreEqual(firstRunId, driver.CurrentRun.RunId);
        Assert.IsTrue(driver.CurrentRun.IsActive);
        Assert.AreEqual("first", driver.CurrentRun.BeganByCommandId);
    }

    [Test]
    public async Task EndSession_WithoutActiveRun_ReturnsNoActiveRun()
    {
        QaCommandResult result = await driver.ExecuteAsync(
            QaCommand.EndSession("end-1"), CancellationToken.None);

        Assert.AreEqual(QaResultCode.NoActiveRun, result.Code);
    }

    [Test]
    public async Task AbortSession_WithoutActiveRun_ReturnsNoActiveRun()
    {
        QaCommandResult result = await driver.ExecuteAsync(
            QaCommand.AbortSession("abort-1"), CancellationToken.None);

        Assert.AreEqual(QaResultCode.NoActiveRun, result.Code);
    }

    [Test]
    public async Task EndSession_AfterBegin_EndsRunAndAllowsNewRunToBegin()
    {
        await driver.ExecuteAsync(QaCommand.BeginSession("first"), CancellationToken.None);

        QaCommandResult endResult = await driver.ExecuteAsync(
            QaCommand.EndSession("end-1"), CancellationToken.None);
        Assert.AreEqual(QaResultCode.Success, endResult.Code);
        Assert.IsFalse(driver.CurrentRun.IsActive);

        QaCommandResult secondBegin = await driver.ExecuteAsync(
            QaCommand.BeginSession("second"), CancellationToken.None);
        Assert.AreEqual(QaResultCode.Success, secondBegin.Code);
        Assert.IsTrue(driver.CurrentRun.IsActive);
    }

    // ---------------------------------------------------------------
    //  Sequence numbers
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_AssignsMonotonicallyIncreasingSequenceNumbers()
    {
        QaCommandResult first = await driver.ExecuteAsync(
            QaCommand.BeginSession("first"), CancellationToken.None);
        QaCommandResult second = await driver.ExecuteAsync(
            QaCommand.EndSession("end-1"), CancellationToken.None);
        QaCommandResult third = await driver.ExecuteAsync(
            QaCommand.Create("cmd-3", QaCommandType.SceneLoad), CancellationToken.None);

        Assert.Less(first.SequenceNumber, second.SequenceNumber);
        Assert.Less(second.SequenceNumber, third.SequenceNumber);
    }

    // ---------------------------------------------------------------
    //  Cancellation
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_AlreadyCancelledToken_ReturnsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        QaCommandResult result = await driver.ExecuteAsync(
            QaCommand.BeginSession("first"), cts.Token);

        Assert.AreEqual(QaResultCode.Cancelled, result.Code);
        Assert.IsFalse(driver.CurrentRun.IsActive);
    }

    // ---------------------------------------------------------------
    //  Internal error handling
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_HandlerThrows_ReturnsSanitizedInternalErrorAndRaisesEvent()
    {
        QaCommandResult observed = null;
        driver.CommandCompleted += result => observed = result;
        driver.FaultInjectorForTests = _ => new InvalidOperationException("secret stack detail");

        QaCommandResult result = await driver.ExecuteAsync(
            QaCommand.BeginSession("first"), CancellationToken.None);

        Assert.AreEqual(QaResultCode.InternalError, result.Code);
        StringAssert.DoesNotContain("secret stack detail", result.Message);
        Assert.AreSame(result, observed);
    }

    // ---------------------------------------------------------------
    //  Serialized execution
    // ---------------------------------------------------------------

    [Test]
    public async Task ExecuteAsync_ConcurrentBeginSession_OnlyOneRunBecomesActive()
    {
        Task<QaCommandResult> taskA = driver.ExecuteAsync(QaCommand.BeginSession("a"), CancellationToken.None);
        Task<QaCommandResult> taskB = driver.ExecuteAsync(QaCommand.BeginSession("b"), CancellationToken.None);

        QaCommandResult[] results = await Task.WhenAll(taskA, taskB);

        int successCount = 0;
        int alreadyActiveCount = 0;
        foreach (QaCommandResult result in results)
        {
            if (result.Code == QaResultCode.Success) successCount++;
            if (result.Code == QaResultCode.RunAlreadyActive) alreadyActiveCount++;
        }

        Assert.AreEqual(1, successCount);
        Assert.AreEqual(1, alreadyActiveCount);
        Assert.IsTrue(driver.CurrentRun.IsActive);
    }
}
