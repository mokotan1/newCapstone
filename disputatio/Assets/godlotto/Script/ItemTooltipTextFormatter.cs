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
        string safeDescription = string.IsNullOrWhiteSpace(itemDescription) ? "설명이 없습니다." : itemDescription.Trim();

        var builder = new StringBuilder();
        builder.Append(safeName);
        builder.Append('\n');
        builder.Append(safeDescription);
        AppendTooltipRows(builder, tooltipRows);
        return builder.ToString();
    }

    private static void AppendTooltipRows(StringBuilder builder, IEnumerable<ItemTooltipRow> tooltipRows)
    {
        if (tooltipRows == null)
            return;

        foreach (ItemTooltipRow row in tooltipRows)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.key) || string.IsNullOrWhiteSpace(row.value))
                continue;

            builder.Append('\n');
            builder.Append(row.key.Trim());
            builder.Append(": ");
            builder.Append(row.value.Trim());
        }
    }
}
