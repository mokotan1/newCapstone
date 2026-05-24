using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BookPanelControllerAutoMapTests
{
    private GameObject panelObject;

    [TearDown]
    public void TearDown()
    {
        if (panelObject != null)
            Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void AutoMapPuzzleBook_DoesNotUseBackspaceLabelAsPageText()
    {
        panelObject = new GameObject("PuzzlePanel");
        panelObject.SetActive(false);

        var backspace = new GameObject("BackspaceCornerFold", typeof(RectTransform), typeof(Button));
        backspace.transform.SetParent(panelObject.transform, false);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(backspace.transform, false);
        var labelText = label.GetComponent<TextMeshProUGUI>();

        var recipe = new GameObject("PuzzleBookRecipeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        recipe.transform.SetParent(panelObject.transform, false);
        var recipeText = recipe.GetComponent<TextMeshProUGUI>();

        var controller = panelObject.AddComponent<BookPanelController>();
        SetPrivateField(controller, "autoMapPuzzleBook", true);

        panelObject.SetActive(true);

        Assert.AreNotSame(labelText, GetPrivateField<TextMeshProUGUI>(controller, "scrapbookPageTextOverlay"));
        Assert.AreSame(recipeText, GetPrivateField<TextMeshProUGUI>(controller, "scrapbookRecipeTextOverlay"));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
