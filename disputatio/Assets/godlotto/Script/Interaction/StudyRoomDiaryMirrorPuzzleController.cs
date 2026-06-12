using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom CardStackPanel — BookmarkMirror 거울로 7337 숫자 단서를 완성하면 DiarySolved / UnlockSuccess 흐름을 탄다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StudyRoomDiaryMirrorPuzzleController : MonoBehaviour
    {
        [Header("Mirror Card")]
        [SerializeField] RectTransform mirrorCardRect;

        [Header("Code View")]
        [SerializeField] StudyRoomDiaryMirrorCodeView codeView;
        [SerializeField] RectTransform bookOverlayRect;

        [Header("Solution")]
        [SerializeField] RectTransform solutionMarker;
        [SerializeField] Vector2 targetAnchoredPosition = new Vector2(168f, 0f);
        [SerializeField] float positionTolerance = 45f;
        [SerializeField] float successDelaySeconds = 0.15f;

        [Header("Success Routing")]
        [SerializeField] StudyRoomPuzzleController roomController;
        [SerializeField] Flowchart flowchart;
        [SerializeField] string successInteractionId = "unlock";
        [SerializeField] string successFungusBlockName = "UnlockSuccess";
        [SerializeField] string solvedBoolVariableName = "DiarySolved";
        [SerializeField] bool setSolvedBoolBeforeSuccess = true;
        [SerializeField] bool preferInteractionController = true;

        bool puzzleActive;
        bool successTriggered;
        Coroutine pendingSuccessRoutine;

        void Awake()
        {
            if (roomController == null)
                roomController = FindFirstObjectByType<StudyRoomPuzzleController>();

            if (flowchart == null && roomController != null)
                flowchart = roomController.GetComponent<Flowchart>();

            if (codeView == null)
                codeView = GetComponent<StudyRoomDiaryMirrorCodeView>();
        }

        void OnDisable()
        {
            UnsubscribeFromMirrorInput();
            CancelPendingSuccess();
        }

        /// <summary>FilterCardBookDropZone에서 거울 카드 활성화 직후 호출.</summary>
        public void NotifyMirrorCardActivated(RectTransform activatedMirrorCard, FilterCardRotator rotator)
        {
            if (activatedMirrorCard != null)
                mirrorCardRect = activatedMirrorCard;

            if (bookOverlayRect == null && mirrorCardRect != null)
                bookOverlayRect = mirrorCardRect.parent as RectTransform;

            puzzleActive = mirrorCardRect != null;
            successTriggered = false;
            CancelPendingSuccess();

            PrepareCodeView();
            SubscribeToMirrorInput();
            EvaluateCurrentPlacement();
        }

        /// <summary>패널이 열렸을 때 BookmarkMirror 드롭 전에도 반쪽 7337 단서를 표시한다.</summary>
        public void ShowHalfCodeClue(RectTransform targetBookOverlay)
        {
            if (targetBookOverlay != null)
                bookOverlayRect = targetBookOverlay;

            if (codeView == null)
                codeView = GetComponent<StudyRoomDiaryMirrorCodeView>();

            if (codeView == null || bookOverlayRect == null)
                return;

            codeView.Configure(bookOverlayRect, mirrorCardRect);
            codeView.EnsureHalfCodeClue();
            codeView.SetMirrorOverlayActive(false);
        }

        public void EvaluateCurrentPlacementForTests()
        {
            EvaluateCurrentPlacement();
        }

        void PrepareCodeView()
        {
            if (codeView == null)
                return;

            codeView.Configure(bookOverlayRect, mirrorCardRect);
            codeView.EnsureHalfCodeClue();
            codeView.EnsureMirrorOverlay();
            codeView.SetMirrorOverlayActive(true);
        }

        void SubscribeToMirrorInput()
        {
            UnsubscribeFromMirrorInput();

            if (mirrorCardRect == null)
                return;

            FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
            if (drag != null)
                drag.DragEnded += OnMirrorMoved;
        }

        void UnsubscribeFromMirrorInput()
        {
            if (mirrorCardRect == null)
                return;

            FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
            if (drag != null)
                drag.DragEnded -= OnMirrorMoved;
        }

        void OnMirrorMoved()
        {
            EvaluateCurrentPlacement();
        }

        void EvaluateCurrentPlacement()
        {
            if (!puzzleActive || successTriggered || mirrorCardRect == null)
                return;

            if (IsAlreadyClaimed())
            {
                successTriggered = true;
                if (codeView != null)
                    codeView.HidePuzzleUi();
                return;
            }

            Vector2 targetPosition = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAnchoredPosition(
                mirrorCardRect,
                solutionMarker,
                targetAnchoredPosition);

            bool isSolution = StudyRoomMirrorPuzzleEvaluator.IsPositionSolution(
                mirrorCardRect.anchoredPosition,
                targetPosition,
                positionTolerance);

            if (!isSolution)
                return;

            if (successDelaySeconds <= 0f)
            {
                TriggerSuccess();
                return;
            }

            if (pendingSuccessRoutine == null)
                pendingSuccessRoutine = StartCoroutine(TriggerSuccessAfterDelay());
        }

        System.Collections.IEnumerator TriggerSuccessAfterDelay()
        {
            yield return new WaitForSeconds(successDelaySeconds);
            pendingSuccessRoutine = null;
            TriggerSuccess();
        }

        void CancelPendingSuccess()
        {
            if (pendingSuccessRoutine == null)
                return;

            StopCoroutine(pendingSuccessRoutine);
            pendingSuccessRoutine = null;
        }

        bool IsAlreadyClaimed()
        {
            Flowchart fc = FlowchartLocator.Resolve(flowchart);
            if (fc == null)
                return false;

            return fc.GetBooleanVariable("HaveTutorKey");
        }

        void TriggerSuccess()
        {
            successTriggered = true;
            puzzleActive = false;
            UnsubscribeFromMirrorInput();
            CancelPendingSuccess();

            if (codeView != null)
                codeView.HidePuzzleUi();

            StudyRoomMirrorPuzzleSuccessRouter.ApplySuccess(
                roomController,
                flowchart,
                successInteractionId,
                successFungusBlockName,
                solvedBoolVariableName,
                setSolvedBoolBeforeSuccess,
                preferInteractionController);
        }
    }
}
