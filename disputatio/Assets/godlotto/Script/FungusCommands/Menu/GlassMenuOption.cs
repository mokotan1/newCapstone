using System;
using Fungus;
using UnityEngine;

/// <summary>
/// <see cref="GlassMenu"/>가 표시할 선택지 한 개. 리스트의 길이가 곧 선택지 개수입니다.
/// </summary>
[Serializable]
public class GlassMenuOption
{
    [Tooltip("버튼에 표시할 문구. Fungus 변수 치환을 지원합니다.")]
    [TextArea] public string text = "Option";

    [Tooltip("이 선택지를 고르면 실행할 블록.")]
    public Block targetBlock;

    [Tooltip("false면 표시되지만 선택할 수 없습니다(비활성/회색).")]
    public bool interactable = true;
}
