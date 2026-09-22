using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 지하 단순 방: Fungus Start 블록(FadeScreen) 대체. 씬 로드 후 1회 페이드 인.
    /// </summary>
    public sealed class BasementRoomEnterFade : MonoBehaviour
    {
        [SerializeField] float durationSeconds = GameplayScreenFade.BasementTransitionDurationSeconds;
        [SerializeField] float targetAlpha = GameplayScreenFade.BasementDoorFadeTargetAlpha;

        void Start()
        {
            GameplayScreenFade.PlayEnterFade(targetAlpha, durationSeconds);
        }
    }
}
