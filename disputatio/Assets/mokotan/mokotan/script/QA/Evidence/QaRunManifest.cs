#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// <see cref="QaRunManifest.AggregateVerdict"/>가 반환할 수 있는 명시적 verdict.
    /// 순서(<see cref="NotRun"/> &lt; <see cref="Blocked"/> &lt; <see cref="Fail"/> &lt;
    /// <see cref="Pass"/>)는 임의이며 비교 연산에 사용하지 않습니다 — 항상 이름으로만 취급합니다.
    /// </summary>
    public enum QaRunVerdictCode
    {
        /// <summary>어떤 어서션도 스크린샷도 기록되지 않았습니다. "예외 없음"을 성공으로 추론하지 않습니다.</summary>
        NotRun,

        /// <summary>일부 증거는 있지만(어서션 또는 스크린샷 중 하나만) PASS를 선언하기에 불충분합니다.</summary>
        Blocked,

        /// <summary>명시적으로 실패한 어서션이 하나 이상 있습니다.</summary>
        Fail,

        /// <summary>통과한 어서션과 첨부된 스크린샷이 모두 있고, 실패한 어서션이 없습니다.</summary>
        Pass
    }

    /// <summary>
    /// 하나의 QA run을 마무리할 때 <c>manifest.json</c>/<c>report.md</c>로 기록되는 불변 요약.
    /// <see cref="Verdict"/>는 항상 <see cref="AggregateVerdict"/>가 이벤트 로그로부터 산출한
    /// 값이어야 하며, 호출자가 임의로 <c>Pass</c>를 지정할 수 없습니다(증거 기반 verdict 원칙).
    /// </summary>
    public sealed class QaRunManifest
    {
        public string RunId { get; }

        public string RunDirectoryName { get; }

        public DateTime StartedAtUtc { get; }

        public DateTime EndedAtUtc { get; }

        /// <summary>
        /// JSON에서 <c>"Pass"</c>/<c>"Fail"</c> 등 사람이 읽을 수 있는 이름으로 직렬화됩니다
        /// (기본 정수 직렬화는 리뷰어가 manifest.json을 눈으로 검사할 때 오해를 유발하므로 사용하지 않음).
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public QaRunVerdictCode Verdict { get; }

        public string VerdictReason { get; }

        public long TotalEvents { get; }

        public int AssertionPassedCount { get; }

        public int AssertionFailedCount { get; }

        public int ScreenshotCount { get; }

        public bool ConsoleRecorded { get; }

        public string EventsFileName { get; }

        public string ConsoleFileName { get; }

        public string ScreenshotsDirectoryName { get; }

        public string ReportFileName { get; }

        private QaRunManifest(
            string runId,
            string runDirectoryName,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            QaRunVerdictCode verdict,
            string verdictReason,
            long totalEvents,
            int assertionPassedCount,
            int assertionFailedCount,
            int screenshotCount,
            bool consoleRecorded,
            string eventsFileName,
            string consoleFileName,
            string screenshotsDirectoryName,
            string reportFileName)
        {
            RunId = runId ?? string.Empty;
            RunDirectoryName = runDirectoryName ?? string.Empty;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            Verdict = verdict;
            VerdictReason = verdictReason ?? string.Empty;
            TotalEvents = totalEvents;
            AssertionPassedCount = assertionPassedCount;
            AssertionFailedCount = assertionFailedCount;
            ScreenshotCount = screenshotCount;
            ConsoleRecorded = consoleRecorded;
            EventsFileName = eventsFileName ?? string.Empty;
            ConsoleFileName = consoleFileName ?? string.Empty;
            ScreenshotsDirectoryName = screenshotsDirectoryName ?? string.Empty;
            ReportFileName = reportFileName ?? string.Empty;
        }

        public static QaRunManifest Create(
            string runId,
            string runDirectoryName,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            IReadOnlyList<QaEvidenceEvent> events,
            string eventsFileName,
            string consoleFileName,
            string screenshotsDirectoryName,
            string reportFileName)
        {
            IReadOnlyList<QaEvidenceEvent> safeEvents = events ?? Array.Empty<QaEvidenceEvent>();

            QaRunVerdictCode verdict = AggregateVerdict(safeEvents, out string reason);

            int assertionPassed = safeEvents.Count(e => e.Type == QaEvidenceEventType.Assertion && e.Passed == true);
            int assertionFailed = safeEvents.Count(e => e.Type == QaEvidenceEventType.Assertion && e.Passed == false);
            int screenshotCount = safeEvents.Count(e => e.Type == QaEvidenceEventType.ScreenshotAttached);
            bool consoleRecorded = safeEvents.Any(e => e.Type == QaEvidenceEventType.ConsoleRecorded);

            return new QaRunManifest(
                runId,
                runDirectoryName,
                startedAtUtc,
                endedAtUtc,
                verdict,
                reason,
                safeEvents.Count,
                assertionPassed,
                assertionFailed,
                screenshotCount,
                consoleRecorded,
                eventsFileName,
                consoleFileName,
                screenshotsDirectoryName,
                reportFileName);
        }

        /// <summary>
        /// 이벤트 로그만으로 verdict를 산출하는 순수 함수(파일 I/O 없음 — 단위 테스트 용이).
        /// 규칙(우선순위 순):
        /// 1) 실패한 어서션이 하나라도 있으면 <see cref="QaRunVerdictCode.Fail"/>.
        /// 2) 통과한 어서션과 스크린샷이 모두 하나 이상이면(그리고 실패가 없으면)
        ///    <see cref="QaRunVerdictCode.Pass"/>.
        /// 3) 그 외 어서션/스크린샷/콘솔 중 무엇이라도 하나 이상 기록되었으면
        ///    <see cref="QaRunVerdictCode.Blocked"/>(증거가 불완전함).
        /// 4) 그 외(증거가 전혀 없음, 생애주기 이벤트만 있음)에는 <see cref="QaRunVerdictCode.NotRun"/>.
        /// "예외가 발생하지 않았다"는 사실만으로는 절대 <see cref="QaRunVerdictCode.Pass"/>를
        /// 추론하지 않습니다.
        /// </summary>
        public static QaRunVerdictCode AggregateVerdict(IReadOnlyList<QaEvidenceEvent> events, out string reason)
        {
            IReadOnlyList<QaEvidenceEvent> safeEvents = events ?? Array.Empty<QaEvidenceEvent>();

            int assertionPassed = 0;
            int assertionFailed = 0;
            int screenshotCount = 0;
            bool consoleRecorded = false;

            foreach (QaEvidenceEvent evidenceEvent in safeEvents)
            {
                if (evidenceEvent == null)
                {
                    continue;
                }

                switch (evidenceEvent.Type)
                {
                    case QaEvidenceEventType.Assertion:
                        if (evidenceEvent.Passed == true)
                        {
                            assertionPassed++;
                        }
                        else if (evidenceEvent.Passed == false)
                        {
                            assertionFailed++;
                        }

                        break;
                    case QaEvidenceEventType.ScreenshotAttached:
                        screenshotCount++;
                        break;
                    case QaEvidenceEventType.ConsoleRecorded:
                        consoleRecorded = true;
                        break;
                }
            }

            if (assertionFailed > 0)
            {
                reason = "One or more assertions explicitly failed (" + assertionFailed + " failed, " +
                    assertionPassed + " passed).";
                return QaRunVerdictCode.Fail;
            }

            if (assertionPassed > 0 && screenshotCount > 0)
            {
                reason = "Evidence-backed success: " + assertionPassed + " assertion(s) passed and " +
                    screenshotCount + " screenshot(s) attached.";
                return QaRunVerdictCode.Pass;
            }

            if (assertionPassed > 0 || screenshotCount > 0 || consoleRecorded)
            {
                reason = "Partial evidence recorded (assertionsPassed=" + assertionPassed +
                    ", screenshots=" + screenshotCount + ", consoleRecorded=" + consoleRecorded +
                    "); a passing assertion AND at least one screenshot are both required for PASS.";
                return QaRunVerdictCode.Blocked;
            }

            reason = "No evidence-backed assertions or screenshots were recorded for this run.";
            return QaRunVerdictCode.NotRun;
        }
    }
}
#endif
