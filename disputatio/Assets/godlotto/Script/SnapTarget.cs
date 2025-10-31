using UnityEngine;

/// <summary>
/// SnapTarget: DraggableSnap2D가 닿으면 스냅되는 투명 타겟.
/// </summary>
public enum SnapKind
{
    [InspectorName("1st Seal")]
    Seal1,

    [InspectorName("2nd Seal")]
    Seal2,

    [InspectorName("3rd Seal")]
    Seal3,

    [InspectorName("4th Seal")]
    Seal4,

    [InspectorName("5th Seal")]
    Seal5,

    [InspectorName("6th Seal")]
    Seal6,

    [InspectorName("7th Seal")]
    Seal7,

    [InspectorName("Any")]
    Any
}

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

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Gizmos.DrawCube(transform.position, GetComponent<BoxCollider2D>().size);
    }
}
