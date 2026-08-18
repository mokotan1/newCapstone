using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BookPanelController 페이지 인덱스 클램프·넘김 클릭 영역 활성 경계.
/// </summary>
public class BookPanelNavigationBoundsTests
{
    GameObject panelObject;
    GameObject page0;
    GameObject page1;
    GameObject page2;
    GameObject orphanPage4;
    GameObject nextArea;
    GameObject prevArea;
    BookPanelController controller;
    string prefKey;

    [SetUp]
    public void SetUp()
    {
        panelObject = new GameObject("CookBook_Panel_NavTest");
        panelObject.SetActive(false);

        page0 = CreatePageChild("CookBookPage1");
        page1 = CreatePageChild("CookBookPage2");
        page2 = CreatePageChild("CookBookPage3");
        orphanPage4 = CreatePageChild("CookBookPage4");
        orphanPage4.SetActive(true);

        nextArea = new GameObject("RightClickArea");
        nextArea.transform.SetParent(panelObject.transform, false);
        nextArea.SetActive(true);

        prevArea = new GameObject("LeftClickArea");
        prevArea.transform.SetParent(panelObject.transform, false);
        prevArea.SetActive(true);

        controller = panelObject.AddComponent<BookPanelController>();
        SetPrivateField(controller, "pages", new[] { page0, page1, page2 });
        SetPrivateField(controller, "nextPageClickArea", nextArea);
        SetPrivateField(controller, "previousPageClickArea", prevArea);

        prefKey = "LastBookPage_" + panelObject.name;
        PlayerPrefs.DeleteKey(prefKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(prefKey))
            PlayerPrefs.DeleteKey(prefKey);
        if (panelObject != null)
            Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void OnEnable_AtPageZero_PreviousOffNextOn()
    {
        PlayerPrefs.SetInt(prefKey, 0);
        panelObject.SetActive(true);

        Assert.AreEqual(0, controller.CurrentPageIndex);
        Assert.IsFalse(prevArea.activeSelf, "Previous area must be off on first page.");
        Assert.IsTrue(nextArea.activeSelf, "Next area must be on when more pages remain.");
    }

    [Test]
    public void OnEnable_AtLastPage_NextOff()
    {
        PlayerPrefs.SetInt(prefKey, 2);
        panelObject.SetActive(true);

        Assert.AreEqual(2, controller.CurrentPageIndex);
        Assert.IsTrue(prevArea.activeSelf);
        Assert.IsFalse(nextArea.activeSelf, "Next area must be off on last page (index 2 of 3).");
    }

    [Test]
    public void OnEnable_StaleSavedIndex_ClampsToLastPage()
    {
        PlayerPrefs.SetInt(prefKey, 3);
        panelObject.SetActive(true);

        Assert.AreEqual(2, controller.CurrentPageIndex, "Stale saved index 3 must clamp to 2.");
        Assert.IsFalse(nextArea.activeSelf);
    }

    [Test]
    public void NextPage_FromZero_RefreshesNavigationAreas()
    {
        PlayerPrefs.SetInt(prefKey, 0);
        panelObject.SetActive(true);

        controller.NextPage();

        Assert.AreEqual(1, controller.CurrentPageIndex);
        Assert.IsTrue(prevArea.activeSelf);
        Assert.IsTrue(nextArea.activeSelf);
    }

    [Test]
    public void NextPage_ToLast_DisablesNextArea()
    {
        PlayerPrefs.SetInt(prefKey, 1);
        panelObject.SetActive(true);

        controller.NextPage();

        Assert.AreEqual(2, controller.CurrentPageIndex);
        Assert.IsFalse(nextArea.activeSelf);
    }

    [Test]
    public void Awake_CookBookPage4Orphan_StaysInactiveAndOutOfPages()
    {
        panelObject.SetActive(true);

        var pages = GetPrivateField<GameObject[]>(controller, "pages");
        CollectionAssert.DoesNotContain(pages, orphanPage4);
        Assert.IsFalse(orphanPage4.activeSelf, "CookBookPage4 orphan must stay inactive.");
    }

    GameObject CreatePageChild(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panelObject.transform, false);
        return go;
    }

    static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
