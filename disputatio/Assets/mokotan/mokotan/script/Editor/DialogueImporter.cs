using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DialogueImporter : EditorWindow
{
    // 유니티 상단 메뉴에 [Di Tools] -> [Import Dialogues] 메뉴 추가
    [MenuItem("Di Tools/Import Dialogues")]
    public static void ShowWindow()
    {
        GetWindow<DialogueImporter>("Dialogue Importer");
    }

    // JSON 파일을 넣을 변수
    private TextAsset jsonFile;

    void OnGUI()
    {
        GUILayout.Label("대사 데이터 임포터", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // JSON 파일 입력 필드
        jsonFile = (TextAsset)EditorGUILayout.ObjectField("JSON 파일 (.json)", jsonFile, typeof(TextAsset), false);

        GUILayout.Space(20);

        if (GUILayout.Button("대사 데이터 생성하기 (Click!)", GUILayout.Height(40)))
        {
            if (jsonFile != null)
            {
                ImportDialogue();
            }
            else
            {
                Debug.LogError("🚨 JSON 파일을 먼저 할당해주세요!");
                EditorUtility.DisplayDialog("오류", "JSON 파일을 넣지 않았습니다.", "확인");
            }
        }
    }

    void ImportDialogue()
    {
        string jsonContent = jsonFile.text;
        
        // JSON 파싱
        DialogueContainer loadedData = JsonUtility.FromJson<DialogueContainer>(jsonContent);

        if (loadedData == null || loadedData.dialogues == null)
        {
            Debug.LogError("🚨 JSON 파싱 실패! 형식이 올바른지 확인해주세요.");
            return;
        }

        // 결과물(ScriptableObject)을 저장할 경로
        string savePath = "Assets/Resources/GameDialogue.asset";
        
        // Resources 폴더가 없으면 생성
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 기존 파일 불러오기 또는 새로 생성
        DialogueData asset = AssetDatabase.LoadAssetAtPath<DialogueData>(savePath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(asset, savePath);
        }

        // 데이터 덮어쓰기
        asset.dialogues = loadedData.dialogues;
        
        // 저장 및 새로고침
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ 성공! {asset.dialogues.Count}개의 대사가 {savePath}에 저장되었습니다.");
        
        // 저장된 파일 하이라이트
        Selection.activeObject = asset;
        EditorUtility.DisplayDialog("성공", $"총 {asset.dialogues.Count}개의 대사를 불러왔습니다!\n위치: {savePath}", "확인");
    }

    // JSON 배열을 받기 위한 래퍼 클래스
    [System.Serializable]
    private class DialogueContainer
    {
        public List<DialogueEntry> dialogues;
    }
}
