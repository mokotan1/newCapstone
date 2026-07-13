using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜터 룸 문제 은행: backend_ai의 quiz_bank.csv 와 Resources/TutorQuestionOrder.txt 를 에디터에서 편집합니다.
/// 메뉴: Di Tools / Tutor Quiz Bank
/// </summary>
public class TutorQuizBankEditorWindow : EditorWindow
{
    private const string MenuPath = "Di Tools/Tutor Quiz Bank";
    private const string PrefsCsvPathKey = "TutorQuizBankEditor.CsvAbsolutePath";
    private const string HeaderLine =
        "question_id,question_ko,question_ja,question_en," +
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en," +
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en," +
        "difficulty,tags";

    private string _csvAbsolutePath = "";
    private string _orderFileRelative = "Assets/Resources/TutorQuestionOrder.txt";
    private Vector2 _scrollBank;
    private Vector2 _scrollOrder;
    private List<QuizBankRow> _rows = new List<QuizBankRow>();
    private List<string> _orderIds = new List<string>();
    private string _statusMessage = "";
    private MessageType _statusType = MessageType.Info;
    private int _selectedBankIndex = -1;
    private int _selectedOrderIndex = -1;

    [Serializable]
    private class QuizBankRow
    {
        public string question_id = "";
        public string question_ko = "";
        public string question_ja = "";
        public string question_en = "";
        public string acceptable_answers_ko = "";
        public string acceptable_answers_ja = "";
        public string acceptable_answers_en = "";
        public string reference_snippet_ko = "";
        public string reference_snippet_ja = "";
        public string reference_snippet_en = "";
        public string difficulty = "";
        public string tags = "";
    }

    [MenuItem(MenuPath)]
    public static void Open()
    {
        var w = GetWindow<TutorQuizBankEditorWindow>("Tutor Quiz Bank");
        w.minSize = new Vector2(520, 420);
        w.Show();
    }

    private void OnEnable()
    {
        _csvAbsolutePath = EditorPrefs.GetString(PrefsCsvPathKey, "");
        if (string.IsNullOrEmpty(_csvAbsolutePath))
            _csvAbsolutePath = GetDefaultQuizCsvPath();
        LoadAll();
    }

