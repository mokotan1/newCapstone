using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 서재 7337 거울 퍼즐의 개별 숫자 조각(7, 3, 3, 7) 한 칸의 직렬화 데이터.
    /// 초기(흩어진) 배치/회전과 해결(정렬) 배치/회전을 함께 들고 있어,
    /// 씬 레퍼런스가 비어 있어도 코드/테스트에서 생성·검증할 수 있다.
    /// </summary>
    [System.Serializable]
    public class StudyRoomDiaryDigitPiece
    {
        [Tooltip("표시할 숫자 글리프(\"7\" / \"3\").")]
        public string glyph = "7";

        [Header("초기(흩어진) 상태")]
        [Tooltip("성공 전 흩어진 위치(부모 RectTransform 로컬, anchoredPosition).")]
        public Vector2 scatterPosition;

        [Tooltip("성공 전 기울어진 회전(z, 보통 -15~+15도).")]
        public float scatterRotation;

        [Header("해결(정렬) 상태")]
        [Tooltip("성공 시 7337로 읽히도록 정렬되는 위치.")]
        public Vector2 solvedPosition;

        [Tooltip("성공 시 회전(z, 보통 0도).")]
        public float solvedRotation;

        public StudyRoomDiaryDigitPiece()
        {
        }

        public StudyRoomDiaryDigitPiece(
            string glyph,
            Vector2 scatterPosition,
            float scatterRotation,
            Vector2 solvedPosition,
            float solvedRotation)
        {
            this.glyph = glyph;
            this.scatterPosition = scatterPosition;
            this.scatterRotation = scatterRotation;
            this.solvedPosition = solvedPosition;
            this.solvedRotation = solvedRotation;
        }

        /// <summary>진행도 t(0=흩어짐, 1=정렬)에 따라 보간된 위치를 돌려준다.</summary>
        public Vector2 ResolvePosition(float progress01)
        {
            return Vector2.LerpUnclamped(scatterPosition, solvedPosition, Mathf.Clamp01(progress01));
        }

        /// <summary>진행도 t(0=흩어짐, 1=정렬)에 따라 보간된 z 회전을 돌려준다.</summary>
        public float ResolveRotation(float progress01)
        {
            return Mathf.LerpAngle(scatterRotation, solvedRotation, Mathf.Clamp01(progress01));
        }
    }
}
