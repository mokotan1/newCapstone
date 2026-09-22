using UnityEngine;

/// <summary>
/// SnapTarget: DraggableSnap2D가 닿으면 스냅되는 투명 타겟.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SnapTarget : MonoBehaviour
{
    [Header("이 타겟이 허용하는 오브젝트 종류")]
    public SnapKind acceptKind = SnapKind.Any;

    [Tooltip("다른 오브젝트가 이미 붙어있으면 막을지 여부")]
    public bool lockWhenOccupied = true;

    [HideInInspector]
    public bool occupied = false;

    public bool CanAccept(DraggableSnap2D d)
    {
        if (lockWhenOccupied && occupied) return false;
        return acceptKind == SnapKind.Any || acceptKind == d.kind;
    }

    private void OnEnable()
    {
        int expected = LayerMask.NameToLayer("SnapTarget");
        if (expected >= 0 && gameObject.layer != expected)
        {
            Debug.LogWarning(
                $"[SnapTarget] '{name}' is on layer '{LayerMask.LayerToName(gameObject.layer)}' " +
                "(expected 'SnapTarget') — DragManager2D will skip it.", this);
        }
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent<BoxCollider2D>(out var box))
            return;
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.offset, box.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
