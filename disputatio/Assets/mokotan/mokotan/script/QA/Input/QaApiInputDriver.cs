#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.Input
{
    /// <summary>
    /// <see cref="QaApiInputDriver"/>가 해석한 대상 하나가 노출하는 최소 API 상호작용 계약.
    /// <see cref="IQaSceneAdapter"/>는 프리셋/스냅샷만 다루므로(Task 5), 실제 클릭/드래그/키
    /// 액션은 이 작은 인터페이스로 분리합니다(SRP·DIP: <see cref="QaApiInputDriver"/>는 구체
    /// 어댑터가 아니라 이 계약에만 의존). 구현체는 기존 도메인 컨트롤러(예:
    /// <c>RoomInteractionController</c>)를 그대로 감싸 호출하며, 실패해도 예외를 던지지 않고
    /// <c>false</c> + 사유 문자열을 반환합니다(Fail-Safe).
    /// </summary>
    public interface IQaApiInteractable
    {
        bool TryClick(QaTargetId targetId, out string error);

        bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error);

        bool TryKey(QaTargetId targetId, string text, out string error);
    }

    /// <summary>
    /// <see cref="QaInteractionMode.Api"/> 구현. Unity 입력 파이프라인(EventSystem, 레이캐스트,
    /// <c>Selectable.interactable</c>)을 완전히 우회하고, 주입된 리졸버 콜백을 통해 대상 ID를
    /// <see cref="IQaApiInteractable"/>로 해석한 뒤 그 API를 직접 호출합니다. 리졸버는 호출자가
    /// 자유롭게 구성할 수 있으므로(예: <c>QaSceneRegistry</c> 기반, 또는 테스트용 인메모리
    /// 매핑), 이 드라이버는 씬 배선 방식에 대해 아무것도 알지 못합니다(DIP).
    /// </summary>
    public sealed class QaApiInputDriver : IQaInputDriver
    {
        private readonly Func<QaTargetId, IQaApiInteractable> resolveInteractable;

        /// <param name="resolveInteractable">
        /// 대상 ID를 그 대상을 소유한 <see cref="IQaApiInteractable"/>로 해석하는 콜백. 알 수
        /// 없는 대상이면 <c>null</c>을 반환해야 합니다. 절대 예외를 던지지 않아야 합니다.
        /// </param>
        public QaApiInputDriver(Func<QaTargetId, IQaApiInteractable> resolveInteractable)
        {
            this.resolveInteractable = resolveInteractable
                ?? throw new ArgumentNullException(nameof(resolveInteractable));
        }

        public QaInteractionMode Mode
        {
            get { return QaInteractionMode.Api; }
        }

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute(targetId, cancellationToken,
                (IQaApiInteractable interactable, out string error) => interactable.TryClick(targetId, out error)));
        }

        public Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId,
            QaTargetId destinationTargetId,
            CancellationToken cancellationToken)
        {
            if (destinationTargetId.IsNone)
            {
                return Task.FromResult(QaInputResult.Failure(
                    sourceTargetId, Mode, QaInputResultCode.InvalidArgument,
                    "destinationTargetId must not be QaTargetId.None."));
            }

            return Task.FromResult(Execute(sourceTargetId, cancellationToken,
                (IQaApiInteractable interactable, out string error) =>
                    interactable.TryDrag(sourceTargetId, destinationTargetId, out error)));
        }

        public Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute(targetId, cancellationToken,
                (IQaApiInteractable interactable, out string error) => interactable.TryKey(targetId, text, out error)));
        }

        private delegate bool ApiOperation(IQaApiInteractable interactable, out string error);

        private QaInputResult Execute(
            QaTargetId targetId, CancellationToken cancellationToken, ApiOperation operation)
        {
            if (targetId.IsNone)
            {
                return QaInputResult.Failure(
                    targetId, Mode, QaInputResultCode.InvalidArgument, "targetId must not be QaTargetId.None.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.Cancelled,
                    "Command was cancelled before execution.");
            }

            IQaApiInteractable interactable;
            try
            {
                interactable = resolveInteractable(targetId);
            }
            catch (Exception ex)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.InternalError,
                    SanitizeExceptionMessage(ex));
            }

            if (interactable == null)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.UnknownTarget,
                    "Target '" + targetId + "' could not be resolved to an API-interactable owner.");
            }

            try
            {
                bool succeeded = operation(interactable, out string error);
                return succeeded
                    ? QaInputResult.Success(targetId, Mode, "API interaction succeeded.")
                    : QaInputResult.Failure(targetId, Mode, QaInputResultCode.ApiInteractionFailed,
                        error ?? "API interaction failed without a specific reason.");
            }
            catch (Exception ex)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.InternalError,
                    SanitizeExceptionMessage(ex));
            }
        }

        private static string SanitizeExceptionMessage(Exception exception)
        {
            return "Internal QA API input driver error (" + exception.GetType().Name + "). See server logs for details.";
        }
    }
}
#endif
