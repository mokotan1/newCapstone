using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SayDialog용 <see cref="DialogInput"/>. ClickAnywhere일 때 <see cref="DialogueLogButton"/> 위
/// 마우스 클릭은 대사 진행으로 처리하지 않아 로그 토글이 씹히는 현상을 방지한다.
/// </summary>
public class GodlottoDialogInput : DialogInput
{
    static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>(8);

    public override void SetClickAnywhereClickedFlag()
    {
        if (clickMode == ClickMode.ClickAnywhere && IsPointerOverDialogueLogButton())
            return;

        base.SetClickAnywhereClickedFlag();
    }

    internal static bool IsPointerOverDialogueLogButton()
    {
        if (EventSystem.current == null || !Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0))
            return false;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition,
        };

        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, RaycastResults);

        for (int i = 0; i < RaycastResults.Count; i++)
        {
            if (RaycastResults[i].gameObject.GetComponentInParent<DialogueLogButton>() != null)
                return true;
        }

        return false;
    }
}
