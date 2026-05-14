using System;
using System.Collections.Generic;
using System.Text;

public sealed class ScenarioLocalizationCsvDocument
{
    private readonly string _idColumn;
    private readonly List<string> _headers;
    private readonly List<Dictionary<string, string>> _rows;

    private ScenarioLocalizationCsvDocument(
        string idColumn,
        List<string> headers,
        List<Dictionary<string, string>> rows)
    {
        _idColumn = idColumn;
        _headers = headers;
        _rows = rows;
    }

    public IReadOnlyList<string> Headers => _headers;
    public IReadOnlyList<Dictionary<string, string>> Rows => _rows;

    public void EnsureColumn(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return;

        EnsureHeader(column);
    }

    public static ScenarioLocalizationCsvDocument FromCsv(string csv, string idColumn)
    {
        List<List<string>> parsedRows = ParseCsv(csv);
        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        if (parsedRows.Count == 0)
        {
            headers.Add(idColumn);
            headers.Add("ko");
            return new ScenarioLocalizationCsvDocument(idColumn, headers, rows);
        }

        foreach (string header in parsedRows[0])
        {
            string trimmed = header.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !ContainsHeader(headers, trimmed))
                headers.Add(trimmed);
        }

        if (!ContainsHeader(headers, idColumn))
            headers.Insert(0, idColumn);
        if (!ContainsHeader(headers, "ko"))
            headers.Add("ko");

        for (int i = 1; i < parsedRows.Count; i++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int j = 0; j < headers.Count; j++)
                row[headers[j]] = j < parsedRows[i].Count ? parsedRows[i][j] : string.Empty;

            string id = GetValue(row, idColumn).Trim();
            if (!string.IsNullOrEmpty(id))
                rows.Add(row);
        }

        return new ScenarioLocalizationCsvDocument(idColumn, headers, rows);
    }

    public bool HasLanguage(string languageCode)
    {
        return ContainsHeader(_headers, languageCode);
    }

    public string[] GetLanguageColumns()
    {
        var languages = new List<string>();
        foreach (string header in _headers)
        {
            if (!string.Equals(header, _idColumn, StringComparison.OrdinalIgnoreCase))
                languages.Add(header);
        }

        return languages.ToArray();
    }

    public string GetValue(string id, string column)
    {
        Dictionary<string, string> row = FindRow(id);
        return row == null ? string.Empty : GetValue(row, column);
    }

    public void SetValue(string id, string column, string value)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(column))
            return;

        EnsureHeader(column);
        Dictionary<string, string> row = FindRow(id);
        if (row == null)
        {
            row = CreateEmptyRow();
            row[_idColumn] = id;
            _rows.Add(row);
        }

        row[column] = value ?? string.Empty;
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        AppendCsvRow(sb, _headers);
        foreach (Dictionary<string, string> row in _rows)
        {
            var cells = new List<string>();
            foreach (string header in _headers)
                cells.Add(GetValue(row, header));
            AppendCsvRow(sb, cells);
        }

        return sb.ToString();
    }

    private void EnsureHeader(string header)
    {
        if (ContainsHeader(_headers, header))
            return;

        _headers.Add(header);
        foreach (Dictionary<string, string> row in _rows)
            row[header] = string.Empty;
    }

    private Dictionary<string, string> CreateEmptyRow()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string header in _headers)
            row[header] = string.Empty;
        return row;
    }

    private Dictionary<string, string> FindRow(string id)
    {
        foreach (Dictionary<string, string> row in _rows)
        {
            if (string.Equals(GetValue(row, _idColumn).Trim(), id, StringComparison.Ordinal))
                return row;
        }

        return null;
    }

    private static bool ContainsHeader(List<string> headers, string header)
    {
        foreach (string existing in headers)
        {
            if (string.Equals(existing, header, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string GetValue(Dictionary<string, string> row, string column)
    {
        return row != null && row.TryGetValue(column, out string value) ? value : string.Empty;
    }

    private static void AppendCsvRow(StringBuilder sb, List<string> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(Escape(cells[i]));
        }

        sb.AppendLine();
    }

    private static string Escape(string value)
    {
        value ??= string.Empty;
        bool quote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!quote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(csv))
            return rows;

        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char ch = csv[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (ch == '"')
                quoted = true;
            else if (ch == ',')
                AddCell(row, cell);
            else if (ch == '\r' || ch == '\n')
            {
                AddCell(row, cell);
                if (row.Count > 1 || row[0].Length > 0)
                    rows.Add(row);
                row = new List<string>();
                if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;
            }
            else
                cell.Append(ch);
        }

        AddCell(row, cell);
        if (row.Count > 1 || row[0].Length > 0)
            rows.Add(row);
        return rows;
    }

    private static void AddCell(List<string> row, StringBuilder cell)
    {
        row.Add(cell.ToString());
        cell.Length = 0;
    }
}

public sealed class ScenarioLocalizationEditorRow
{
    public int commandIndex;
    public string lineId;
    public string speakerId;
    public string speakerName;
    public string sourceText;
    public string translation;
    public ScenarioSpeakerSide side;
    public bool isTranslated;
}

public static class ScenarioLocalizationEditorModel
{
    public static ScenarioLocalizationEditorRow[] BuildRows(
        ScenarioScript script,
        string blockId,
        ScenarioLocalizationCsvDocument dialogue,
        ScenarioLocalizationCsvDocument speakers,
        string languageCode)
    {
        if (script == null || !script.TryGetBlock(blockId, out ScenarioBlock block) || block.commands == null)
            return Array.Empty<ScenarioLocalizationEditorRow>();

        var rows = new List<ScenarioLocalizationEditorRow>();
        for (int i = 0; i < block.commands.Length; i++)
        {
            ScenarioCommand command = block.commands[i];
            if (command == null || !IsTalkStanding(command.command))
                continue;

            string translation = dialogue?.GetValue(command.line_id, languageCode) ?? string.Empty;
            rows.Add(new ScenarioLocalizationEditorRow
            {
                commandIndex = i + 1,
                lineId = command.line_id,
                speakerId = command.speaker_id,
                speakerName = ResolveSpeakerName(speakers, command.speaker_id, languageCode),
                sourceText = dialogue?.GetValue(command.line_id, "ko") ?? string.Empty,
                translation = translation,
                side = ParseSide(command.side),
                isTranslated = !string.IsNullOrWhiteSpace(translation)
            });
        }

        return rows.ToArray();
    }

    private static string ResolveSpeakerName(
        ScenarioLocalizationCsvDocument speakers,
        string speakerId,
        string languageCode)
    {
        string localized = speakers?.GetValue(speakerId, languageCode) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(localized))
            return localized;

        string korean = speakers?.GetValue(speakerId, "ko") ?? string.Empty;
        return string.IsNullOrWhiteSpace(korean) ? speakerId ?? string.Empty : korean;
    }

    private static bool IsTalkStanding(string command)
    {
        return string.IsNullOrWhiteSpace(command)
            || string.Equals(command, "talk_standing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "Talk Standing", StringComparison.OrdinalIgnoreCase);
    }

    private static ScenarioSpeakerSide ParseSide(string side)
    {
        return string.Equals(side, "right", StringComparison.OrdinalIgnoreCase)
            ? ScenarioSpeakerSide.Right
            : ScenarioSpeakerSide.Left;
    }
}
