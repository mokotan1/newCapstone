using NUnit.Framework;
using UnityEngine;

public class PuzzleBookPageItemGateTests
{
    [Test]
    public void ApplyVisibility_ShowsPickupOnlyOnConfiguredPage()
    {
        var panel = new GameObject("PuzzlePanel");
        var bookPanel = panel.AddComponent<BookPanelController>();
        var pickup = new GameObject("BookmarkMirrorPickup");
        pickup.transform.SetParent(panel.transform, false);

        var gate = panel.AddComponent<PuzzleBookPageItemGate>();
        typeof(PuzzleBookPageItemGate)
            .GetField("bookPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(gate, bookPanel);
        typeof(PuzzleBookPageItemGate)
            .GetField("pickupObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(gate, pickup);
        typeof(PuzzleBookPageItemGate)
            .GetField("visibleOnPageIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(gate, 1);

        typeof(BookPanelController)
            .GetField("currentPageIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(bookPanel, 0);

        gate.SendMessage("OnEnable");
        Assert.IsFalse(pickup.activeSelf, "Pickup should stay hidden on page 0.");

        typeof(BookPanelController)
            .GetField("currentPageIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(bookPanel, 1);

        typeof(PuzzleBookPageItemGate)
            .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(gate, null);
        Assert.IsTrue(pickup.activeSelf, "Pickup should appear on page 1.");

        Object.DestroyImmediate(panel);
    }
}
