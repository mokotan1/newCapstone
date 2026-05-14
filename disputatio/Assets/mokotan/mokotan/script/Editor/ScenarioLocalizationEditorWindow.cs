using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ScenarioLocalizationEditorWindow : EditorWindow
{
    private const string MenuPath = "Tools/Scenario/Localization Editor";
    private const string ScenarioFolder = "Assets/Resources/Scenario";
    private const string PrefsScenarioGuid = "ScenarioLocalizationEditor.ScenarioGuid";
    private const string PrefsDialogueGuid = "ScenarioLocalizationEditor.DialogueGuid";
    private const string PrefsSpeakersGuid = "ScenarioLocalizationEditor.SpeakersGuid";
    private const string PrefsLanguage = "ScenarioLocalizationEditor.Language";

    private TextAsset _scenarioAsset;
    private TextAsset _dialogueCsvAsset;
    private TextAsset _speakerCsvAsset;
    private ScenarioScript _script;
    private ScenarioLocalizationCsvDocument _dialogueDocument;
    private ScenarioLocalizationCsvDocument _speakerDocument;
    private ScenarioLocalizationEditorRow[] _rows = Array.Empty<ScenarioLocalizationEditorRow>();
    private string[] _scenarioAssetGuids = Array.Empty<string>();
    private string[] _scenarioAssetLabels = Array.Empty<string>();
    private string[] _blockIds = Array.Empty<string>();
    private string[] _languageOptions = new[] { "en" };
    private string _languageCode = "en";
    private string _newLanguageCode = "";
    private int _selectedScenarioIndex;
    private int _selectedBlockIndex;
    private int _selectedLanguageIndex;
    private Vector2 _scroll;
    private bool _showOnlyMissing;
    private bool _dirty;
    private string _status = "";
    private MessageType _statusType = MessageType.Info;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        ScenarioLocalizationEditorWindow window =
            GetWindow<ScenarioLocalizationEditorWindow>("Scenario Localization");
        window.minSize = new Vector2(880, 560);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshScenarioAssetList();
        LoadPrefs();
        ReloadAll();
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawAssetSelectors();
        DrawBlockAndLanguageSelectors();
        DrawActions();
        DrawStatus();
        DrawRows();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Scenario Localization Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "시나리오 JSON에서 블록을 고른 뒤 원문(ko)을 보면서 선택 언어 번역을 직접 입력합니다. 저장은 dialogue CSV에만 반영됩니다.",
            MessageType.None);
    }

    private void DrawAssetSelectors()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("자료 선택", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int nextScenarioIndex = EditorGUILayout.Popup("Scenario Script", _selectedScenarioIndex, _scenarioAssetLabels);
        if (EditorGUI.EndChangeCheck() && nextScenarioIndex >= 0 && nextScenarioIndex < _scenarioAssetGuids.Length)
        {
            _selectedScenarioIndex = nextScenarioIndex;
            _scenarioAsset = LoadTextAssetByGuid(_scenarioAssetGuids[_selectedScenarioIndex]);
            SaveGuidPref(PrefsScenarioGuid, _scenarioAsset);
            ReloadScenario();
        }

        EditorGUI.BeginChangeCheck();
        _dialogueCsvAsset = (TextAsset)EditorGUILayout.ObjectField(
            "Dialogue CSV",
            _dialogueCsvAsset,
            typeof(TextAsset),
            false);
        _speakerCsvAsset = (TextAsset)EditorGUILayout.ObjectField(
            "Speaker CSV",
            _speakerCsvAsset,
            typeof(TextAsset),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            SaveGuidPref(PrefsDialogueGuid, _dialogueCsvAsset);
            SaveGuidPref(PrefsSpeakersGuid, _speakerCsvAsset);
            ReloadCsvDocuments();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBlockAndLanguageSelectors()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("작업 범위", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(_blockIds.Length == 0);
        EditorGUI.BeginChangeCheck();
        _selectedBlockIndex = EditorGUILayout.Popup("Block", _selectedBlockIndex, _blockIds);
        if (EditorGUI.EndChangeCheck())
            RebuildRows();
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginChangeCheck();
        _selectedLanguageIndex = EditorGUILayout.Popup("Language", _selectedLanguageIndex, _languageOptions);
        if (EditorGUI.EndChangeCheck())
        {
            _selectedLanguageIndex = Mathf.Clamp(_selectedLanguageIndex, 0, _languageOptions.Length - 1);
            _languageCode = _languageOptions[_selectedLanguageIndex];
            EditorPrefs.SetString(PrefsLanguage, _languageCode);
            RebuildRows();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        _newLanguageCode = EditorGUILayout.TextField("Add Language", _newLanguageCode);
        if (GUILayout.Button("언어 추가", GUILayout.Width(100)))
            AddLanguage();
        _showOnlyMissing = EditorGUILayout.ToggleLeft("미번역만 보기", _showOnlyMissing, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawActions()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("자료 다시 읽기", GUILayout.Height(28)))
            ReloadAll();

        EditorGUI.BeginDisabledGroup(!_dirty || _dialogueCsvAsset == null);
        if (GUILayout.Button("번역 CSV 저장", GUILayout.Height(28)))
            SaveDialogueCsv();
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("선택 블록 미번역 개수 확인", GUILayout.Height(28)))
            ReportMissingCount();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, _statusType);
    }

    private void DrawRows()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("선택 블록 커맨드", EditorStyles.boldLabel);
        if (_rows.Length == 0)
        {
            EditorGUILayout.HelpBox("선택한 블록에 talk_standing 커맨드가 없거나 자료를 읽지 못했습니다.", MessageType.Warning);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (ScenarioLocalizationEditorRow row in _rows)
        {
            if (_showOnlyMissing && row.isTranslated)
                continue;

            DrawRow(row);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawRow(ScenarioLocalizationEditorRow row)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = row.isTranslated ? new Color(0.88f, 1f, 0.88f) : new Color(1f, 0.9f, 0.82f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = previous;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"#{row.commandIndex:000}  {row.speakerName}  [{row.side}]",
            EditorStyles.boldLabel,
            GUILayout.MinWidth(220));
        EditorGUILayout.LabelField(row.lineId, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(row.isTranslated ? "번역 완료" : "미번역", GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("원문 (ko)");
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextArea(row.sourceText, GUILayout.MinHeight(42));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField($"번역 ({_languageCode})");
        EditorGUI.BeginChangeCheck();
        string next = EditorGUILayout.TextArea(row.translation, GUILayout.MinHeight(54));
        if (EditorGUI.EndChangeCheck())
        {
            _dialogueDocument.SetValue(row.lineId, _languageCode, next);
            _dirty = true;
            RebuildRowsPreserveScroll();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    private void RefreshScenarioAssetList()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { ScenarioFolder });
        var filteredGuids = new List<string>();
        var labels = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            filteredGuids.Add(guid);
            labels.Add(Path.GetFileNameWithoutExtension(path));
        }

        _scenarioAssetGuids = filteredGuids.ToArray();
        _scenarioAssetLabels = labels.Count == 0 ? new[] { "(시나리오 JSON 없음)" } : labels.ToArray();
    }

    private void LoadPrefs()
    {
        _scenarioAsset = LoadTextAssetByGuid(EditorPrefs.GetString(PrefsScenarioGuid, ""));
        _dialogueCsvAsset = LoadTextAssetByGuid(EditorPrefs.GetString(PrefsDialogueGuid, ""));
        _speakerCsvAsset = LoadTextAssetByGuid(EditorPrefs.GetString(PrefsSpeakersGuid, ""));
        _languageCode = EditorPrefs.GetString(PrefsLanguage, "en");

        if (_scenarioAsset == null && _scenarioAssetGuids.Length > 0)
            _scenarioAsset = LoadTextAssetByGuid(_scenarioAssetGuids[0]);
        if (_dialogueCsvAsset == null)
            _dialogueCsvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                Path.Combine(ScenarioFolder, "the_unholy_dialogue.csv").Replace('\\', '/'));
        if (_speakerCsvAsset == null)
            _speakerCsvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                Path.Combine(ScenarioFolder, "the_unholy_speakers.csv").Replace('\\', '/'));

        _selectedScenarioIndex = IndexOfScenario(_scenarioAsset);
    }

    private void ReloadAll()
    {
        ReloadScenario();
        ReloadCsvDocuments();
        SetStatus("자료를 다시 읽었습니다.", MessageType.Info);
    }

    private void ReloadScenario()
    {
        _script = ScenarioScript.FromJson(_scenarioAsset != null ? _scenarioAsset.text : "");
        RebuildBlockIds();
        RebuildRows();
    }

    private void ReloadCsvDocuments()
    {
        _dialogueDocument = ScenarioLocalizationCsvDocument.FromCsv(
            _dialogueCsvAsset != null ? _dialogueCsvAsset.text : "",
            "line_id");
        _speakerDocument = ScenarioLocalizationCsvDocument.FromCsv(
            _speakerCsvAsset != null ? _speakerCsvAsset.text : "",
            "speaker_id");
        RebuildLanguageOptions();
        RebuildRows();
        _dirty = false;
    }

    private void RebuildBlockIds()
    {
        if (_script?.blocks == null || _script.blocks.Length == 0)
        {
            _blockIds = Array.Empty<string>();
            _selectedBlockIndex = 0;
            return;
        }

        var ids = new List<string>();
        foreach (ScenarioBlock block in _script.blocks)
        {
            if (block != null && !string.IsNullOrWhiteSpace(block.block_id))
                ids.Add(block.block_id);
        }

        _blockIds = ids.ToArray();
        _selectedBlockIndex = Mathf.Clamp(_selectedBlockIndex, 0, Mathf.Max(0, _blockIds.Length - 1));
    }

    private void RebuildLanguageOptions()
    {
        string[] languages = _dialogueDocument?.GetLanguageColumns() ?? Array.Empty<string>();
        var options = new List<string>();
        foreach (string language in languages)
        {
            if (!string.Equals(language, "ko", StringComparison.OrdinalIgnoreCase))
                options.Add(language);
        }

        if (options.Count == 0)
            options.Add("en");

        _languageOptions = options.ToArray();
        _selectedLanguageIndex = Mathf.Max(0, Array.IndexOf(_languageOptions, _languageCode));
        _languageCode = _languageOptions[_selectedLanguageIndex];
    }

    private void RebuildRows()
    {
        string blockId = GetSelectedBlockId();
        _rows = ScenarioLocalizationEditorModel.BuildRows(
            _script,
            blockId,
            _dialogueDocument,
            _speakerDocument,
            _languageCode);
    }

    private void RebuildRowsPreserveScroll()
    {
        Vector2 scroll = _scroll;
        RebuildRows();
        _scroll = scroll;
    }

    private void AddLanguage()
    {
        string language = _newLanguageCode.Trim();
        if (string.IsNullOrEmpty(language))
        {
            SetStatus("추가할 언어 코드를 입력하세요. 예: en, ja, zh", MessageType.Warning);
            return;
        }

        _dialogueDocument.EnsureColumn(language);
        _languageCode = language;
        _newLanguageCode = "";
        RebuildLanguageOptions();
        RebuildRows();
        _dirty = true;
        SetStatus($"{language} 언어 열을 추가했습니다. 저장을 눌러 CSV에 반영하세요.", MessageType.Info);
    }

    private void SaveDialogueCsv()
    {
        string path = AssetDatabase.GetAssetPath(_dialogueCsvAsset);
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("Dialogue CSV 파일 경로를 찾지 못했습니다.", MessageType.Error);
            return;
        }

        File.WriteAllText(path, _dialogueDocument.ToCsv());
        AssetDatabase.ImportAsset(path);
        _dirty = false;
        SetStatus("번역 CSV를 저장했습니다: " + path, MessageType.Info);
    }

    private void ReportMissingCount()
    {
        int missing = 0;
        foreach (ScenarioLocalizationEditorRow row in _rows)
        {
            if (!row.isTranslated)
                missing++;
        }

        SetStatus($"선택 블록 미번역: {missing} / {_rows.Length}", missing == 0 ? MessageType.Info : MessageType.Warning);
    }

    private string GetSelectedBlockId()
    {
        return _blockIds.Length == 0
            ? string.Empty
            : _blockIds[Mathf.Clamp(_selectedBlockIndex, 0, _blockIds.Length - 1)];
    }

    private int IndexOfScenario(TextAsset asset)
    {
        if (asset == null)
            return 0;

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        for (int i = 0; i < _scenarioAssetGuids.Length; i++)
        {
            if (_scenarioAssetGuids[i] == guid)
                return i;
        }

        return 0;
    }

    private static TextAsset LoadTextAssetByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    }

    private static void SaveGuidPref(string key, TextAsset asset)
    {
        string path = asset == null ? "" : AssetDatabase.GetAssetPath(asset);
        EditorPrefs.SetString(key, string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path));
    }

    private void SetStatus(string message, MessageType type)
    {
        _status = message;
        _statusType = type;
    }
}
