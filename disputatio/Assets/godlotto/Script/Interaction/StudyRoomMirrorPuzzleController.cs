using Fungus;
using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom CardStackPanel 거울 퍼즐 — FilterCard 위치·회전 정답 시 기존 UnlockSuccess 흐름을 재사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StudyRoomMirrorPuzzleController : MonoBehaviour
    {
        [Header("Mirror Card")]
        [SerializeField] RectTransform mirrorCardRect;
        [SerializeField] FilterCardRotator mirrorRotator;

        [Header("Word Display")]
        [SerializeField] Image sourceWordImage;
        [SerializeField] StudyRoomMirrorReflectionView reflectionView;

        [Header("Solution")]
        [SerializeField] RectTransform solutionMarker;
        [SerializeField] Vector2 targetAnchoredPosition = new Vector2(168f, 0f);
        [SerializeField] float targetVisualAngleDegrees;
        [SerializeField] float positionTolerance = 45f;
        [SerializeField] float angleToleranceDegrees = 10f;

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

        void Awake()
        {
            if (roomController == null)
                roomController = FindFirstObjectByType<StudyRoomPuzzleController>();

            if (flowchart == null && roomController != null)
                flowchart = roomController.GetComponent<Flowchart>();

            if (reflectionView == null)
                reflectionView = GetComponent<StudyRoomMirrorReflectionView>();
        }

        void OnDisable()
        {
            UnsubscribeFromMirrorInput();
        }

        /// <summary>FilterCardBookDropZone에서 거울 카드 활성화 직후 호출.</summary>
        public void NotifyMirrorCardActivated(RectTransform activatedMirrorCard, FilterCardRotator rotator)
        {
            if (activatedMirrorCard != null)
                mirrorCardRect = activatedMirrorCard;

            if (rotator != null)
                mirrorRotator = rotator;

            puzzleActive = mirrorCardRect != null;
            successTriggered = false;

            PrepareReflectionView();
            SubscribeToMirrorInput();
            EvaluateCurrentPlacement();
        }

        public void EvaluateCurrentPlacementForTests()
        {
            EvaluateCurrentPlacement();
        }

        void PrepareReflectionView()
        {
            if (reflectionView == null)
                return;

            Image wordImage = sourceWordImage;
            if (wordImage == null && mirrorCardRect != null)
            {
                Transform cardStack = mirrorCardRect.parent;
                while (cardStack != null && cardStack.name != "CardStackPanel")
                    cardStack = cardStack.parent;

                if (cardStack != null)
                {
                    Transform wordCard = cardStack.Find("WordCard");
                    if (wordCard != null)
                        wordImage = wordCard.GetComponent<Image>();
                }
            }

            reflectionView.Configure(wordImage, mirrorCardRect, null, null);
            reflectionView.EnsureStudyRoomMirrorOverlay();
            reflectionView.SetMirrorOverlayActive(true);
        }

        void SubscribeToMirrorInput()
        {
            UnsubscribeFromMirrorInput();

            if (mirrorCardRect == null)
                return;

            FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
            if (drag != null)
                drag.DragEnded += OnMirrorTransformChanged;

            if (mirrorRotator != null)
                mirrorRotator.Rotated += OnMirrorTransformChanged;
        }

        void UnsubscribeFromMirrorInput()
        {
            if (mirrorCardRect != null)
            {
                FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
                if (drag != null)
                    drag.DragEnded -= OnMirrorTransformChanged;
            }

            if (mirrorRotator != null)
                mirrorRotator.Rotated -= OnMirrorTransformChanged;
        }

        void OnMirrorTransformChanged()
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
                return;
            }

            Vector2 targetPosition = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAnchoredPosition(
                mirrorCardRect,
                solutionMarker,
                targetAnchoredPosition);

            float targetAngle = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAngle(
                solutionMarker,
                targetVisualAngleDegrees);

            float currentAngle = mirrorRotator != null
                ? mirrorRotator.CurrentVisualAngleDegrees
                : StudyRoomMirrorPuzzleEvaluator.NormalizeVisualAngle(mirrorCardRect.localEulerAngles.z);

            bool isSolution = StudyRoomMirrorPuzzleEvaluator.IsSolution(
                mirrorCardRect.anchoredPosition,
                currentAngle,
                targetPosition,
                targetAngle,
                positionTolerance,
                angleToleranceDegrees);

            if (!isSolution)
                return;

            TriggerSuccess();
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

            if (reflectionView != null)
                reflectionView.SetMirrorOverlayActive(false);

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

    /// <summary>거울 퍼즐 성공 시 Fungus / RoomInteractionController 라우팅.</summary>
    public static class StudyRoomMirrorPuzzleSuccessRouter
    {
        internal static System.Action<StudyRoomPuzzleController, string> InteractionHandlerForTests;
        internal static System.Action<Flowchart, string, bool> SetBoolHandlerForTests;
        internal static System.Func<Flowchart, string, bool> ExecuteBlockHandlerForTests;

        public static void ApplySuccess(
            StudyRoomPuzzleController roomController,
            Flowchart flowchart,
            string interactionId,
            string fungusBlockName,
            string solvedBoolVariableName,
            bool setSolvedBoolBeforeSuccess,
            bool preferInteractionController)
        {
            Flowchart fc = FlowchartLocator.Resolve(flowchart);

            if (setSolvedBoolBeforeSuccess && fc != null && !string.IsNullOrWhiteSpace(solvedBoolVariableName))
                SetFlowchartBool(fc, solvedBoolVariableName, true);

            if (preferInteractionController
                && roomController != null
                && !string.IsNullOrWhiteSpace(interactionId))
            {
                if (InteractionHandlerForTests != null)
                {
                    InteractionHandlerForTests(roomController, interactionId);
                    return;
                }

                roomController.OnInteraction(interactionId);
                return;
            }

            if (fc == null || string.IsNullOrWhiteSpace(fungusBlockName))
                return;

            if (ExecuteBlockHandlerForTests != null)
            {
                ExecuteBlockHandlerForTests(fc, fungusBlockName);
                return;
            }

            FungusDialogueBridge.ExecuteBlockSafely(fc, fungusBlockName);
        }

        static void SetFlowchartBool(Flowchart flowchart, string key, bool value)
        {
            if (SetBoolHandlerForTests != null)
            {
                SetBoolHandlerForTests(flowchart, key, value);
                return;
            }

            flowchart.SetBooleanVariable(key, value);
        }

        internal static void ResetForTests()
        {
            InteractionHandlerForTests = null;
            SetBoolHandlerForTests = null;
            ExecuteBlockHandlerForTests = null;
        }
    }
}
