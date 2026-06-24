using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 거울 반사 판정에 필요한 2D 광학 입력. 광원·거울(위치/각도)·목표 표식을 직렬화 가능한 값으로 담는다.
    /// 모든 좌표는 같은 RectTransform 부모(BookOverlay) 로컬 공간 기준이어야 한다.
    /// </summary>
    [System.Serializable]
    public struct MirrorReflectionInput
    {
        public Vector2 mirrorPosition;
        public float mirrorAngleDegrees;
        public Vector2 lightSource;
        public Vector2 targetMarker;
        public Vector2 mirrorBaseNormal;
    }

    /// <summary>
    /// StudyRoom 거울 카드 퍼즐 — 위치·각도·반사 방향 정답 판정 (EditMode 테스트 가능한 순수 로직).
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

        /// <summary>θ=0 기준 거울 법선을 시각 각도만큼 회전한 단위 법선을 돌려준다.</summary>
        public static Vector2 ComputeMirrorNormal(float angleDegrees, Vector2 baseNormal)
        {
            if (baseNormal == Vector2.zero)
                baseNormal = Vector2.right;

            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 rotated = new Vector2(
                baseNormal.x * cos - baseNormal.y * sin,
                baseNormal.x * sin + baseNormal.y * cos);

            return rotated.normalized;
        }

        /// <summary>광원→거울 입사광을 거울 법선으로 반사한 단위 방향(<see cref="Vector2.Reflect"/>)을 돌려준다.</summary>
        public static Vector2 ComputeReflectedDirection(MirrorReflectionInput input)
        {
            Vector2 incoming = (input.mirrorPosition - input.lightSource);
            if (incoming == Vector2.zero)
                incoming = Vector2.right;
            incoming = incoming.normalized;

            Vector2 normal = ComputeMirrorNormal(input.mirrorAngleDegrees, input.mirrorBaseNormal);
            return Vector2.Reflect(incoming, normal).normalized;
        }

        /// <summary>반사광 방향과 "거울→목표 표식" 방향 사이의 각도 오차(도, 0~180)를 돌려준다.</summary>
        public static float ReflectionAngleErrorDegrees(MirrorReflectionInput input)
        {
            Vector2 reflected = ComputeReflectedDirection(input);

            Vector2 desired = (input.targetMarker - input.mirrorPosition);
            if (desired == Vector2.zero)
                return 0f;
            desired = desired.normalized;

            float alignment = Mathf.Clamp(Vector2.Dot(reflected, desired), -1f, 1f);
            return Mathf.Acos(alignment) * Mathf.Rad2Deg;
        }

        /// <summary>반사광이 허용 각도 안에서 목표 표식을 통과하는지 판정한다.</summary>
        public static bool IsReflectionSolution(MirrorReflectionInput input, float reflectionToleranceDegrees)
        {
            if (reflectionToleranceDegrees < 0f)
                reflectionToleranceDegrees = 0f;

            return ReflectionAngleErrorDegrees(input) <= reflectionToleranceDegrees;
        }

        /// <summary>위치·각도·반사 방향 세 조건을 모두 만족해야 정답으로 본다.</summary>
        public static bool IsFullSolution(
            Vector2 currentAnchoredPosition,
            float currentVisualAngleDegrees,
            Vector2 targetAnchoredPosition,
            float targetVisualAngleDegrees,
            float positionTolerance,
            float angleToleranceDegrees,
            MirrorReflectionInput reflectionInput,
            float reflectionToleranceDegrees)
        {
            if (!IsSolution(
                    currentAnchoredPosition,
                    currentVisualAngleDegrees,
                    targetAnchoredPosition,
                    targetVisualAngleDegrees,
                    positionTolerance,
                    angleToleranceDegrees))
                return false;

            return IsReflectionSolution(reflectionInput, reflectionToleranceDegrees);
        }

        /// <summary>
        /// 위치·각도·반사 근접도를 곱해 0~1 밝기 강도를 만든다. 정답에 가까울수록 1에 수렴한다.
        /// falloff 값은 각 축의 "완전히 어두워지는" 오차 범위(반사광 UI 밝기 곡선)다.
        /// </summary>
        public static float SolveIntensity01(
            Vector2 currentAnchoredPosition,
            Vector2 targetAnchoredPosition,
            float positionFalloff,
            float currentVisualAngleDegrees,
            float targetVisualAngleDegrees,
            float angleFalloff,
            MirrorReflectionInput reflectionInput,
            float reflectionFalloffDegrees)
        {
            float positionError = Vector2.Distance(currentAnchoredPosition, targetAnchoredPosition);
            float angleError = Mathf.Abs(Mathf.DeltaAngle(currentVisualAngleDegrees, targetVisualAngleDegrees));
            float reflectionError = ReflectionAngleErrorDegrees(reflectionInput);

            float positionFactor = Falloff01(positionError, positionFalloff);
            float angleFactor = Falloff01(angleError, angleFalloff);
            float reflectionFactor = Falloff01(reflectionError, reflectionFalloffDegrees);

            return Mathf.Clamp01(positionFactor * angleFactor * reflectionFactor);
        }

        static float Falloff01(float error, float falloff)
        {
            if (falloff <= 0f)
                return error <= 0f ? 1f : 0f;

            return Mathf.Clamp01(1f - error / falloff);
        }
    }
}
