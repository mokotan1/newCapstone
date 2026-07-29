#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Godlotto.QA.Scenes
{
    /// <summary>
    /// <see cref="IQaSceneAdapter.CaptureSnapshot"/>가 반환하는 씬 상태의 불변 스냅샷.
    /// 이 태스크(5) 시점에는 어댑터별 상세 상태 수집기가 아직 없으므로(Task 8의
    /// <c>QaStateProbe</c>에서 확장), 씬 이름·캡처 시각과 어댑터가 자유롭게 채울 수 있는
    /// 얕은 key-value 진단 정보만 담습니다. 비밀값이나 자유 텍스트(챗봇 응답 등)는 절대
    /// 담지 않아야 합니다(디자인 문서 §4.7).
    /// </summary>
    public sealed class QaSceneSnapshot
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyValues =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        /// <summary>이 스냅샷이 속한 씬의 이름.</summary>
        public string SceneName { get; }

        /// <summary>스냅샷이 캡처된 UTC 시각.</summary>
        public DateTime CapturedAtUtc { get; }

        /// <summary>어댑터가 채운 얕은 진단 값. 절대 null이 아니며 기본값은 빈 딕셔너리입니다.</summary>
        public IReadOnlyDictionary<string, string> Values { get; }

        private QaSceneSnapshot(string sceneName, DateTime capturedAtUtc, IReadOnlyDictionary<string, string> values)
        {
            SceneName = sceneName ?? string.Empty;
            CapturedAtUtc = capturedAtUtc;
            Values = values ?? EmptyValues;
        }

        public static QaSceneSnapshot Create(
            string sceneName,
            DateTime capturedAtUtc,
            IReadOnlyDictionary<string, string> values = null)
        {
            return new QaSceneSnapshot(sceneName, capturedAtUtc, values);
        }
    }
}
#endif
