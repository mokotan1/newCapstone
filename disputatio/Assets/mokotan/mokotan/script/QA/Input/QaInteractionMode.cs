#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Input
{
    /// <summary>
    /// QA 명령이 대상과 상호작용하는 두 가지 방식(디자인 문서 §4.5). <see cref="Api"/>는 씬
    /// 어댑터/컨트롤러 경계를 직접 호출하여 Unity 입력 파이프라인을 완전히 우회하고,
    /// <see cref="RealInput"/>은 실제 <c>UnityEngine.EventSystems.EventSystem</c>을 통해
    /// 포인터/드래그/키 이벤트를 주입하여 "실제 플레이어가 클릭할 수 있는가"까지 검증합니다.
    /// 같은 대상이 API에서는 성공하고 RealInput에서는 실패하면(가려짐/비활성 등) 이는 버그
    /// 신호이며, 이 구분이 바로 hybrid QA 드라이버의 핵심 가치입니다(Task 7 §Classify
    /// API-pass/RealInput-fail).
    /// </summary>
    public enum QaInteractionMode
    {
        /// <summary>
        /// 씬 어댑터/컨트롤러 API를 직접 호출합니다. Unity 입력 파이프라인(EventSystem,
        /// GraphicRaycaster, Selectable.interactable)을 우회하므로 빠르고 안정적이지만,
        /// "실제로 화면에서 클릭 가능한가"는 검증하지 않습니다.
        /// </summary>
        Api,

        /// <summary>
        /// 실제 Unity 입력/EventSystem 경로(레이캐스트, 포인터 이벤트, 선택)를 통해 상호작용을
        /// 주입합니다. 가려진 대상이나 비활성 대상은 API에서는 성공하더라도 여기서는
        /// <c>InputLayerFailure</c> 진단과 함께 실패합니다.
        /// </summary>
        RealInput
    }
}
#endif
