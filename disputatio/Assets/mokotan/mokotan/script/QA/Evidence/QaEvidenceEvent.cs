#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// <see cref="QaEvidenceEvent"/>가 나타낼 수 있는 명시적 사건 종류.
    /// <see cref="Assertion"/>만 <see cref="QaEvidenceEvent.Passed"/>를 채우며, 그 외 종류는
    /// 항상 <c>null</c>입니다 — "예외가 없었다"는 사실만으로 성공을 추론하지 않기 위함입니다.
    /// </summary>
    public enum QaEvidenceEventType
    {
        RunBegan,
        CommandResult,
        Assertion,
        ScreenshotAttached,
        ConsoleRecorded,
        Note,
        RunEnded
    }

    /// <summary>
    /// 하나의 QA run의 append-only 이벤트 로그(<c>events.jsonl</c>) 한 줄을 나타내는 불변 사건.
    /// <see cref="SequenceNumber"/>와 <see cref="TimestampUtc"/>는 호출자가 채우지 않고
    /// <see cref="IQaEvidenceRecorder.AppendEvent"/> 구현체가 append 시점에 <see cref="WithSequence"/>로
    /// 부여합니다(디자인 문서: run 전체에서 단조 증가하는 순서 보장은 recorder의 책임).
    /// </summary>
    public sealed class QaEvidenceEvent
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyData =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        public long SequenceNumber { get; }

        public DateTime TimestampUtc { get; }

        /// <summary>
        /// JSON에서 <c>"Assertion"</c>/<c>"ScreenshotAttached"</c> 등 사람이 읽을 수 있는
        /// 이름으로 직렬화됩니다(evidence 로그를 사람이 직접 검토할 때 정수 값은 오해를 유발함).
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public QaEvidenceEventType Type { get; }

        /// <summary>이 사건이 관련된 QA 명령의 상관관계 ID(있는 경우). 절대 null이 아닙니다.</summary>
        public string CommandId { get; }

        /// <summary>결과/사유 코드의 문자열 표현(예: <c>QaResultCode</c> 이름). 절대 null이 아닙니다.</summary>
        public string Code { get; }

        /// <summary>
        /// <see cref="QaEvidenceEventType.Assertion"/>일 때만 값이 있는 명시적 통과/실패.
        /// 다른 종류는 항상 <c>null</c> — "예외 없음"을 "성공"으로 추론하지 않기 위한 방어 규칙.
        /// </summary>
        public bool? Passed { get; }

        public string Message { get; }

        /// <summary>사람이 읽을 수 있는 얕은 key-value 데이터. 절대 null이 아닙니다. 민감 필드는
        /// recorder가 append 시점에 <see cref="QaEvidenceRedactor"/>로 치환합니다.</summary>
        public IReadOnlyDictionary<string, string> Data { get; }

        private QaEvidenceEvent(
            long sequenceNumber,
            DateTime timestampUtc,
            QaEvidenceEventType type,
            string commandId,
            string code,
            bool? passed,
            string message,
            IReadOnlyDictionary<string, string> data)
        {
            SequenceNumber = sequenceNumber;
            TimestampUtc = timestampUtc;
            Type = type;
            CommandId = commandId ?? string.Empty;
            Code = code ?? string.Empty;
            Passed = passed;
            Message = message ?? string.Empty;
            Data = data ?? EmptyData;
        }

        /// <summary>
        /// 새 이벤트를 생성합니다. <see cref="SequenceNumber"/>/<see cref="TimestampUtc"/>는 아직
        /// 부여되지 않은 기본값(0 / <c>default</c>)이며, recorder가 <see cref="WithSequence"/>로 채웁니다.
        /// </summary>
        public static QaEvidenceEvent Create(
            QaEvidenceEventType type,
            string commandId = null,
            string code = null,
            bool? passed = null,
            string message = null,
            IReadOnlyDictionary<string, string> data = null)
        {
            return new QaEvidenceEvent(0, default, type, commandId, code, passed, message, data);
        }

        /// <summary>
        /// <see cref="QaEvidenceEventType.Assertion"/> 편의 팩토리. <paramref name="passed"/>는
        /// 반드시 호출자가 명시적으로 검증한 결과여야 하며, "예외 없음"으로 대체해서는 안 됩니다.
        /// </summary>
        public static QaEvidenceEvent ForAssertion(
            string commandId,
            bool passed,
            string message,
            IReadOnlyDictionary<string, string> data = null)
        {
            return Create(QaEvidenceEventType.Assertion, commandId, passed ? "Passed" : "Failed", passed, message, data);
        }

        /// <summary>
        /// <see cref="QaEvidenceEventType.CommandResult"/> 편의 팩토리. 명령 결과 코드만 기록하며,
        /// <see cref="Passed"/>는 절대 채우지 않습니다(명령 성공 ≠ QA 어서션 통과).
        /// </summary>
        public static QaEvidenceEvent ForCommandResult(string commandId, string resultCode, string message)
        {
            return Create(QaEvidenceEventType.CommandResult, commandId, resultCode, null, message);
        }

        /// <summary>런타임에서 이미 순서/시각이 정해진 사본을 만듭니다. 원본은 변경되지 않습니다.</summary>
        public QaEvidenceEvent WithSequence(long sequenceNumber, DateTime timestampUtc)
        {
            return new QaEvidenceEvent(sequenceNumber, timestampUtc, Type, CommandId, Code, Passed, Message, Data);
        }

        /// <summary>지정한 데이터로 치환한 사본을 만듭니다(레코더의 redaction 단계에서 사용).</summary>
        public QaEvidenceEvent WithData(IReadOnlyDictionary<string, string> data)
        {
            return new QaEvidenceEvent(SequenceNumber, TimestampUtc, Type, CommandId, Code, Passed, Message, data);
        }

        /// <summary>지정한 메시지로 치환한 사본을 만듭니다(레코더의 redaction 단계에서 사용).</summary>
        public QaEvidenceEvent WithMessage(string message)
        {
            return new QaEvidenceEvent(SequenceNumber, TimestampUtc, Type, CommandId, Code, Passed, message, Data);
        }
    }

    /// <summary>
    /// 토큰·헤더 등 민감할 수 있는 필드를 evidence에 기록하기 전에 치환하는 순수 유틸리티.
    /// 절대 예외를 던지지 않고(Fail-Safe), 일치하는 것이 없으면 원본과 동등한 값을 반환합니다.
    /// </summary>
    public static class QaEvidenceRedactor
    {
        public const string RedactedPlaceholder = "***REDACTED***";

        /// <summary>구성하지 않았을 때 사용하는 기본 민감 필드 이름(대소문자 무시, 부분 일치).</summary>
        public static readonly IReadOnlyList<string> DefaultSensitiveFieldNames = new[]
        {
            "token",
            "authorization",
            "apikey",
            "api_key",
            "password",
            "secret",
            "cookie"
        };

        private static readonly Regex InlineKeyValuePattern = new Regex(
            @"(?<key>token|authorization|api[_-]?key|password|secret|cookie)\s*[:=]\s*(?<value>[^\s,;]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// <paramref name="evidenceEvent"/>의 <see cref="QaEvidenceEvent.Data"/> 값과
        /// <see cref="QaEvidenceEvent.Message"/> 본문에서, <paramref name="sensitiveFieldNames"/>에
        /// (대소문자 무시, 부분 일치로) 매칭되는 필드/구간을 치환한 새 사본을 반환합니다. 원본은
        /// 변경되지 않습니다.
        /// </summary>
        public static QaEvidenceEvent Redact(QaEvidenceEvent evidenceEvent, IReadOnlyCollection<string> sensitiveFieldNames)
        {
            if (evidenceEvent == null)
            {
                return null;
            }

            IReadOnlyCollection<string> fieldNames = sensitiveFieldNames ?? DefaultSensitiveFieldNames;

            IReadOnlyDictionary<string, string> redactedData = RedactFields(evidenceEvent.Data, fieldNames);
            string redactedMessage = RedactMessage(evidenceEvent.Message, fieldNames);

            return evidenceEvent.WithData(redactedData).WithMessage(redactedMessage);
        }

        /// <summary>
        /// <paramref name="data"/>에서 키가 <paramref name="sensitiveFieldNames"/> 중 하나를
        /// (대소문자 무시, 부분 일치로) 포함하면 값을 <see cref="RedactedPlaceholder"/>로 치환한
        /// 새 딕셔너리를 반환합니다.
        /// </summary>
        public static IReadOnlyDictionary<string, string> RedactFields(
            IReadOnlyDictionary<string, string> data,
            IReadOnlyCollection<string> sensitiveFieldNames)
        {
            if (data == null || data.Count == 0)
            {
                return data;
            }

            IReadOnlyCollection<string> fieldNames = sensitiveFieldNames ?? DefaultSensitiveFieldNames;
            var result = new Dictionary<string, string>(data.Count);
            bool anyRedacted = false;

            foreach (KeyValuePair<string, string> entry in data)
            {
                if (IsSensitiveFieldName(entry.Key, fieldNames))
                {
                    result[entry.Key] = RedactedPlaceholder;
                    anyRedacted = true;
                }
                else
                {
                    result[entry.Key] = entry.Value;
                }
            }

            return anyRedacted ? new ReadOnlyDictionary<string, string>(result) : data;
        }

        /// <summary>
        /// 자유 텍스트 안에서 <c>key=value</c> / <c>key: value</c> 형태로 나타나는 민감 필드의
        /// 값 부분만 <see cref="RedactedPlaceholder"/>로 치환합니다. 일치하는 것이 없으면 원본을
        /// 그대로 반환합니다.
        /// </summary>
        public static string RedactMessage(string message, IReadOnlyCollection<string> sensitiveFieldNames)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message ?? string.Empty;
            }

            // sensitiveFieldNames 파라미터는 향후 호출자별 커스텀 필드 확장을 위해 계약에는
            // 남겨두되, 현재 정규식은 고정된 핵심 토큰/헤더 패턴만 다룹니다(과설계 방지).
            return InlineKeyValuePattern.Replace(message, match => match.Groups["key"].Value + "=" + RedactedPlaceholder);
        }

        private static bool IsSensitiveFieldName(string fieldName, IReadOnlyCollection<string> sensitiveFieldNames)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return false;
            }

            foreach (string candidate in sensitiveFieldNames)
            {
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (fieldName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
