using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godlotto.QA.Evidence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// <see cref="DevelopmentQaEvidenceRecorder"/>(및 그 위의 <see cref="EditorQaEvidenceRecorder"/>
/// 경로 해석 래퍼)가 강제하는 계약을 검증합니다: run id 경로 안전성, append-only 이벤트 로그,
/// 토큰/헤더 필드 redaction, finalize 이후 불변성, 그리고 "예외 없음 == PASS"를 절대 추론하지
/// 않는 증거 기반 verdict 산출. 실제 저장소 <c>docs/qa/runs</c>를 오염시키지 않도록 매 테스트마다
/// 전용 임시 디렉터리를 루트로 주입합니다. QA Evidence 타입은 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만
/// 컴파일되며, 본 EditMode 테스트는 항상 에디터에서 실행되므로 해당 타입을 볼 수 있습니다.
/// </summary>
[TestFixture]
public class QaEvidenceRecorderTests
{
    private string tempRoot;
    private FakeClock clock;
    private DevelopmentQaEvidenceRecorder recorder;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "QaEvidenceRecorderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        recorder = new DevelopmentQaEvidenceRecorder(tempRoot, clock.UtcNow);
    }

    [TearDown]
    public void TearDown()
    {
        recorder = null;

        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; leftover temp dirs from a failed test must never fail the suite.
        }
    }

    // ---------------------------------------------------------------
    //  Step 1a: run id path safety
    // ---------------------------------------------------------------

    [TestCase("has/slash")]
    [TestCase("has\\backslash")]
    [TestCase("..")]
    [TestCase("../escape")]
    [TestCase("has space")]
    [TestCase("")]
    [TestCase("   ")]
    public void BeginRun_UnsafeRunId_ReturnsInvalidRequestAndCreatesNoDirectory(string unsafeRunId)
    {
        QaEvidenceOperationResult result = recorder.BeginRun(unsafeRunId);

        Assert.AreEqual(QaEvidenceOperationCode.InvalidRequest, result.Code);
        Assert.IsFalse(Directory.Exists(tempRoot) && Directory.GetDirectories(tempRoot).Any(),
            "An unsafe runId must never cause a run directory to be created.");
    }

    [Test]
    public void BeginRun_NullRunId_ReturnsInvalidRequest()
    {
        QaEvidenceOperationResult result = recorder.BeginRun(null);

        Assert.AreEqual(QaEvidenceOperationCode.InvalidRequest, result.Code);
    }

    [Test]
    public void BeginRun_SafeRunId_CreatesDirectoryNamedWithUtcTimestampAndRunId()
    {
        QaEvidenceOperationResult result = recorder.BeginRun("smoke-001");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(recorder.RunDirectoryPath);

        string directoryName = Path.GetFileName(recorder.RunDirectoryPath);
        Assert.AreEqual("20260101T000000Z-run-smoke-001", directoryName);
        Assert.IsTrue(Directory.Exists(recorder.RunDirectoryPath));
        Assert.IsTrue(Directory.Exists(Path.Combine(recorder.RunDirectoryPath, "screenshots")));
        Assert.IsTrue(Directory.Exists(Path.Combine(recorder.RunDirectoryPath, "patches")));
        Assert.IsTrue(File.Exists(Path.Combine(recorder.RunDirectoryPath, "console.log")));
        Assert.IsTrue(File.Exists(Path.Combine(recorder.RunDirectoryPath, "report.md")));
        Assert.IsTrue(File.Exists(Path.Combine(recorder.RunDirectoryPath, "manifest.json")));
        Assert.IsTrue(File.Exists(Path.Combine(recorder.RunDirectoryPath, "journal.jsonl")));
        Assert.IsTrue(File.Exists(Path.Combine(recorder.RunDirectoryPath, "events.jsonl")));
    }

    [Test]
    public void BeginRun_WhileAlreadyActive_ReturnsAlreadyActiveWithoutDisturbingCurrentRun()
    {
        recorder.BeginRun("first-run");
        string firstDirectory = recorder.RunDirectoryPath;

        QaEvidenceOperationResult second = recorder.BeginRun("second-run");

        Assert.AreEqual(QaEvidenceOperationCode.AlreadyActive, second.Code);
        Assert.AreEqual(firstDirectory, recorder.RunDirectoryPath);
    }

    // ---------------------------------------------------------------
    //  Step 1b: append-only events.jsonl
    // ---------------------------------------------------------------

    [Test]
    public void AppendEvent_BeforeBeginRun_ReturnsNotActive()
    {
        QaEvidenceOperationResult result = recorder.AppendEvent(
            QaEvidenceEvent.ForAssertion("cmd-1", true, "should be rejected"));

        Assert.AreEqual(QaEvidenceOperationCode.NotActive, result.Code);
    }

    [Test]
    public void AppendEvent_SecondCall_AppendsRatherThanOverwritingFirstLine()
    {
        recorder.BeginRun("append-only-run");
        string eventsPath = Path.Combine(recorder.RunDirectoryPath, DevelopmentQaEvidenceRecorder.EventsFileName);

        // BeginRun itself already appended a RunBegan event as line 1 (sequence 1).
        string[] afterBegin = File.ReadAllLines(eventsPath);
        Assert.AreEqual(1, afterBegin.Length, "BeginRun must append exactly one RunBegan line.");
        string firstLineAfterBegin = afterBegin[0];

        QaEvidenceOperationResult first = recorder.AppendEvent(
            QaEvidenceEvent.ForAssertion("cmd-1", true, "first assertion"));
        QaEvidenceOperationResult second = recorder.AppendEvent(
            QaEvidenceEvent.ForAssertion("cmd-2", true, "second assertion"));

        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);

        string[] lines = File.ReadAllLines(eventsPath);
        Assert.AreEqual(3, lines.Length, "Each AppendEvent call must add exactly one line without removing prior lines.");
        Assert.AreEqual(firstLineAfterBegin, lines[0], "Earlier lines must never be rewritten by later appends.");

        JObject firstAssertionJson = JObject.Parse(lines[1]);
        JObject secondAssertionJson = JObject.Parse(lines[2]);
        Assert.AreEqual("cmd-1", firstAssertionJson["CommandId"]?.ToString());
        Assert.AreEqual("cmd-2", secondAssertionJson["CommandId"]?.ToString());

        // Sequence numbers are monotonic across the whole run, not per-call: BeginRun's own
        // RunBegan event already consumed sequence 1, so these two appends are 2 and 3.
        Assert.AreEqual(2, (long)firstAssertionJson["SequenceNumber"]);
        Assert.AreEqual(3, (long)secondAssertionJson["SequenceNumber"]);
    }

    [Test]
    public void AppendEvent_AfterFinalize_ReturnsAlreadyFinalizedAndDoesNotAppendFurtherLines()
    {
        recorder.BeginRun("finalize-then-append");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "passed", null));
        QaEvidenceFinalizeResult finalizeResult = recorder.Finalize();
        Assert.IsTrue(finalizeResult.IsSuccess);

        string eventsPath = Path.Combine(finalizeResult.RunDirectoryPath, DevelopmentQaEvidenceRecorder.EventsFileName);
        string[] linesBeforeExtraAppend = File.ReadAllLines(eventsPath);

        QaEvidenceOperationResult afterFinalize = recorder.AppendEvent(
            QaEvidenceEvent.ForAssertion("cmd-2", true, "must be rejected"));

        Assert.AreEqual(QaEvidenceOperationCode.AlreadyFinalized, afterFinalize.Code);
        string[] linesAfterExtraAppend = File.ReadAllLines(eventsPath);
        Assert.AreEqual(linesBeforeExtraAppend.Length, linesAfterExtraAppend.Length,
            "A rejected append after Finalize must never mutate the immutable events log.");
    }

    // ---------------------------------------------------------------
    //  Step 1c: redaction of configured token/header fields
    // ---------------------------------------------------------------

    [Test]
    public void AppendEvent_DataContainsConfiguredSensitiveField_IsRedactedOnDisk()
    {
        recorder.BeginRun("redaction-run");

        var data = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer super-secret-value",
            ["nonSensitive"] = "keep-me"
        };

        recorder.AppendEvent(QaEvidenceEvent.Create(QaEvidenceEventType.Note, message: "calling API", data: data));

        string eventsPath = Path.Combine(recorder.RunDirectoryPath, DevelopmentQaEvidenceRecorder.EventsFileName);
        string[] lines = File.ReadAllLines(eventsPath);
        JObject noteLine = JObject.Parse(lines[lines.Length - 1]);
        JObject dataJson = (JObject)noteLine["Data"];

        Assert.AreEqual(QaEvidenceRedactor.RedactedPlaceholder, dataJson["Authorization"]?.ToString());
        Assert.AreEqual("keep-me", dataJson["nonSensitive"]?.ToString());
        StringAssert.DoesNotContain("super-secret-value", File.ReadAllText(eventsPath));
    }

    [Test]
    public void RecordConsole_TextContainsInlineToken_IsRedactedInConsoleLogFile()
    {
        recorder.BeginRun("console-redaction-run");

        recorder.RecordConsole("Request failed. token=abc123XYZ was rejected by the server.");

        string consolePath = Path.Combine(recorder.RunDirectoryPath, DevelopmentQaEvidenceRecorder.ConsoleFileName);
        string consoleContent = File.ReadAllText(consolePath);

        StringAssert.DoesNotContain("abc123XYZ", consoleContent);
        StringAssert.Contains(QaEvidenceRedactor.RedactedPlaceholder, consoleContent);
    }

    // ---------------------------------------------------------------
    //  AttachScreenshot
    // ---------------------------------------------------------------

    [Test]
    public void AttachScreenshot_WritesFileUnderScreenshotsDirectoryAndAppendsEvent()
    {
        recorder.BeginRun("screenshot-run");
        byte[] fakePngBytes = { 1, 2, 3, 4 };

        QaEvidenceOperationResult result = recorder.AttachScreenshot("cmd-1", fakePngBytes, "kitchen-sink");

        Assert.IsTrue(result.IsSuccess);
        string screenshotsDir = Path.Combine(recorder.RunDirectoryPath, "screenshots");
        string[] files = Directory.GetFiles(screenshotsDir);
        Assert.AreEqual(1, files.Length);
        CollectionAssert.AreEqual(fakePngBytes, File.ReadAllBytes(files[0]));
    }

    [Test]
    public void AttachScreenshot_EmptyBytes_ReturnsInvalidRequest()
    {
        recorder.BeginRun("screenshot-empty-run");

        QaEvidenceOperationResult result = recorder.AttachScreenshot("cmd-1", Array.Empty<byte>());

        Assert.AreEqual(QaEvidenceOperationCode.InvalidRequest, result.Code);
    }

    [Test]
    public void AttachScreenshot_FileNameHintTriesPathEscape_IsSanitizedIntoScreenshotsDirectory()
    {
        recorder.BeginRun("screenshot-escape-run");
        byte[] fakePngBytes = { 9, 9, 9 };

        recorder.AttachScreenshot("cmd-1", fakePngBytes, "../../evil");

        string screenshotsDir = Path.Combine(recorder.RunDirectoryPath, "screenshots");
        string[] filesInScreenshotsDir = Directory.GetFiles(screenshotsDir);
        Assert.AreEqual(1, filesInScreenshotsDir.Length,
            "A hostile fileNameHint must never escape the screenshots directory.");
    }

    // ---------------------------------------------------------------
    //  Step 3: verdict aggregation - never infer PASS from "no exception"
    // ---------------------------------------------------------------

    [Test]
    public void Finalize_NoEventsRecordedBesidesLifecycle_YieldsNotRun()
    {
        recorder.BeginRun("empty-run");

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(QaRunVerdictCode.NotRun, result.Manifest.Verdict);
    }

    [Test]
    public void Finalize_OnlyScreenshotNoAssertion_YieldsBlockedNotPass()
    {
        recorder.BeginRun("screenshot-only-run");
        recorder.AttachScreenshot("cmd-1", new byte[] { 1 });

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreEqual(QaRunVerdictCode.Blocked, result.Manifest.Verdict);
    }

    [Test]
    public void Finalize_OnlyPassingAssertionNoScreenshot_YieldsBlockedNotPass()
    {
        recorder.BeginRun("assertion-only-run");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "state matched expectation"));

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreEqual(QaRunVerdictCode.Blocked, result.Manifest.Verdict);
    }

    [Test]
    public void Finalize_PassingAssertionAndScreenshot_YieldsPass()
    {
        recorder.BeginRun("evidence-backed-pass-run");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "state matched expectation"));
        recorder.AttachScreenshot("cmd-1", new byte[] { 1, 2, 3 });

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreEqual(QaRunVerdictCode.Pass, result.Manifest.Verdict);
    }

    [Test]
    public void Finalize_AnyFailedAssertion_YieldsFailEvenWithPassingAssertionsAndScreenshot()
    {
        recorder.BeginRun("mixed-with-failure-run");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "this part matched"));
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-2", false, "this part did not match"));
        recorder.AttachScreenshot("cmd-1", new byte[] { 1 });

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreEqual(QaRunVerdictCode.Fail, result.Manifest.Verdict);
    }

    [Test]
    public void Finalize_CommandResultEventsAloneNeverInferPass()
    {
        // A command completing without throwing must never be treated as evidence of success:
        // CommandResult events never carry a Passed flag, so they cannot push the verdict to Pass.
        recorder.BeginRun("no-exception-is-not-pass-run");
        recorder.AppendEvent(QaEvidenceEvent.ForCommandResult("cmd-1", "Success", "command completed without error"));

        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreNotEqual(QaRunVerdictCode.Pass, result.Manifest.Verdict);
        Assert.AreEqual(QaRunVerdictCode.NotRun, result.Manifest.Verdict);
    }

    // ---------------------------------------------------------------
    //  Immutable manifest.json / report.md
    // ---------------------------------------------------------------

    [Test]
    public void Finalize_WritesManifestJsonAndReportMd()
    {
        recorder.BeginRun("manifest-run");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "ok"));
        recorder.AttachScreenshot("cmd-1", new byte[] { 1 });

        QaEvidenceFinalizeResult result = recorder.Finalize();

        string manifestPath = Path.Combine(result.RunDirectoryPath, DevelopmentQaEvidenceRecorder.ManifestFileName);
        string reportPath = Path.Combine(result.RunDirectoryPath, DevelopmentQaEvidenceRecorder.ReportFileName);
        Assert.IsTrue(File.Exists(manifestPath));
        Assert.IsTrue(File.Exists(reportPath));

        JObject manifestJson = JObject.Parse(File.ReadAllText(manifestPath));
        Assert.AreEqual("manifest-run", manifestJson["RunId"]?.ToString());
        Assert.AreEqual("Pass", manifestJson["Verdict"]?.ToString());

        StringAssert.Contains("PASS", File.ReadAllText(reportPath));
    }

    [Test]
    public void Finalize_CalledTwice_SecondCallIsRejectedAndFilesRemainUnchanged()
    {
        recorder.BeginRun("double-finalize-run");
        recorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "ok"));
        recorder.AttachScreenshot("cmd-1", new byte[] { 1 });

        QaEvidenceFinalizeResult first = recorder.Finalize();
        Assert.IsTrue(first.IsSuccess);

        string manifestPath = Path.Combine(first.RunDirectoryPath, DevelopmentQaEvidenceRecorder.ManifestFileName);
        string manifestContentAfterFirst = File.ReadAllText(manifestPath);

        QaEvidenceFinalizeResult second = recorder.Finalize();

        Assert.IsFalse(second.IsSuccess);
        Assert.AreEqual(QaEvidenceOperationCode.AlreadyFinalized, second.Operation.Code);
        Assert.AreEqual(manifestContentAfterFirst, File.ReadAllText(manifestPath),
            "manifest.json must be immutable once written by Finalize.");
    }

    [Test]
    public void Finalize_WithoutActiveRun_ReturnsNotActive()
    {
        QaEvidenceFinalizeResult result = recorder.Finalize();

        Assert.AreEqual(QaEvidenceOperationCode.NotActive, result.Operation.Code);
        Assert.IsNull(result.Manifest);
    }

    // ---------------------------------------------------------------
    //  QaRunManifest.AggregateVerdict as a pure function (no file I/O)
    // ---------------------------------------------------------------

    [Test]
    public void AggregateVerdict_EmptyEventList_ReturnsNotRun()
    {
        QaRunVerdictCode verdict = QaRunManifest.AggregateVerdict(Array.Empty<QaEvidenceEvent>(), out string reason);

        Assert.AreEqual(QaRunVerdictCode.NotRun, verdict);
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void AggregateVerdict_NullEventList_ReturnsNotRunWithoutThrowing()
    {
        QaRunVerdictCode verdict = default;
        Assert.DoesNotThrow(() => verdict = QaRunManifest.AggregateVerdict(null, out _));

        Assert.AreEqual(QaRunVerdictCode.NotRun, verdict);
    }

    // ---------------------------------------------------------------
    //  EditorQaEvidenceRecorder - path resolution + delegation
    // ---------------------------------------------------------------

    [Test]
    public void EditorRecorder_ResolveRepoRunsRootDirectory_EndsWithDocsQaRuns()
    {
        string root = EditorQaEvidenceRecorder.ResolveRepoRunsRootDirectory();

        string expectedSuffix = Path.Combine("docs", "qa", "runs");
        StringAssert.EndsWith(expectedSuffix, root);
    }

    [Test]
    public void EditorRecorder_WithRootOverride_DelegatesBeginRunAndFinalizeToUnderlyingRecorder()
    {
        var editorRecorder = new EditorQaEvidenceRecorder(clock.UtcNow, null, tempRoot);

        QaEvidenceOperationResult begin = editorRecorder.BeginRun("editor-delegation-run");
        Assert.IsTrue(begin.IsSuccess);
        Assert.IsNotNull(editorRecorder.RunDirectoryPath);
        StringAssert.Contains("editor-delegation-run", editorRecorder.RunDirectoryPath);

        editorRecorder.AppendEvent(QaEvidenceEvent.ForAssertion("cmd-1", true, "ok"));
        editorRecorder.AttachScreenshot("cmd-1", new byte[] { 1 });

        QaEvidenceFinalizeResult finalizeResult = editorRecorder.Finalize();

        Assert.IsTrue(finalizeResult.IsSuccess);
        Assert.AreEqual(QaRunVerdictCode.Pass, finalizeResult.Manifest.Verdict);
    }

    // ---------------------------------------------------------------
    //  Test doubles
    // ---------------------------------------------------------------

    private sealed class FakeClock
    {
        private DateTime now;

        public FakeClock(DateTime initialUtc)
        {
            now = initialUtc;
        }

        public DateTime UtcNow()
        {
            return now;
        }

        public void Advance(TimeSpan by)
        {
            now = now + by;
        }
    }
}
