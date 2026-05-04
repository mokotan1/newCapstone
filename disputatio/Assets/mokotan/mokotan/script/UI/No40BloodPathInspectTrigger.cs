using UnityEngine;

/// <summary>
/// 전기 실 조작 후 복도에서 피의 길(조명이 켜진 바닥 등)을 클릭했을 때 No40 독백 1회.
/// ElectricOn이 아니면 콜라이더가 꺼집니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class No40BloodPathInspectTrigger : MonoBehaviour
{
    private BoxCollider2D _col;

    private void Awake()
    {
        _col = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        bool electric = FlowchartLocator.GetFungusGlobalBoolean(FungusVariableKeys.ElectricOn);
        if (_col != null && _col.enabled != electric)
            _col.enabled = electric;
    }

    private void OnMouseDown()
    {
        if (!enabled || InteractionLock.IsLocked)
            return;

        if (!FlowchartLocator.GetFungusGlobalBoolean(FungusVariableKeys.ElectricOn))
            return;

        No40ConditionalDialogueRunner.EnsureExists();
        No40ConditionalDialogueRunner.Instance.TryPlayBloodPathLine();
    }
}
