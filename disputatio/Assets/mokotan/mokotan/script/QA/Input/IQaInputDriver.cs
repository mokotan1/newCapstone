#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.Input
{
    /// <summary>
    /// <see cref="QaInputResult"/>가 나타낼 수 있는 명시적 결과 코드. 새 의미가 필요하면 이
    /// 열거형에 값을 추가하고, 문자열 매직 코드는 사용하지 않습니다.
    /// </summary>
    public enum QaInputResultCode
    {
        Success,

        /// <summary>호출자가 넘긴 파라미터 자체가 유효하지 않습니다(예: <see cref="QaTargetId.None"/>).</summary>
        InvalidArgument,

        /// <summary>대상 ID를 이 드라이버가 해석할 수 없습니다(등록되지 않았거나 GameObject를 찾지 못함).</summary>
        UnknownTarget,

        /// <summary>
        /// 이 상호작용 종류를 대상이 지원하지 않습니다(예: 텍스트 입력 컴포넌트가 없는 대상에
        /// <see cref="IQaInputDriver.KeyAsync"/> 호출).
        /// </summary>
        UnsupportedInteraction,

        /// <summary>
        /// RealInput 전용 실패. 대상 자체는 존재하지만 Unity 입력 레이어(레이캐스트로 가려짐,
        /// <c>Selectable.interactable == false</c>, 입력 게이트 차단 등)가 실제 상호작용을
        /// 막았습니다. 자세한 사유는 <see cref="QaInputResult.Diagnostics"/>.
        /// </summary>
        InputLayerFailure,

        /// <summary>API 모드 어댑터/컨트롤러 호출 자체가 실패를 반환했습니다.</summary>
        ApiInteractionFailed,

        Cancelled,
        InternalError
    }

    /// <summary>
    /// <see cref="QaInputResult.Diagnostics"/>가 담는, RealInput 레이어 실패를 재현·디버깅하기
    /// 위한 불변 진단 스냅샷(Task 7 §Step 3). 비밀값이나 자유 텍스트는 절대 담지 않습니다.
    /// </summary>
    public sealed class QaInputLayerDiagnostics
    {
        private static readonly IReadOnlyList<string> EmptyHits = new ReadOnlyCollection<string>(new List<string>());

        /// <summary>대상 GameObject가 씬에서 발견되었는지 여부.</summary>
        public bool TargetFound { get; }

        /// <summary>
        /// 대상이 <c>UnityEngine.UI.Selectable.interactable</c>(있는 경우) 기준으로 상호작용
        /// 가능한 상태인지 여부. Selectable이 없는 대상은 항상 <c>true</c>로 취급합니다.
        /// </summary>
        public bool TargetInteractable { get; }

        /// <summary>
        /// 대상 위치로 쏜 UI 레이캐스트 결과에 대상 자신이 "가장 위" 히트로 나타났는지 여부.
        /// <c>false</c>이면 다른 UI가 대상을 가리고 있다는 뜻입니다.
        /// </summary>
        public bool RaycastHitTarget { get; }

        /// <summary>
        /// 레이캐스트 결과 전체(위에서부터, GameObject 이름 기준). 진단 로그 용도이며 비어
        /// 있으면 아무것도 히트되지 않았다는 뜻입니다. 절대 null이 아닙니다.
        /// </summary>
        public IReadOnlyList<string> RaycastHitNames { get; }

        /// <summary>가장 위 히트의 <c>Canvas.sortingOrder</c>(없으면 <c>0</c>).</summary>
        public int TopHitSortingOrder { get; }

        /// <summary>대상 자신의 <c>Canvas.sortingOrder</c>(없으면 <c>0</c>).</summary>
        public int TargetSortingOrder { get; }

        /// <summary>
        /// 시도 시점의 게임 전역 입력 게이트 스냅샷(예: 대사 중·씬 전환 중 차단). 드라이버는
        /// 특정 게이트 구현에 직접 의존하지 않고 호출자가 주입한 콜백으로만 이 값을 얻습니다
        /// (DIP). 콜백이 없으면 항상 <c>true</c>(열림)로 취급합니다.
        /// </summary>
        public bool InputGateOpen { get; }

        /// <summary>사람이 읽을 수 있는 추가 설명.</summary>
        public string Details { get; }

        private QaInputLayerDiagnostics(
            bool targetFound,
            bool targetInteractable,
            bool raycastHitTarget,
            IReadOnlyList<string> raycastHitNames,
            int topHitSortingOrder,
            int targetSortingOrder,
            bool inputGateOpen,
            string details)
        {
            TargetFound = targetFound;
            TargetInteractable = targetInteractable;
            RaycastHitTarget = raycastHitTarget;
            RaycastHitNames = raycastHitNames ?? EmptyHits;
            TopHitSortingOrder = topHitSortingOrder;
            TargetSortingOrder = targetSortingOrder;
            InputGateOpen = inputGateOpen;
            Details = details ?? string.Empty;
        }

        public static QaInputLayerDiagnostics Create(
            bool targetFound,
            bool targetInteractable,
            bool raycastHitTarget,
            IReadOnlyList<string> raycastHitNames,
            int topHitSortingOrder,
            int targetSortingOrder,
            bool inputGateOpen,
            string details = null)
        {
            return new QaInputLayerDiagnostics(
                targetFound, targetInteractable, raycastHitTarget, raycastHitNames,
                topHitSortingOrder, targetSortingOrder, inputGateOpen, details);
        }
    }

    /// <summary>
    /// <see cref="IQaInputDriver"/> 호출 한 건의 불변 결과.
    /// </summary>
    public sealed class QaInputResult
    {
        public QaTargetId TargetId { get; }

        public QaInteractionMode Mode { get; }

        public QaInputResultCode Code { get; }

        public string Message { get; }

        /// <summary>
        /// <see cref="QaInputResultCode.InputLayerFailure"/>일 때만 채워지는 진단 정보. 그 외
        /// 코드에서는 <c>null</c>입니다.
        /// </summary>
        public QaInputLayerDiagnostics Diagnostics { get; }

        private QaInputResult(
            QaTargetId targetId,
            QaInteractionMode mode,
            QaInputResultCode code,
            string message,
            QaInputLayerDiagnostics diagnostics)
        {
            TargetId = targetId;
            Mode = mode;
            Code = code;
            Message = message ?? string.Empty;
            Diagnostics = diagnostics;
        }

        public bool IsSuccess
        {
            get { return Code == QaInputResultCode.Success; }
        }

        public static QaInputResult Success(QaTargetId targetId, QaInteractionMode mode, string message = null)
        {
            return new QaInputResult(targetId, mode, QaInputResultCode.Success, message, diagnostics: null);
        }

        public static QaInputResult Failure(
            QaTargetId targetId, QaInteractionMode mode, QaInputResultCode code, string message)
        {
            if (code == QaInputResultCode.InputLayerFailure)
            {
                throw new ArgumentException(
                    "Use " + nameof(LayerFailure) + " for " + nameof(QaInputResultCode.InputLayerFailure) + ".",
                    nameof(code));
            }

            return new QaInputResult(targetId, mode, code, message, diagnostics: null);
        }

        /// <summary>
        /// RealInput 레이어가 상호작용을 막았을 때의 결과. 항상 <paramref name="diagnostics"/>를
        /// 포함하여, "왜 실패했는지"를 재현 가능하게 남깁니다.
        /// </summary>
        public static QaInputResult LayerFailure(
            QaTargetId targetId,
            QaInteractionMode mode,
            string message,
            QaInputLayerDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            return new QaInputResult(targetId, mode, QaInputResultCode.InputLayerFailure, message, diagnostics);
        }
    }

    /// <summary>
    /// QA 명령 게이트웨이가 안정적 <see cref="QaTargetId"/>만으로 클릭/드래그/키 입력을 실행하기
    /// 위해 의존하는 최소 계약(디자인 문서 §4.5, Task 7). 구현체는 <see cref="QaInteractionMode.Api"/>
    /// (어댑터/컨트롤러 직접 호출)와 <see cref="QaInteractionMode.RealInput"/>(실제 EventSystem
    /// 경로)로 나뉘며, 절대 예외를 밖으로 던지지 않고 항상 명시적 <see cref="QaInputResult"/>를
    /// 반환합니다.
    /// </summary>
    public interface IQaInputDriver
    {
        /// <summary>이 드라이버 인스턴스가 구현하는 상호작용 방식.</summary>
        QaInteractionMode Mode { get; }

        /// <summary>대상을 한 번 클릭합니다.</summary>
        Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken);

        /// <summary>
        /// <paramref name="sourceTargetId"/>를 <paramref name="destinationTargetId"/>로 드래그합니다.
        /// </summary>
        Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId,
            QaTargetId destinationTargetId,
            CancellationToken cancellationToken);

        /// <summary>대상에 텍스트를 입력합니다(포커스/선택 후 문자 전달).</summary>
        Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken);
    }
}
#endif
