using System.Collections.Generic;
using UnityEngine;

// 대사 하나하나의 정보를 담는 클래스
[System.Serializable]
public class DialogueEntry
{
    public int id;
    public int act;
    public string scene;
    public string speaker;
    public string type; // "dialogue", "monologue", "phone" 등
    [TextArea(3, 10)]
    public string text;
}

// 대사 리스트를 관리하는 유니티 데이터 파일 (ScriptableObject)
[CreateAssetMenu(fileName = "NewDialogueData", menuName = "Game/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public List<DialogueEntry> dialogues = new List<DialogueEntry>();
}
