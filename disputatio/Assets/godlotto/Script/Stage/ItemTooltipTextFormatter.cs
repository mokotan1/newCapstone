using System.Collections.Generic;
using System.Text;

public static class ItemTooltipTextFormatter
{
    public static string Build(string itemName, string itemDescription)
    {
        return Build(itemName, itemDescription, null);
    }

    public static string Build(string itemName, string itemDescription, IEnumerable<ItemTooltipRow> tooltipRows)
    {
        string safeName = string.IsNullOrWhiteSpace(itemName) ? "Unknown Item" : itemName.Trim();
        List<ItemTooltipRow> safeRows = BuildSafeRows(tooltipRows);
        string safeDescription = string.IsNullOrWhiteSpace(itemDescription)
            ? (safeRows.Count == 0 ? "설명이 없습니다." : string.Empty)
            : itemDescription.Trim();

        var builder = new StringBuilder();
        builder.Append(safeName);
        if (!string.IsNullOrEmpty(safeDescription))
        {
            builder.Append('\n');
            builder.Append(safeDescription);
        }

        AppendTooltipRows(builder, safeRows);
        return builder.ToString();
    }

    private static List<ItemTooltipRow> BuildSafeRows(IEnumerable<ItemTooltipRow> tooltipRows)
    {
        var safeRows = new List<ItemTooltipRow>();
        if (tooltipRows == null)
            return safeRows;

        foreach (ItemTooltipRow row in tooltipRows)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.key) || string.IsNullOrWhiteSpace(row.value))
                continue;

            safeRows.Add(row);
        }

        return safeRows;
    }

    private static void AppendTooltipRows(StringBuilder builder, IEnumerable<ItemTooltipRow> tooltipRows)
    {
        foreach (ItemTooltipRow row in tooltipRows)
        {
            builder.Append('\n');
            builder.Append(row.key.Trim());
            builder.Append(": ");
            builder.Append(row.value.Trim());
        }
    }
}
