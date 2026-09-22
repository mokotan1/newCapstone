using UnityEngine;

/// <summary>
/// <see cref="SnapTarget"/>과 <see cref="DraggableSnap2D"/>가 짝을 맞출 때 쓰는 종류 키.
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
