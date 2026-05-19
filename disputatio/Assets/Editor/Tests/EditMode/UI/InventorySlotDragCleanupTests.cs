using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InventorySlotDragCleanupTests
{
    [Test]
    public void ClearDragState_RemovesDragIconAndDraggedItem()
    {
        GameObject dragIcon = new GameObject("DragIcon");
        Item item = ScriptableObject.CreateInstance<Item>();
        SetPrivateStaticDragIcon(dragIcon);
        InventorySlot.draggedItem = item;

        InventorySlot.ClearDragState();

        Assert.IsNull(GetPrivateStaticDragIcon());
        Assert.IsNull(InventorySlot.draggedItem);
        Assert.IsTrue(dragIcon == null);
        Object.DestroyImmediate(item);
    }

    private static void SetPrivateStaticDragIcon(GameObject value)
    {
        GetDragIconField().SetValue(null, value);
    }

    private static GameObject GetPrivateStaticDragIcon()
    {
        return (GameObject)GetDragIconField().GetValue(null);
    }

    private static FieldInfo GetDragIconField()
    {
        return typeof(InventorySlot).GetField("dragIcon", BindingFlags.NonPublic | BindingFlags.Static);
    }
}
