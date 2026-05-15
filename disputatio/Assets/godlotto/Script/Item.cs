using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ItemTooltipRow
{
    public string key = "";
    public string value = "";
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    [Tooltip("1~30. ItemAcquisitionTracker 비트마스크 인덱스(중복 금지).")]
    [Range(1, 30)]
    public int itemId = 1;

    public string itemName = "New Item";
    public Sprite icon = null;
    [TextArea]
    public string itemDescription = "";
    public List<ItemTooltipRow> tooltipRows = new List<ItemTooltipRow>();
}