    private static string GetDefaultQuizCsvPath()
    {
        string disputatioDir = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(disputatioDir))
            return "";
        string repoRoot = Path.GetDirectoryName(disputatioDir);
        if (string.IsNullOrEmpty(repoRoot))
            return "";
        return Path.GetFullPath(Path.Combine(repoRoot, "backend_ai", "data", "tutor_quiz", "quiz_bank.csv"));
    }

    private static string GetOrderFilePath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "TutorQuestionOrder.txt"));
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("튜터 룸 문제 은행", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "CSV는 백엔드 RAG·채점에 사용됩니다. JA/EN이 비어 있으면 서버가 KO로 폴백합니다. " +
            "저장 후 필요 시 backend_ai에서 validate / 인덱스 빌드를 실행하세요.",
            MessageType.None);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("quiz_bank.csv", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _csvAbsolutePath = EditorGUILayout.TextField("절대 경로", _csvAbsolutePath);
        if (GUILayout.Button("기본 경로", GUILayout.Width(80)))
        {
            _csvAbsolutePath = GetDefaultQuizCsvPath();
            EditorPrefs.SetString(PrefsCsvPathKey, _csvAbsolutePath);
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("이 경로를 기본으로 저장", GUILayout.Width(200)))
            EditorPrefs.SetString(PrefsCsvPathKey, _csvAbsolutePath);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("CSV 다시 읽기", GUILayout.Height(24)))
            LoadCsv();
        if (GUILayout.Button("순서 파일 다시 읽기", GUILayout.Height(24)))
            LoadOrderFile();
        if (GUILayout.Button("CSV 저장", GUILayout.Height(24)))
            SaveCsv();
        if (GUILayout.Button("순서 저장", GUILayout.Height(24)))
            SaveOrderFile();
        EditorGUILayout.EndHorizontal();

        DrawStatus();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("문제 목록 (quiz_bank.csv)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("행 추가"))
        {
            _rows.Add(new QuizBankRow { question_id = SuggestNextId() });
            _selectedBankIndex = _rows.Count - 1;
        }
        EditorGUI.BeginDisabledGroup(_selectedBankIndex < 0 || _selectedBankIndex >= _rows.Count);
        if (GUILayout.Button("선택 행 삭제"))
        {
            _rows.RemoveAt(_selectedBankIndex);
            _selectedBankIndex = Mathf.Min(_selectedBankIndex, _rows.Count - 1);
        }
        if (GUILayout.Button("선택 ID를 순서 목록 끝에 추가"))
        {
            var id = _rows[_selectedBankIndex].question_id.Trim();
            if (!string.IsNullOrEmpty(id) && !_orderIds.Contains(id))
                _orderIds.Add(id);
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("순서 ← 은행 ID 전부 반영"))
            SyncOrderFromBankIds();
        EditorGUILayout.EndHorizontal();

        _scrollBank = EditorGUILayout.BeginScrollView(_scrollBank, GUILayout.MinHeight(200));
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var prev = GUI.backgroundColor;
            if (_selectedBankIndex == i)
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            if (GUILayout.Button($"#{i + 1}  {row.question_id}", EditorStyles.miniButton, GUILayout.MinWidth(140)))
                _selectedBankIndex = i;
            GUI.backgroundColor = prev;
            EditorGUILayout.EndHorizontal();

            row.question_id = EditorGUILayout.TextField("question_id", row.question_id);
            row.question_ko = EditorGUILayout.TextField("question_ko", row.question_ko);
            row.question_ja = EditorGUILayout.TextField("question_ja (선택)", row.question_ja);
            row.question_en = EditorGUILayout.TextField("question_en (선택)", row.question_en);
            row.acceptable_answers_ko = EditorGUILayout.TextField("acceptable_answers_ko (|)", row.acceptable_answers_ko);
            row.acceptable_answers_ja = EditorGUILayout.TextField("acceptable_answers_ja (선택)", row.acceptable_answers_ja);
            row.acceptable_answers_en = EditorGUILayout.TextField("acceptable_answers_en (선택)", row.acceptable_answers_en);
            row.reference_snippet_ko = EditorGUILayout.TextField("reference_snippet_ko", row.reference_snippet_ko);
            row.reference_snippet_ja = EditorGUILayout.TextField("reference_snippet_ja (선택)", row.reference_snippet_ja);
            row.reference_snippet_en = EditorGUILayout.TextField("reference_snippet_en (선택)", row.reference_snippet_en);
            row.difficulty = EditorGUILayout.TextField("difficulty (선택)", row.difficulty);
            row.tags = EditorGUILayout.TextField("tags (선택)", row.tags);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("출제 순서 (TutorQuestionOrder.txt)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(_orderFileRelative, EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(
            "순서에 ID를 넣는 방법:\n" +
            "① 위 문제 목록에서 행을 선택한 뒤 「선택 ID를 순서 목록 끝에 추가」\n" +
            "② 또는 「순서 ← 은행 ID 전부 반영」으로 은행에만 있는 ID를 끝에 한꺼번에 추가\n" +
            "③ 아래 「순서 줄 추가」로 빈 줄을 만든 뒤 quiz_bank와 같은 question_id를 직접 입력\n" +
            "마지막에 「순서 저장」을 눌러 TutorQuestionOrder.txt에 반영합니다.",
            MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("선택 순서 위로"))
            MoveOrder(-1);
        if (GUILayout.Button("선택 순서 아래로"))
            MoveOrder(1);
        if (GUILayout.Button("순서 줄 추가 (끝)"))
        {
            _orderIds.Add("");
            _selectedOrderIndex = _orderIds.Count - 1;
        }
        if (GUILayout.Button("선택 순서 제거"))
        {
            if (_selectedOrderIndex >= 0 && _selectedOrderIndex < _orderIds.Count)
            {
                _orderIds.RemoveAt(_selectedOrderIndex);
                _selectedOrderIndex = Mathf.Min(_selectedOrderIndex, _orderIds.Count - 1);
            }
        }
        EditorGUILayout.EndHorizontal();

        _scrollOrder = EditorGUILayout.BeginScrollView(_scrollOrder, GUILayout.MinHeight(120));
        for (int i = 0; i < _orderIds.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            var prevO = GUI.backgroundColor;
            if (_selectedOrderIndex == i)
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            if (GUILayout.Button($"{i + 1}.", EditorStyles.miniButton, GUILayout.Width(28)))
                _selectedOrderIndex = i;
            GUI.backgroundColor = prevO;
            _orderIds[i] = EditorGUILayout.TextField(_orderIds[i]);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawStatus()
    {
        if (string.IsNullOrEmpty(_statusMessage))
            return;
        EditorGUILayout.HelpBox(_statusMessage, _statusType);
    }

    private void SetStatus(string msg, MessageType t)
    {
        _statusMessage = msg;
        _statusType = t;
    }

    private string SuggestNextId()
    {
        int max = 0;
        foreach (var r in _rows)
        {
            var id = r.question_id.Trim();
            if (id.Length > 1 && id[0] == 'Q' && int.TryParse(id.Substring(1), out int n))
                max = Mathf.Max(max, n);
        }
        return "Q" + (max + 1).ToString("D3");
    }

    private void SyncOrderFromBankIds()
    {
        var seen = new HashSet<string>(_orderIds);
        foreach (var r in _rows)
        {
            var id = r.question_id.Trim();
            if (string.IsNullOrEmpty(id) || seen.Contains(id))
                continue;
            _orderIds.Add(id);
            seen.Add(id);
        }
        SetStatus("순서 목록에 은행에만 있는 ID를 뒤에 추가했습니다.", MessageType.Info);
    }

    private void MoveOrder(int delta)
    {
        int i = _selectedOrderIndex;
        int j = i + delta;
        if (i < 0 || i >= _orderIds.Count || j < 0 || j >= _orderIds.Count)
            return;
        (_orderIds[i], _orderIds[j]) = (_orderIds[j], _orderIds[i]);
        _selectedOrderIndex = j;
    }

    private void LoadAll()
    {
        LoadCsv();
        LoadOrderFile();
    }

    private void LoadCsv()
    {
        _rows.Clear();
        if (string.IsNullOrEmpty(_csvAbsolutePath) || !File.Exists(_csvAbsolutePath))
        {
            SetStatus("CSV 파일이 없습니다. 경로를 확인하세요.\n" + _csvAbsolutePath, MessageType.Warning);
            return;
        }

        try
        {
            string text = File.ReadAllText(_csvAbsolutePath, Encoding.UTF8);
            var table = ParseCsv(text);
            if (table.Count == 0)
            {
                SetStatus("CSV가 비어 있습니다.", MessageType.Warning);
                return;
            }

            var header = table[0];
            int idxId = IndexOfHeader(header, "question_id");
            int idxQKo = IndexOfHeader(header, "question_ko");
            int idxQJa = IndexOfHeader(header, "question_ja");
            int idxQEn = IndexOfHeader(header, "question_en");
            int idxAKo = FirstHeaderIndex(header, "acceptable_answers_ko", "acceptable_answers");
            int idxAJa = IndexOfHeader(header, "acceptable_answers_ja");
            int idxAEn = IndexOfHeader(header, "acceptable_answers_en");
            int idxRKo = FirstHeaderIndex(header, "reference_snippet_ko", "reference_snippet");
            int idxRJa = IndexOfHeader(header, "reference_snippet_ja");
            int idxREn = IndexOfHeader(header, "reference_snippet_en");
            int idxD = IndexOfHeader(header, "difficulty");
            int idxT = IndexOfHeader(header, "tags");
            if (idxId < 0 || idxQKo < 0 || idxAKo < 0 || idxRKo < 0)
            {
                SetStatus(
                    "CSV 헤더에 필수 열이 없습니다: question_id, question_ko, " +
                    "acceptable_answers_ko (또는 acceptable_answers), " +
                    "reference_snippet_ko (또는 reference_snippet)",
                    MessageType.Error);
                return;
            }

            for (int r = 1; r < table.Count; r++)
            {
                var line = table[r];
                if (line.Count == 0 || line.TrueForAll(s => string.IsNullOrWhiteSpace(s)))
                    continue;
                _rows.Add(
                    new QuizBankRow
                    {
                        question_id = GetCell(line, idxId),
                        question_ko = GetCell(line, idxQKo),
                        question_ja = GetCell(line, idxQJa),
                        question_en = GetCell(line, idxQEn),
                        acceptable_answers_ko = GetCell(line, idxAKo),
                        acceptable_answers_ja = GetCell(line, idxAJa),
                        acceptable_answers_en = GetCell(line, idxAEn),
                        reference_snippet_ko = GetCell(line, idxRKo),
                        reference_snippet_ja = GetCell(line, idxRJa),
                        reference_snippet_en = GetCell(line, idxREn),
                        difficulty = idxD >= 0 ? GetCell(line, idxD) : "",
                        tags = idxT >= 0 ? GetCell(line, idxT) : "",
                    });
            }

            SetStatus($"CSV 로드 완료: {_rows.Count}문항 ({_csvAbsolutePath})", MessageType.Info);
        }
        catch (Exception e)
        {
            SetStatus("CSV 읽기 실패: " + e.Message, MessageType.Error);
        }
    }

    private static int IndexOfHeader(List<string> header, string name)
    {
        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int FirstHeaderIndex(List<string> header, params string[] names)
    {
        foreach (string name in names)
        {
            int idx = IndexOfHeader(header, name);
            if (idx >= 0)
                return idx;
        }
        return -1;
    }

    private static string GetCell(List<string> line, int idx)
    {
        if (idx < 0 || idx >= line.Count)
            return "";
        return line[idx] ?? "";
    }

    private void LoadOrderFile()
    {
        _orderIds.Clear();
        string path = GetOrderFilePath();
        if (!File.Exists(path))
        {
            SetStatus("순서 파일 없음 (새로 저장하면 생성): " + path, MessageType.Warning);
            return;
        }

        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string t = raw.Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
                continue;
            _orderIds.Add(t);
        }
        SetStatus($"순서 파일 로드: {_orderIds.Count}개 ({path})", MessageType.Info);
    }

    private void SaveCsv()
    {
        if (string.IsNullOrEmpty(_csvAbsolutePath))
        {
            SetStatus("CSV 경로가 비어 있습니다.", MessageType.Error);
            return;
        }

        if (!ValidateBankForSave(out string err))
        {
            EditorUtility.DisplayDialog("저장 불가", err, "확인");
            SetStatus(err, MessageType.Error);
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_csvAbsolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine(HeaderLine);
            foreach (var row in _rows)
            {
                sb.AppendLine(
                    string.Join(
                        ",",
                        CsvEscape(row.question_id.Trim()),
                        CsvEscape(row.question_ko.Trim()),
                        CsvEscape(row.question_ja.Trim()),
                        CsvEscape(row.question_en.Trim()),
                        CsvEscape(row.acceptable_answers_ko.Trim()),
                        CsvEscape(row.acceptable_answers_ja.Trim()),
                        CsvEscape(row.acceptable_answers_en.Trim()),
                        CsvEscape(row.reference_snippet_ko.Trim()),
                        CsvEscape(row.reference_snippet_ja.Trim()),
                        CsvEscape(row.reference_snippet_en.Trim()),
                        CsvEscape(row.difficulty.Trim()),
                        CsvEscape(row.tags.Trim())));
            }
            File.WriteAllText(_csvAbsolutePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetStatus("CSV 저장 완료: " + _csvAbsolutePath, MessageType.Info);
            Debug.Log("[TutorQuizBank] CSV 저장: " + _csvAbsolutePath);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("저장 실패", e.Message, "확인");
            SetStatus(e.Message, MessageType.Error);
        }
    }

    private bool ValidateBankForSave(out string err)
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            string id = row.question_id.Trim();
            if (string.IsNullOrEmpty(id))
            {
                err = $"행 {i + 1}: question_id가 비어 있습니다.";
                return false;
            }
            if (!ids.Add(id))
            {
                err = $"중복 question_id: {id}";
                return false;
            }
            if (string.IsNullOrWhiteSpace(row.question_ko))
            {
                err = $"행 {id}: question_ko가 비어 있습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(row.acceptable_answers_ko))
            {
                err = $"행 {id}: acceptable_answers_ko가 비어 있습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(row.reference_snippet_ko))
            {
                err = $"행 {id}: reference_snippet_ko가 비어 있습니다.";
                return false;
            }
        }
        err = "";
        return true;
    }

    private void SaveOrderFile()
    {
        string path = GetOrderFilePath();
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("# 튜터 룸 출제 순서 (quiz_bank.csv의 question_id와 일치)");
            foreach (var id in _orderIds)
            {
                string t = id.Trim();
                if (t.Length > 0)
                    sb.AppendLine(t);
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            AssetDatabase.Refresh();
            SetStatus("순서 파일 저장: " + path, MessageType.Info);
            Debug.Log("[TutorQuizBank] 순서 저장: " + path);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("저장 실패", e.Message, "확인");
            SetStatus(e.Message, MessageType.Error);
        }
    }

    private static string CsvEscape(string s)
    {
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    /// <summary>간단 RFC4180 스타일 파서 (따옴표 필드 지원).</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;
                }
                else
                    cell.Append(c);
            }
            else
            {
                if (c == '"')
                    inQuotes = true;
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    if (row.Count > 1 || (row.Count == 1 && row[0].Length > 0) || rows.Count > 0)
                        rows.Add(row);
                    row = new List<string>();
                }
                else
                    cell.Append(c);
            }
        }
        row.Add(cell.ToString());
        if (row.Count > 1 || (row.Count == 1 && row[0].Length > 0))
            rows.Add(row);
        return rows;
    }
}
