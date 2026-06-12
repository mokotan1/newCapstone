using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom 거울 카드 퍼즐 — 위치·각도 정답 판정 (EditMode 테스트 가능한 순수 로직).
    /// </summary>
    public static class StudyRoomMirrorPuzzleEvaluator
    {
        public static bool IsSolution(
            Vector2 currentAnchoredPosition,
            float currentVisualAngleDegrees,
            Vector2 targetAnchoredPosition,
            float targetVisualAngleDegrees,
            float positionTolerance,
            float angleToleranceDegrees)
        {
            if (!IsPositionSolution(currentAnchoredPosition, targetAnchoredPosition, positionTolerance))
                return false;

            if (angleToleranceDegrees < 0f)
                angleToleranceDegrees = 0f;

            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(currentVisualAngleDegrees, targetVisualAngleDegrees));
            return deltaAngle <= angleToleranceDegrees;
        }

        /// <summary>거울 숫자 퍼즐 — 위치만으로 정답 여부를 판정한다.</summary>
        public static bool IsPositionSolution(
            Vector2 currentAnchoredPosition,
            Vector2 targetAnchoredPosition,
            float positionTolerance)
        {
            if (positionTolerance < 0f)
                positionTolerance = 0f;

            float distance = Vector2.Distance(currentAnchoredPosition, targetAnchoredPosition);
            return distance <= positionTolerance;
        }

        public static float NormalizeVisualAngle(float zEulerDegrees)
        {
            float normalized = zEulerDegrees % 360f;
            if (normalized < 0f)
                normalized += 360f;

            return normalized;
        }

        public static float ResolveTargetAngle(RectTransform solutionMarker, float fallbackAngleDegrees)
        {
            if (solutionMarker == null)
                return NormalizeVisualAngle(fallbackAngleDegrees);

            return NormalizeVisualAngle(solutionMarker.localEulerAngles.z);
        }

        public static Vector2 ResolveTargetAnchoredPosition(
            RectTransform mirrorCard,
            RectTransform solutionMarker,
            Vector2 fallbackAnchoredPosition)
        {
            if (mirrorCard == null || solutionMarker == null)
                return fallbackAnchoredPosition;

            RectTransform sharedParent = mirrorCard.parent as RectTransform;
            if (sharedParent == null || solutionMarker.parent != sharedParent)
                return fallbackAnchoredPosition;

            return solutionMarker.anchoredPosition;
        }
    }
}
