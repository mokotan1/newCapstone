using System;
using System.Collections.Generic;
using System.Text;

public sealed class ItemTooltipContent
{
    public string itemName;
    public string itemDescription;
    public readonly List<ItemTooltipRow> rows = new List<ItemTooltipRow>();
}

public sealed class ItemTooltipTable
{
    private static readonly ItemTooltipTable Empty = new ItemTooltipTable(new Dictionary<int, ItemTooltipContent>());

    private readonly Dictionary<int, ItemTooltipContent> byItemId;

    private ItemTooltipTable(Dictionary<int, ItemTooltipContent> byItemId)
    {
        this.byItemId = byItemId;
    }

    public static ItemTooltipTable FromCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Empty;

        List<List<string>> rows = ParseCsv(csv);
        if (rows.Count == 0)
            return Empty;

        List<string> header = rows[0];
        int itemIdIndex = FindColumn(header, "item_id");
        int displayNameIndex = FindColumn(header, "display_name_ko");
        int keyIndex = FindColumn(header, "tooltip_key");
        int valueIndex = FindColumn(header, "tooltip_value_ko");

        if (itemIdIndex < 0 || keyIndex < 0 || valueIndex < 0)
            return Empty;

        var map = new Dictionary<int, ItemTooltipContent>();
        for (int i = 1; i < rows.Count; i++)
        {
            List<string> row = rows[i];
            if (!int.TryParse(GetCell(row, itemIdIndex).Trim(), out int itemId))
                continue;

            string key = GetCell(row, keyIndex).Trim();
            string value = GetCell(row, valueIndex).Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;

            if (!map.TryGetValue(itemId, out ItemTooltipContent content))
            {
                content = new ItemTooltipContent
                {
                    itemName = GetCell(row, displayNameIndex).Trim()
                };
                map[itemId] = content;
            }

            content.rows.Add(new ItemTooltipRow { key = key, value = value });
        }

        return new ItemTooltipTable(map);
    }

    public ItemTooltipContent GetContent(int itemId, string fallbackName, string fallbackDescription)
    {
        if (!byItemId.TryGetValue(itemId, out ItemTooltipContent tableContent))
        {
            return new ItemTooltipContent
            {
                itemName = fallbackName,
                itemDescription = fallbackDescription
            };
        }

        var content = new ItemTooltipContent
        {
            itemName = string.IsNullOrWhiteSpace(tableContent.itemName) ? fallbackName : tableContent.itemName,
            itemDescription = fallbackDescription
        };
        content.rows.AddRange(tableContent.rows);
        return content;
    }

    private static int FindColumn(List<string> header, string column)
    {
        if (header == null)
            return -1;

        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), column, StringComparison.OrdinalIgnoreCase))
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
