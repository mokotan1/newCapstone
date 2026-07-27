using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>거울 퍼즐의 읽기 전용 판정 스냅샷(개발자 모드 디버그 표시용).</summary>
    public readonly struct MirrorPlacementDebug
    {
        public readonly bool PositionPass;
        public readonly bool AnglePass;
        public readonly bool ReflectionPass;
        public readonly float Intensity01;
        public readonly bool AngleRequired;
        public readonly bool ReflectionRequired;

        public MirrorPlacementDebug(
            bool positionPass,
            bool anglePass,
            bool reflectionPass,
            float intensity01,
            bool angleRequired,
            bool reflectionRequired)
        {
            PositionPass = positionPass;
            AnglePass = anglePass;
            ReflectionPass = reflectionPass;
            Intensity01 = intensity01;
            AngleRequired = angleRequired;
            ReflectionRequired = reflectionRequired;
        }

        public bool IsFullSolution => PositionPass && AnglePass && ReflectionPass;
    }

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

        [Header("Solution — Position")]
        [SerializeField] RectTransform solutionMarker;
        [SerializeField] Vector2 targetAnchoredPosition = new Vector2(10f, -10f);
        [SerializeField] float positionTolerance = 46f;
        [SerializeField] float successDelaySeconds = 0.15f;

        [Header("Solution — Angle")]
        [SerializeField] bool requireMirrorAngle = true;
        [SerializeField] float targetVisualAngleDegrees = 35f;
        [SerializeField] float angleToleranceDegrees = 10f;

        [Header("Solution — Reflection")]
        [SerializeField] bool requireReflection = true;
        [Tooltip("BookOverlay 로컬 좌표의 광원 위치.")]
        [SerializeField] Vector2 lightSourceAnchoredPosition = new Vector2(-210f, -150f);
        [Tooltip("지정하면 이 표식의 anchoredPosition을 반사 목표로 쓴다. 비우면 아래 폴백 좌표를 쓴다.")]
        [SerializeField] RectTransform reflectionTargetMarker;
        [SerializeField] Vector2 reflectionTargetAnchoredPosition = new Vector2(190f, 120f);
        [SerializeField] Vector2 mirrorBaseNormal = new Vector2(1f, 0f);
        [SerializeField] float reflectionToleranceDegrees = 12f;

        [Header("Brightness Falloff (밝기 곡선)")]
        [SerializeField] float positionFalloff = 240f;
        [SerializeField] float angleFalloff = 70f;
        [SerializeField] float reflectionFalloff = 70f;

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
        FilterCardRotator mirrorRotator;

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

            mirrorRotator = rotator;

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

        /// <summary>
        /// QA observability seam: snap the active mirror card to the inspector-configured
        /// solution pose and re-evaluate through the normal
        /// <see cref="EvaluateCurrentPlacement"/> → <see cref="TriggerSuccess"/> →
        /// <see cref="StudyRoomMirrorPuzzleSuccessRouter"/> path.
        /// Does not alter player input handlers, does not call ForceSolve, and no-ops when
        /// no mirror card is active.
        /// </summary>
        public bool TrySnapToConfiguredSolutionAndEvaluateForQa()
        {
            if (mirrorCardRect == null)
                return false;

            Vector2 targetPosition = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAnchoredPosition(
                mirrorCardRect,
                solutionMarker,
                targetAnchoredPosition);
            float targetAngle = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAngle(
                solutionMarker,
                targetVisualAngleDegrees);

            mirrorCardRect.anchoredPosition = targetPosition;

            if (mirrorRotator == null)
                mirrorRotator = mirrorCardRect.GetComponent<FilterCardRotator>();

            if (mirrorRotator != null)
            {
                float delta = Mathf.DeltaAngle(mirrorRotator.CurrentVisualAngleDegrees, targetAngle);
                if (!Mathf.Approximately(delta, 0f))
                    mirrorRotator.RotateVisualBy(delta);
            }
            else
            {
                mirrorCardRect.localEulerAngles = new Vector3(0f, 0f, targetAngle);
            }

            // Immediate evaluation only for this QA entry; restore delay so player path is unchanged.
            float previousDelay = successDelaySeconds;
            successDelaySeconds = 0f;
            try
            {
                if (!puzzleActive)
                    puzzleActive = true;

                EvaluateCurrentPlacement();
            }
            finally
            {
                successDelaySeconds = previousDelay;
            }

            return true;
        }

        /// <summary>
        /// 개발자 모드 QA용 읽기 전용 판정 스냅샷. 성공을 트리거하지 않고
        /// 현재 위치·각도·반사 통과 여부와 밝기 강도만 계산한다.
        /// </summary>
        public bool TryGetPlacementDebug(out MirrorPlacementDebug debug)
        {
            if (mirrorCardRect == null)
            {
                debug = default;
                return false;
            }

            Vector2 currentPosition = mirrorCardRect.anchoredPosition;
            float currentAngle = ResolveCurrentVisualAngle();

            Vector2 targetPosition = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAnchoredPosition(
                mirrorCardRect,
                solutionMarker,
                targetAnchoredPosition);
            float targetAngle = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAngle(
                solutionMarker,
                targetVisualAngleDegrees);

            MirrorReflectionInput reflectionInput = BuildReflectionInput(currentPosition, currentAngle);

            bool positionPass = StudyRoomMirrorPuzzleEvaluator.IsPositionSolution(
                currentPosition, targetPosition, positionTolerance);
            bool anglePass = !requireMirrorAngle
                || Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) <= angleToleranceDegrees;
            bool reflectionPass = !requireReflection
                || StudyRoomMirrorPuzzleEvaluator.IsReflectionSolution(reflectionInput, reflectionToleranceDegrees);
            float intensity = StudyRoomMirrorPuzzleEvaluator.SolveIntensity01(
                currentPosition, targetPosition, positionFalloff,
                currentAngle, targetAngle, angleFalloff,
                reflectionInput, reflectionFalloff);

            debug = new MirrorPlacementDebug(
                positionPass, anglePass, reflectionPass, intensity, requireMirrorAngle, requireReflection);
            return true;
        }

        void PrepareCodeView()
        {
            if (codeView == null)
                return;

            codeView.Configure(bookOverlayRect, mirrorCardRect);
            codeView.EnsureHalfCodeClue();
            codeView.EnsureMirrorOverlay();
            codeView.SetMirrorOverlayActive(true);
            codeView.ShowScattered();
        }

        void SubscribeToMirrorInput()
        {
            UnsubscribeFromMirrorInput();

            if (mirrorCardRect == null)
                return;

            FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
            if (drag != null)
                drag.DragEnded += OnMirrorMoved;

            if (mirrorRotator == null)
                mirrorRotator = mirrorCardRect.GetComponent<FilterCardRotator>();

            if (mirrorRotator != null)
                mirrorRotator.Rotated += OnMirrorMoved;
        }

        void UnsubscribeFromMirrorInput()
        {
            if (mirrorCardRect != null)
            {
                FilterCardBoundedDrag drag = mirrorCardRect.GetComponent<FilterCardBoundedDrag>();
                if (drag != null)
                    drag.DragEnded -= OnMirrorMoved;
            }

            if (mirrorRotator != null)
                mirrorRotator.Rotated -= OnMirrorMoved;
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

            Vector2 currentPosition = mirrorCardRect.anchoredPosition;
            float currentAngle = ResolveCurrentVisualAngle();

            Vector2 targetPosition = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAnchoredPosition(
                mirrorCardRect,
                solutionMarker,
                targetAnchoredPosition);
            float targetAngle = StudyRoomMirrorPuzzleEvaluator.ResolveTargetAngle(
                solutionMarker,
                targetVisualAngleDegrees);

            MirrorReflectionInput reflectionInput = BuildReflectionInput(currentPosition, currentAngle);

            UpdateBrightness(currentPosition, targetPosition, currentAngle, targetAngle, reflectionInput);

            if (!IsSolutionReached(currentPosition, currentAngle, targetPosition, targetAngle, reflectionInput))
                return;

            if (successDelaySeconds <= 0f)
            {
                TriggerSuccess();
                return;
            }

            if (pendingSuccessRoutine == null)
                pendingSuccessRoutine = StartCoroutine(TriggerSuccessAfterDelay());
        }

        bool IsSolutionReached(
            Vector2 currentPosition,
            float currentAngle,
            Vector2 targetPosition,
            float targetAngle,
            MirrorReflectionInput reflectionInput)
        {
            if (!StudyRoomMirrorPuzzleEvaluator.IsPositionSolution(currentPosition, targetPosition, positionTolerance))
                return false;

            if (requireMirrorAngle
                && !StudyRoomMirrorPuzzleEvaluator.IsSolution(
                    currentPosition,
                    currentAngle,
                    targetPosition,
                    targetAngle,
                    positionTolerance,
                    angleToleranceDegrees))
                return false;

            if (requireReflection
                && !StudyRoomMirrorPuzzleEvaluator.IsReflectionSolution(reflectionInput, reflectionToleranceDegrees))
                return false;

            return true;
        }

        void UpdateBrightness(
            Vector2 currentPosition,
            Vector2 targetPosition,
            float currentAngle,
            float targetAngle,
            MirrorReflectionInput reflectionInput)
        {
            if (codeView == null)
                return;

            float intensity = StudyRoomMirrorPuzzleEvaluator.SolveIntensity01(
                currentPosition,
                targetPosition,
                positionFalloff,
                currentAngle,
                targetAngle,
                angleFalloff,
                reflectionInput,
                reflectionFalloff);

            codeView.SetReflectionIntensity(intensity);
            codeView.SetReflectionPath(
                lightSourceAnchoredPosition,
                currentPosition,
                StudyRoomMirrorPuzzleEvaluator.ComputeReflectedDirection(reflectionInput),
                reflectionInput.targetMarker,
                IsSolutionReached(currentPosition, currentAngle, targetPosition, targetAngle, reflectionInput));
        }

        float ResolveCurrentVisualAngle()
        {
            if (mirrorRotator != null)
                return mirrorRotator.CurrentVisualAngleDegrees;

            if (mirrorCardRect != null)
                return StudyRoomMirrorPuzzleEvaluator.NormalizeVisualAngle(mirrorCardRect.localEulerAngles.z);

            return 0f;
        }

        MirrorReflectionInput BuildReflectionInput(Vector2 currentPosition, float currentAngle)
        {
            return new MirrorReflectionInput
            {
                mirrorPosition = currentPosition,
                mirrorAngleDegrees = currentAngle,
                lightSource = lightSourceAnchoredPosition,
                targetMarker = ResolveReflectionTarget(),
                mirrorBaseNormal = mirrorBaseNormal
            };
        }

        Vector2 ResolveReflectionTarget()
        {
            if (reflectionTargetMarker != null
                && mirrorCardRect != null
                && reflectionTargetMarker.parent == mirrorCardRect.parent)
                return reflectionTargetMarker.anchoredPosition;

            return reflectionTargetAnchoredPosition;
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
                codeView.ShowSolved();

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
