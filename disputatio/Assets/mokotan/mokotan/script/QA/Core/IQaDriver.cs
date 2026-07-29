#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// Unity CLI 게이트웨이와 사람이 조작하는 개발자 패널이 QA 실행을 구동하기 위해 의존하는
    /// 최소 계약. 구현체는 명령을 직렬화하여 실행하고, 절대 예외를 밖으로 던지지 않으며
    /// 항상 명시적 <see cref="QaCommandResult"/>를 반환합니다.
    /// </summary>
    public interface IQaDriver
    {
        Task<QaCommandResult> ExecuteAsync(QaCommand command, CancellationToken cancellationToken);
    }
}
#endif
