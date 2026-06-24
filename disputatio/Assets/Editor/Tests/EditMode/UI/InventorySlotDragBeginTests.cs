using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture]
public class InventorySlotDragBeginTests
{
    GameObject canvasRoot;
    InventorySlot slot;
    Item bottle;

    [SetUp]
    public void SetUp()
    {
        InventorySlot.ClearDragState();
        InventorySlot.draggedItem = null;

        canvasRoot = new GameObject("Canvas");
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasRoot.AddComponent<GraphicRaycaster>();

        var slotGo = new GameObject("InventorySlot");
        slotGo.transform.SetParent(canvasRoot.transform, false);
        slotGo.AddComponent<RectTransform>();
        slotGo.AddComponent<Image>().raycastTarget = true;
        slotGo.AddComponent<Button>();

        var iconGo = new GameObject("SlotIcon");
        iconGo.transform.SetParent(slotGo.transform, false);
        var iconRect = iconGo.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var iconImage = iconGo.AddComponent<Image>();
        iconImage.raycastTarget = false;

        slot = slotGo.AddComponent<InventorySlot>();
        slot.icon = iconImage;

        bottle = ScriptableObject.CreateInstance<Item>();
        bottle.itemName = "Bottle";
        bottle.icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        slot.AddItem(bottle);
    }

    [TearDown]
    public void TearDown()
    {
        InventorySlot.ClearDragState();
        InventorySlot.draggedItem = null;

        if (canvasRoot != null)
            Object.DestroyImmediate(canvasRoot);
        if (bottle != null)
            Object.DestroyImmediate(bottle);
    }

    [Test]
    public void OnBeginDrag_CreatesDragIconWithoutGraphicRaycaster()
    {
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(200f, 200f),
        };

        slot.OnBeginDrag(eventData);

        GameObject dragIcon = GetPrivateStaticDragIcon();
        Assert.IsNotNull(dragIcon, "Drag icon should be created.");
        Assert.AreEqual(bottle, InventorySlot.draggedItem);
        Assert.IsNull(dragIcon.GetComponent<GraphicRaycaster>(),
            "Drag icon must not add a nested GraphicRaycaster; it breaks UI drag.");
        Assert.IsNotNull(dragIcon.GetComponent<Canvas>());
        Assert.IsFalse(slot.icon.enabled, "Slot icon should hide while dragging.");
    }

    [Test]
    public void OnEndDrag_RestoresSlotIconAndBlocksRaycasts()
    {
        slot.OnBeginDrag(new PointerEventData(EventSystem.current) { position = new Vector2(100f, 100f) });
        slot.OnEndDrag(new PointerEventData(EventSystem.current));

        Assert.IsNull(GetPrivateStaticDragIcon());
        Assert.IsNull(InventorySlot.draggedItem);
        Assert.IsTrue(slot.icon.enabled, "Slot icon should be restored after drag ends.");

        var group = slot.GetComponent<CanvasGroup>();
        if (group != null)
            Assert.IsTrue(group.blocksRaycasts, "blocksRaycasts must be restored after drag ends.");
    }

    [Test]
    public void OnEndDrag_FilterCardBookDropZoneChild_ConsumesFilterCard()
    {
        var filterCard = ScriptableObject.CreateInstance<Item>();
        filterCard.itemName = "FilterCard";
        filterCard.icon = bottle.icon;
        slot.AddItem(filterCard);

        var dropZoneGo = new GameObject("FilterCardBookPanel", typeof(RectTransform), typeof(Image));
        dropZoneGo.transform.SetParent(canvasRoot.transform, false);
        var dropZone = dropZoneGo.AddComponent<FilterCardBookDropZone>();
        dropZone.requiredItem = filterCard;
        dropZone.maxUses = 1;

        var hitChild = new GameObject("BookOverlayRaycastChild", typeof(RectTransform), typeof(Image));
        hitChild.transform.SetParent(dropZoneGo.transform, false);

        slot.OnBeginDrag(new PointerEventData(EventSystem.current) { position = new Vector2(100f, 100f) });
        Assert.AreEqual(filterCard, InventorySlot.draggedItem);

        slot.OnEndDrag(new PointerEventData(EventSystem.current)
        {
            pointerEnter = hitChild,
        });

        Assert.IsNull(InventorySlot.draggedItem);
        Assert.IsTrue(dropZone.gameObject.activeSelf);
        Assert.IsNull(GetPrivateStaticDragIcon());

        Object.DestroyImmediate(filterCard);
    }

    static GameObject GetPrivateStaticDragIcon()
    {
        var field = typeof(InventorySlot).GetField("dragIcon", BindingFlags.NonPublic | BindingFlags.Static);
        return (GameObject)field.GetValue(null);
    }
}
