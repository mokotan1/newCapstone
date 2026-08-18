#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// QA 커맨드 게이트웨이가 인식하는 명령 종류.
    /// <see cref="QaDriverCore"/>는 세션(session.*) 명령만 처리하며, 나머지는 이후 태스크에서
    /// 해당 서비스(<c>IQaProfileService</c>, <c>IQaSceneRegistry</c> 등)가 연결될 때까지
    /// <see cref="QaResultCode.UnsupportedCommand"/>를 반환합니다.
    /// </summary>
    public enum QaCommandType
    {
        SessionBegin,
        SessionEnd,
        SessionAbort,
        ProfileReset,
        ProfileApplyPreset,
        SceneLoad,
        SceneWaitReady,
        InteractionApi,
        InteractionPointer,
        InteractionDrag,
        InteractionKey,
        StateRead,
        StateAssert,
        EvidenceCapture,
        ConsoleRead,
        ScenarioRun,
        ScenarioCancel,
        ScenarioStatus
    }

    /// <summary>
    /// Unity CLI 게이트웨이와 사람이 조작하는 개발자 패널이 공유하는 불변 QA 명령.
    /// 임의의 C# 소스나 리플렉션 멤버 이름을 받지 않고, 명시적 <see cref="QaCommandType"/>과
    /// 문자열 파라미터만으로 구성됩니다. 실제 검증(빈 ID, 지원하지 않는 타입 등)은
    /// <see cref="QaDriverCore"/>가 수행합니다.
    /// </summary>
    public sealed class QaCommand
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        /// <summary>호출자가 부여한 명령 상관관계 ID. 빈 값이면 <see cref="QaResultCode.InvalidCommand"/>.</summary>
        public string Id { get; }

        /// <summary>명령 종류.</summary>
        public QaCommandType Type { get; }

        /// <summary>대상 씬/타겟/프리셋 등의 안정적인 식별자(있는 경우).</summary>
        public string TargetId { get; }

        /// <summary>명령별 추가 파라미터. 절대 null이 아니며 기본값은 빈 딕셔너리입니다.</summary>
        public IReadOnlyDictionary<string, string> Parameters { get; }

        private QaCommand(
            string id,
            QaCommandType type,
            string targetId,
            IReadOnlyDictionary<string, string> parameters)
        {
            Id = id;
            Type = type;
            TargetId = targetId;
            Parameters = parameters ?? EmptyParameters;
        }

        public static QaCommand Create(
            string id,
            QaCommandType type,
            string targetId = null,
            IReadOnlyDictionary<string, string> parameters = null)
        {
            return new QaCommand(id, type, targetId, parameters);
        }

        public static QaCommand BeginSession(string id, string targetId = null)
        {
            return Create(id, QaCommandType.SessionBegin, targetId);
        }

        public static QaCommand EndSession(string id, string targetId = null)
        {
            return Create(id, QaCommandType.SessionEnd, targetId);
        }

        public static QaCommand AbortSession(string id, string targetId = null)
        {
            return Create(id, QaCommandType.SessionAbort, targetId);
        }
    }
}
#endif
