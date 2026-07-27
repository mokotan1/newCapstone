#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Godlotto.QA.Scenes
{
    /// <summary>
    /// 씬 안의 상호작용 대상을 가리키는 불변·안정적 식별자. Hierarchy 경로나 화면 좌표는
    /// 절대 진짜 식별자가 될 수 없으므로(디자인 문서 §4.5), QA 명령/시나리오는 항상 이
    /// 값 타입을 통해서만 대상을 참조합니다. 값은 항상 소문자 dotted 문자열로 정규화되며
    /// (예: <c>kitchen.sink.faucet</c>), 공백이나 계층 구분자(<c>/</c>, <c>\</c>)를 포함한
    /// 원본 문자열은 생성 시점에 거부됩니다.
    /// </summary>
    public readonly struct QaTargetId : IEquatable<QaTargetId>
    {
        private readonly string normalizedValue;

        private QaTargetId(string normalizedValue)
        {
            this.normalizedValue = normalizedValue;
        }

        /// <summary>대상이 지정되지 않은 기본값.</summary>
        public static readonly QaTargetId None = default;

        /// <summary>정규화된(소문자) 문자열 값. <see cref="IsNone"/>이면 빈 문자열.</summary>
        public string Value
        {
            get { return normalizedValue ?? string.Empty; }
        }

        /// <summary><see cref="None"/>과 같은 기본값인지 여부.</summary>
        public bool IsNone
        {
            get { return string.IsNullOrEmpty(normalizedValue); }
        }

        /// <summary>
        /// <paramref name="rawValue"/>를 검증·정규화하여 <see cref="QaTargetId"/>를 생성합니다.
        /// 실패 시 예외를 던지지 않고 false를 반환하며, <paramref name="error"/>에 사람이 읽을 수
        /// 있는 사유를 채웁니다(Fail-Safe: 호출자가 잘못된 입력을 직접 검사하지 않아도 됨).
        /// </summary>
        public static bool TryCreate(string rawValue, out QaTargetId targetId, out string error)
        {
            if (string.IsNullOrEmpty(rawValue))
            {
                targetId = default;
                error = "Target id must not be null or empty.";
                return false;
            }

            if (rawValue.Trim().Length != rawValue.Length)
            {
                targetId = default;
                error = "Target id must not contain leading or trailing whitespace: '" + rawValue + "'.";
                return false;
            }

            for (int i = 0; i < rawValue.Length; i++)
            {
                char c = rawValue[i];
                if (char.IsWhiteSpace(c))
                {
                    targetId = default;
                    error = "Target id must not contain whitespace: '" + rawValue + "'.";
                    return false;
                }

                if (c == '/' || c == '\\')
                {
                    targetId = default;
                    error = "Target id must not contain hierarchy separators ('/' or '\\'): '" + rawValue + "'.";
                    return false;
                }
            }

            targetId = new QaTargetId(rawValue.ToLowerInvariant());
            error = null;
            return true;
        }

        /// <summary>
        /// <see cref="TryCreate"/>와 동일하게 검증하되, 실패 시 <see cref="ArgumentException"/>을
        /// 던집니다. 프로그래머가 코드에서 고정된 대상 ID를 등록할 때(예: 씬 어댑터 초기화)
        /// 잘못된 리터럴을 즉시 발견하기 위한 용도입니다.
        /// </summary>
        public static QaTargetId Create(string rawValue)
        {
            if (!TryCreate(rawValue, out QaTargetId targetId, out string error))
            {
                throw new ArgumentException(error, nameof(rawValue));
            }

            return targetId;
        }

        public bool Equals(QaTargetId other)
        {
            return string.Equals(normalizedValue, other.normalizedValue, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is QaTargetId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return normalizedValue != null ? normalizedValue.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(QaTargetId left, QaTargetId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QaTargetId left, QaTargetId right)
        {
            return !left.Equals(right);
        }
    }
}
#endif
