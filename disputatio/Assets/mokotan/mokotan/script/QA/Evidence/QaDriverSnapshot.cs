#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// Task 8 임시 대체(stub) 타입. 이후 태스크에서 <c>QaDriverCore</c>가 노출할 권위 있는
    /// 진단 스냅샷(<c>QaDriverSnapshot</c>)이 아직 존재하지 않으므로, Evidence 레이어가
    /// run 시작/종료 시점에 첨부할 수 있는 최소한의 key-value 스냅샷만 이 타입에 담습니다.
    /// Task 8이 Core(또는 다른 네임스페이스)에 완전한 버전을 도입하면, 이 타입은 그 결과를
    /// 얕은 <see cref="Values"/> 딕셔너리로 변환하는 어댑터로 축소되거나 제거될 예정입니다.
    /// 비밀값·자유 텍스트(챗봇 응답 등)는 절대 담지 않아야 합니다.
    /// </summary>
    public sealed class QaDriverSnapshot
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyValues =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        /// <summary>이 스냅샷이 속한 QA run의 식별자(문자열 표현).</summary>
        public string RunId { get; }

        /// <summary>스냅샷이 캡처된 UTC 시각.</summary>
        public DateTime CapturedAtUtc { get; }

        /// <summary>호출자가 채운 얕은 진단 값. 절대 null이 아니며 기본값은 빈 딕셔너리입니다.</summary>
        public IReadOnlyDictionary<string, string> Values { get; }

        private QaDriverSnapshot(string runId, DateTime capturedAtUtc, IReadOnlyDictionary<string, string> values)
        {
            RunId = runId ?? string.Empty;
            CapturedAtUtc = capturedAtUtc;
            Values = values ?? EmptyValues;
        }

        public static QaDriverSnapshot Create(
            string runId,
            DateTime capturedAtUtc,
            IReadOnlyDictionary<string, string> values = null)
        {
            return new QaDriverSnapshot(runId, capturedAtUtc, values);
        }
    }
}
#endif
