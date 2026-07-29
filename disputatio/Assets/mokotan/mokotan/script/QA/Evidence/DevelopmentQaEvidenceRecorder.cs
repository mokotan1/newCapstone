#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// <see cref="IQaEvidenceRecorder"/>의 파일 기반 기본 구현. Editor QA 러너와 development
    /// player 빌드가 모두 재사용할 수 있도록 저장 루트 경로만 생성자로 주입받습니다(SRP: 이
    /// 클래스는 "어떻게 기록하는가"만 알고, "어디에 기록하는가"는 호출자가 결정). 하나의
    /// run 디렉터리는 <c>&lt;runsRootDirectory&gt;/&lt;UTC timestamp&gt;-run-&lt;safeId&gt;/</c>
    /// 형태이며, <c>events.jsonl</c>은 append-only, <c>manifest.json</c>/<c>report.md</c>는
    /// <see cref="Finalize"/> 시 정확히 한 번만 씁니다.
    /// </summary>
    public sealed class DevelopmentQaEvidenceRecorder : IQaEvidenceRecorder
    {
        public const string EventsFileName = "events.jsonl";
        public const string JournalFileName = "journal.jsonl";
        public const string ConsoleFileName = "console.log";
        public const string ScreenshotsDirectoryName = "screenshots";
        public const string PatchesDirectoryName = "patches";
        public const string ManifestFileName = "manifest.json";
        public const string ReportFileName = "report.md";

        private const string RunIdDirectoryTimestampFormat = "yyyyMMdd'T'HHmmss'Z'";
        private const string ProvisionalReportMarkdown =
            "# QA Run Report" + "\n\n" + "(stub — replace on Finalize)" + "\n";

        private readonly string runsRootDirectory;
        private readonly Func<DateTime> utcNowProvider;
        private readonly IReadOnlyCollection<string> redactedFieldNames;

        private readonly object sync = new object();
        private readonly List<QaEvidenceEvent> recordedEvents = new List<QaEvidenceEvent>();

        private bool isActive;
        private bool isFinalized;
        private string activeRunId;
        private string activeRunDirectoryPath;
        private DateTime startedAtUtc;
        private long sequenceNumber;

        public DevelopmentQaEvidenceRecorder(
            string runsRootDirectory,
            Func<DateTime> utcNowProvider = null,
            IReadOnlyCollection<string> redactedFieldNames = null)
        {
            if (string.IsNullOrWhiteSpace(runsRootDirectory))
            {
                throw new ArgumentException("runsRootDirectory must not be blank.", nameof(runsRootDirectory));
            }

            this.runsRootDirectory = runsRootDirectory;
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
            this.redactedFieldNames = redactedFieldNames ?? QaEvidenceRedactor.DefaultSensitiveFieldNames;
        }

        /// <summary>
        /// 플레이어 development 빌드 기본 경로(<c>persistentDataPath/QA/runs</c>)를 루트로 사용하는
        /// 인스턴스를 생성합니다. Editor용 저장소 경로는 <c>EditorQaEvidenceRecorder</c>가 담당합니다.
        /// </summary>
        public static DevelopmentQaEvidenceRecorder CreateDefault()
        {
            string basePath = UnityEngine.Application.persistentDataPath;
            string root = Path.Combine(basePath, "QA", "runs");
            return new DevelopmentQaEvidenceRecorder(root);
        }

        /// <summary>현재 활성 run의 절대 디렉터리 경로. 활성 run이 없으면 <c>null</c>.</summary>
        public string RunDirectoryPath
        {
            get
            {
                lock (sync)
                {
                    return activeRunDirectoryPath;
                }
            }
        }

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            lock (sync)
            {
                if (isActive)
                {
                    return QaEvidenceOperationResult.AlreadyActive(
                        "An evidence run is already active. Call Finalize before beginning a new one.");
                }

                if (!TryValidateRunId(runId, out string error))
                {
                    return QaEvidenceOperationResult.Invalid(error);
                }

                DateTime now = utcNowProvider();
                string timestamp = now.ToString(RunIdDirectoryTimestampFormat, CultureInfo.InvariantCulture);
                string directoryName = timestamp + "-run-" + runId;
                string candidatePath = Path.Combine(runsRootDirectory, directoryName);

                if (Directory.Exists(candidatePath))
                {
                    return QaEvidenceOperationResult.Invalid(
                        "A run directory already exists at '" + directoryName +
                        "'. Choose a different runId or retry in the next second.");
                }

                try
                {
                    Directory.CreateDirectory(candidatePath);
                    Directory.CreateDirectory(Path.Combine(candidatePath, ScreenshotsDirectoryName));
                    Directory.CreateDirectory(Path.Combine(candidatePath, PatchesDirectoryName));
                    // Stub artifacts so consumers (DeveloperQa, orchestrator) can rely on the
                    // design layout immediately — Finalize overwrites report.md / manifest.json.
                    File.WriteAllText(
                        Path.Combine(candidatePath, ConsoleFileName), string.Empty, Encoding.UTF8);
                    File.WriteAllText(
                        Path.Combine(candidatePath, ReportFileName), ProvisionalReportMarkdown, Encoding.UTF8);
                    WriteProvisionalManifest(candidatePath, runId, now);
                }
                catch (Exception ex)
                {
                    return QaEvidenceOperationResult.InternalError(SanitizeExceptionMessage(ex));
                }

                activeRunId = runId;
                activeRunDirectoryPath = candidatePath;
                startedAtUtc = now;
                sequenceNumber = 0;
                recordedEvents.Clear();
                isActive = true;
                isFinalized = false;

                QaEvidenceOperationResult appendResult = AppendEventInternal(
                    QaEvidenceEvent.Create(QaEvidenceEventType.RunBegan, message: "QA run started.",
                        data: SnapshotToData(startSnapshot)));

                if (!appendResult.IsSuccess)
                {
                    return appendResult;
                }

                return QaEvidenceOperationResult.Success(
                    "QA evidence run '" + runId + "' started at '" + candidatePath + "'.");
            }
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            lock (sync)
            {
                if (evidenceEvent == null)
                {
                    return QaEvidenceOperationResult.Invalid("Event must not be null.");
                }

                QaEvidenceOperationResult guard = EnsureAppendable();
                if (guard != null)
                {
                    return guard;
                }

                return AppendEventInternal(evidenceEvent);
            }
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            lock (sync)
            {
                QaEvidenceOperationResult guard = EnsureAppendable();
                if (guard != null)
                {
                    return guard;
                }

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    return QaEvidenceOperationResult.Invalid("pngBytes must not be null or empty.");
                }

                string fileName = BuildScreenshotFileName(sequenceNumber + 1, commandId, fileNameHint);
                string screenshotsDirectory = Path.Combine(activeRunDirectoryPath, ScreenshotsDirectoryName);
                string absolutePath = Path.Combine(screenshotsDirectory, fileName);

                try
                {
                    Directory.CreateDirectory(screenshotsDirectory);
                    File.WriteAllBytes(absolutePath, pngBytes);
                }
                catch (Exception ex)
                {
                    return QaEvidenceOperationResult.InternalError(SanitizeExceptionMessage(ex));
                }

                var data = new Dictionary<string, string>
                {
                    ["fileName"] = fileName,
                    ["byteCount"] = pngBytes.Length.ToString(CultureInfo.InvariantCulture)
                };

                return AppendEventInternal(QaEvidenceEvent.Create(
                    QaEvidenceEventType.ScreenshotAttached,
                    commandId: commandId,
                    message: "Screenshot attached: " + fileName,
                    data: data));
            }
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            lock (sync)
            {
                QaEvidenceOperationResult guard = EnsureAppendable();
                if (guard != null)
                {
                    return guard;
                }

                string redactedText = QaEvidenceRedactor.RedactMessage(logText ?? string.Empty, redactedFieldNames);
                string consolePath = Path.Combine(activeRunDirectoryPath, ConsoleFileName);

                try
                {
                    string entry = "[" + utcNowProvider().ToString("o", CultureInfo.InvariantCulture) + "] " +
                        redactedText + Environment.NewLine;
                    File.AppendAllText(consolePath, entry, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    return QaEvidenceOperationResult.InternalError(SanitizeExceptionMessage(ex));
                }

                var data = new Dictionary<string, string>
                {
                    ["length"] = redactedText.Length.ToString(CultureInfo.InvariantCulture)
                };

                return AppendEventInternal(QaEvidenceEvent.Create(
                    QaEvidenceEventType.ConsoleRecorded,
                    message: "Console output recorded.",
                    data: data));
            }
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            lock (sync)
            {
                // Mirrors EnsureAppendable's ordering: a finalized run has isActive == false too,
                // so isFinalized must be checked first to report the precise "already closed"
                // reason instead of the less precise "never started" one.
                if (isFinalized)
                {
                    return QaEvidenceFinalizeResult.Failure(
                        QaEvidenceOperationResult.AlreadyFinalized(
                            "This run was already finalized; manifest.json and report.md are immutable."));
                }

                if (!isActive)
                {
                    return QaEvidenceFinalizeResult.Failure(
                        QaEvidenceOperationResult.NotActive("No active evidence run to finalize."));
                }

                DateTime endedAtUtc = utcNowProvider();

                AppendEventInternal(QaEvidenceEvent.Create(QaEvidenceEventType.RunEnded, message: "QA run ended.",
                    data: SnapshotToData(endSnapshot)));

                string runDirectoryName = Path.GetFileName(activeRunDirectoryPath);
                QaRunManifest manifest = QaRunManifest.Create(
                    activeRunId,
                    runDirectoryName,
                    startedAtUtc,
                    endedAtUtc,
                    recordedEvents,
                    EventsFileName,
                    ConsoleFileName,
                    ScreenshotsDirectoryName,
                    ReportFileName);

                try
                {
                    WriteManifest(manifest);
                    WriteReport(manifest);
                }
                catch (Exception ex)
                {
                    return QaEvidenceFinalizeResult.Failure(
                        QaEvidenceOperationResult.InternalError(SanitizeExceptionMessage(ex)));
                }

                string finalizedDirectoryPath = activeRunDirectoryPath;
                isFinalized = true;
                isActive = false;

                return QaEvidenceFinalizeResult.Success(manifest, finalizedDirectoryPath);
            }
        }

        // -----------------------------------------------------------------------------------
        //  Internal helpers (all called while holding `sync`)
        // -----------------------------------------------------------------------------------

        private QaEvidenceOperationResult EnsureAppendable()
        {
            // Finalize() clears `isActive` as part of closing out a run, so `isFinalized` must be
            // checked first — otherwise a finalized run would be misreported as "never started"
            // (NotActive) instead of the more precise "closed for writing" (AlreadyFinalized).
            if (isFinalized)
            {
                return QaEvidenceOperationResult.AlreadyFinalized(
                    "This run was already finalized; events are immutable.");
            }

            if (!isActive)
            {
                return QaEvidenceOperationResult.NotActive("No active evidence run. Call BeginRun first.");
            }

            return null;
        }

        private QaEvidenceOperationResult AppendEventInternal(QaEvidenceEvent evidenceEvent)
        {
            long assignedSequence = ++sequenceNumber;
            DateTime timestamp = utcNowProvider();
            QaEvidenceEvent stamped = evidenceEvent.WithSequence(assignedSequence, timestamp);
            QaEvidenceEvent redacted = QaEvidenceRedactor.Redact(stamped, redactedFieldNames);

            try
            {
                string jsonLine = JsonConvert.SerializeObject(redacted);
                string lineWithNewline = jsonLine + Environment.NewLine;
                // Dual-write: legacy QA driver uses events.jsonl; self-extending DeveloperQa
                // design (§11) names the same append-only journal journal.jsonl.
                File.AppendAllText(
                    Path.Combine(activeRunDirectoryPath, EventsFileName), lineWithNewline, Encoding.UTF8);
                File.AppendAllText(
                    Path.Combine(activeRunDirectoryPath, JournalFileName), lineWithNewline, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // Roll back the sequence number so a failed append never leaves a gap that looks
                // like a silently dropped event.
                sequenceNumber--;
                return QaEvidenceOperationResult.InternalError(SanitizeExceptionMessage(ex));
            }

            recordedEvents.Add(redacted);
            return QaEvidenceOperationResult.Success();
        }

        private void WriteManifest(QaRunManifest manifest)
        {
            string manifestPath = Path.Combine(activeRunDirectoryPath, ManifestFileName);
            string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(manifestPath, json, Encoding.UTF8);
        }

        private static void WriteProvisionalManifest(string runDirectoryPath, string runId, DateTime startedAtUtc)
        {
            var provisional = new Dictionary<string, object>
            {
                ["RunId"] = runId ?? string.Empty,
                ["RunDirectoryName"] = Path.GetFileName(runDirectoryPath) ?? string.Empty,
                ["StartedAtUtc"] = startedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                ["Status"] = "InProgress",
                ["EventsFileName"] = EventsFileName,
                ["JournalFileName"] = JournalFileName,
                ["ConsoleFileName"] = ConsoleFileName,
                ["ScreenshotsDirectoryName"] = ScreenshotsDirectoryName,
                ["PatchesDirectoryName"] = PatchesDirectoryName,
                ["ReportFileName"] = ReportFileName
            };
            string json = JsonConvert.SerializeObject(provisional, Formatting.Indented);
            File.WriteAllText(
                Path.Combine(runDirectoryPath, ManifestFileName), json, Encoding.UTF8);
        }

        private void WriteReport(QaRunManifest manifest)
        {
            string reportPath = Path.Combine(activeRunDirectoryPath, ReportFileName);
            File.WriteAllText(reportPath, BuildReportMarkdown(manifest), Encoding.UTF8);
        }

        private static string BuildReportMarkdown(QaRunManifest manifest)
        {
            var sb = new StringBuilder();
            sb.Append("# QA Run Report").Append(Environment.NewLine).Append(Environment.NewLine);
            sb.Append("- Run ID: `").Append(manifest.RunId).Append('`').Append(Environment.NewLine);
            sb.Append("- Run directory: `").Append(manifest.RunDirectoryName).Append('`').Append(Environment.NewLine);
            sb.Append("- Started (UTC): ").Append(manifest.StartedAtUtc.ToString("o", CultureInfo.InvariantCulture))
                .Append(Environment.NewLine);
            sb.Append("- Ended (UTC): ").Append(manifest.EndedAtUtc.ToString("o", CultureInfo.InvariantCulture))
                .Append(Environment.NewLine);
            sb.Append("- Verdict: **").Append(manifest.Verdict.ToString().ToUpperInvariant()).Append("**")
                .Append(Environment.NewLine);
            sb.Append("- Reason: ").Append(manifest.VerdictReason).Append(Environment.NewLine).Append(Environment.NewLine);

            sb.Append("## Evidence Summary").Append(Environment.NewLine).Append(Environment.NewLine);
            sb.Append("| Metric | Count |").Append(Environment.NewLine);
            sb.Append("|---|---|").Append(Environment.NewLine);
            sb.Append("| Total events | ").Append(manifest.TotalEvents).Append(" |").Append(Environment.NewLine);
            sb.Append("| Assertions passed | ").Append(manifest.AssertionPassedCount).Append(" |").Append(Environment.NewLine);
            sb.Append("| Assertions failed | ").Append(manifest.AssertionFailedCount).Append(" |").Append(Environment.NewLine);
            sb.Append("| Screenshots attached | ").Append(manifest.ScreenshotCount).Append(" |").Append(Environment.NewLine);
            sb.Append("| Console log recorded | ").Append(manifest.ConsoleRecorded ? "yes" : "no").Append(" |")
                .Append(Environment.NewLine).Append(Environment.NewLine);

            sb.Append("## Artifacts").Append(Environment.NewLine).Append(Environment.NewLine);
            sb.Append("- Events: `").Append(manifest.EventsFileName)
                .Append("` (append-only; not tracked in git, see run directory)").Append(Environment.NewLine);
            sb.Append("- Console log: `").Append(manifest.ConsoleFileName).Append("` (not tracked in git)")
                .Append(Environment.NewLine);
            sb.Append("- Screenshots: `").Append(manifest.ScreenshotsDirectoryName).Append("/` (not tracked in git)")
                .Append(Environment.NewLine);

            return sb.ToString();
        }

        private static IReadOnlyDictionary<string, string> SnapshotToData(QaDriverSnapshot snapshot)
        {
            return snapshot?.Values;
        }

        private static string BuildScreenshotFileName(long sequenceHint, string commandId, string fileNameHint)
        {
            string baseName = !string.IsNullOrWhiteSpace(fileNameHint)
                ? Path.GetFileNameWithoutExtension(SanitizeFileNameComponent(fileNameHint))
                : SanitizeFileNameComponent(commandId);

            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "screenshot";
            }

            return sequenceHint.ToString("D4", CultureInfo.InvariantCulture) + "-" + baseName + ".png";
        }

        private static string SanitizeFileNameComponent(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // Strip any directory component a caller-supplied hint might smuggle in, then replace
            // remaining invalid filename characters so a hostile hint can never escape the
            // screenshots directory (defense in depth on top of the runId path-safety check).
            string nameOnly = Path.GetFileName(raw);
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(nameOnly.Length);
            foreach (char c in nameOnly)
            {
                sb.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
            }

            return sb.ToString();
        }

        /// <summary>
        /// run id 경로 안전성 검증. 빈 값, 공백 포함, 계층 구분자(<c>/</c>, <c>\</c>), 상위
        /// 디렉터리 이동(<c>..</c>), 또는 플랫폼이 파일 이름에 금지하는 문자를 모두 거부합니다.
        /// 실패해도 예외를 던지지 않고 사람이 읽을 수 있는 사유만 반환합니다(Fail-Safe).
        /// </summary>
        private static bool TryValidateRunId(string runId, out string error)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                error = "runId must not be blank.";
                return false;
            }

            if (runId.Trim().Length != runId.Length)
            {
                error = "runId must not contain leading or trailing whitespace: '" + runId + "'.";
                return false;
            }

            if (runId.Contains(".."))
            {
                error = "runId must not contain '..': '" + runId + "'.";
                return false;
            }

            foreach (char c in runId)
            {
                if (c == '/' || c == '\\')
                {
                    error = "runId must not contain hierarchy separators ('/' or '\\'): '" + runId + "'.";
                    return false;
                }

                if (char.IsWhiteSpace(c))
                {
                    error = "runId must not contain whitespace: '" + runId + "'.";
                    return false;
                }

                if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
                {
                    error = "runId contains a character that is not allowed in a file/directory name: '" + runId + "'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static string SanitizeExceptionMessage(Exception exception)
        {
            return "Internal QA evidence recorder error (" + exception.GetType().Name + "). See server logs for details.";
        }
    }
}
#endif
