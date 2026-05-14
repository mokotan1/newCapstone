using System;
using System.Collections.Generic;
using System.Text;

public sealed class ScenarioLocalizationTable
{
    private readonly Dictionary<string, string> _items;

    private ScenarioLocalizationTable(Dictionary<string, string> items)
    {
        _items = items;
    }

    public static ScenarioLocalizationTable FromCsv(string csv, string languageCode, string idColumn)
    {
        var items = new Dictionary<string, string>(StringComparer.Ordinal);
        List<List<string>> rows = ParseCsv(csv);
        if (rows.Count == 0)
            return new ScenarioLocalizationTable(items);

        List<string> header = rows[0];
        int idIndex = FindColumn(header, idColumn);
        int languageIndex = FindColumn(header, languageCode);
        int fallbackIndex = FindColumn(header, "ko");

        if (idIndex < 0)
            return new ScenarioLocalizationTable(items);

        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];
            string id = GetCell(row, idIndex).Trim();
            if (string.IsNullOrEmpty(id))
                continue;

            string value = GetCell(row, languageIndex);
            if (string.IsNullOrEmpty(value))
                value = GetCell(row, fallbackIndex);

            if (!string.IsNullOrEmpty(value))
                items[id] = value;
        }

        return new ScenarioLocalizationTable(items);
    }

    public string Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        return _items.TryGetValue(id, out string value) ? value : id;
    }

    private static int FindColumn(List<string> header, string column)
    {
        if (header == null || string.IsNullOrWhiteSpace(column))
            return -1;

        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), column.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string GetCell(List<string> row, int index)
    {
        if (row == null || index < 0 || index >= row.Count)
            return string.Empty;

        return row[index];
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
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                AddCell(row, cell);
            }
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
            {
                cell.Append(ch);
            }
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
