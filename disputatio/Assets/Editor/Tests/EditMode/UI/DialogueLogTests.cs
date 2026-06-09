using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class DialogueLogTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        Time.timeScale = 1f;
        SettingPanelWorldInputBlocker.End();
        DestroyAll<DialogueLogPanel>();
        DestroyAll<InGameSettingsPanel>();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        SettingPanelWorldInputBlocker.End();
        DestroyAll<DialogueLogPanel>();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    // ---------------------------------------------------------------
    //  DialogueLogEntry
    // ---------------------------------------------------------------

    [Test]
    public void Entry_NullSpeakerAndText_NormalizeToEmptyStrings()
    {
        var entry = new DialogueLogEntry(null, null);

        Assert.AreEqual(string.Empty, entry.Speaker);
        Assert.AreEqual(string.Empty, entry.Text);
    }

    [Test]
    public void Entry_PreservesProvidedSpeakerAndText()
    {
        var entry = new DialogueLogEntry("Alice", "Hello.");

        Assert.AreEqual("Alice", entry.Speaker);
        Assert.AreEqual("Hello.", entry.Text);
    }

    // ---------------------------------------------------------------
    //  DialogueLogLogic
    // ---------------------------------------------------------------

    [Test]
    public void FormatEntry_WithSpeaker_UsesBoldSpeakerAndNewline()
    {
        var entry = new DialogueLogEntry("Bob", "Line one.");

        Assert.AreEqual("<b>Bob</b>\nLine one.", DialogueLogLogic.FormatEntry(entry));
    }

    [Test]
    public void FormatEntry_WithoutSpeaker_ReturnsTextOnly()
    {
        var entry = new DialogueLogEntry(string.Empty, "Narration only.");

        Assert.AreEqual("Narration only.", DialogueLogLogic.FormatEntry(entry));
    }

    [Test]
    public void TryAppend_SkipsDuplicateSpeakerAndText()
    {
        var entries = new List<DialogueLogEntry>
        {
            new DialogueLogEntry("Alice", "Same line.")
        };

        Assert.IsFalse(DialogueLogLogic.TryAppend(entries, "Alice", "Same line."));
        Assert.AreEqual(1, entries.Count);
    }

    [Test]
    public void TryAppend_AllowsDifferentTextOrSpeaker()
    {
        var entries = new List<DialogueLogEntry>
        {
            new DialogueLogEntry("Alice", "First line.")
        };

        Assert.IsTrue(DialogueLogLogic.TryAppend(entries, "Alice", "Second line."));
        Assert.IsTrue(DialogueLogLogic.TryAppend(entries, "Bob", "First line."));
        Assert.AreEqual(3, entries.Count);
    }

    [Test]
    public void TryAppend_RejectsWhitespaceOnlyText()
    {
        var entries = new List<DialogueLogEntry>();

        Assert.IsFalse(DialogueLogLogic.TryAppend(entries, "Alice", "   "));
        Assert.IsFalse(DialogueLogLogic.TryAppend(entries, "Alice", null));
        Assert.AreEqual(0, entries.Count);
    }

    // ---------------------------------------------------------------
    //  DialogueLogPanel open/close
    // ---------------------------------------------------------------

    [Test]
    public void OpenCloseAndToggle_UpdateIsOpenAndPanelActiveState()
    {
        DialogueLogPanel panel = CreatePanel(out GameObject logPanelRoot);

        Assert.IsFalse(panel.IsOpen);
        Assert.IsFalse(logPanelRoot.activeSelf);

        panel.Open();
        Assert.IsTrue(panel.IsOpen);
        Assert.IsTrue(logPanelRoot.activeSelf);

        panel.Close();
        Assert.IsFalse(panel.IsOpen);
        Assert.IsFalse(logPanelRoot.activeSelf);

        panel.Toggle();
        Assert.IsTrue(panel.IsOpen);
        Assert.IsTrue(logPanelRoot.activeSelf);

        panel.Toggle();
        Assert.IsFalse(panel.IsOpen);
        Assert.IsFalse(logPanelRoot.activeSelf);
    }

    [Test]
    public void Open_IsIdempotent_WhenAlreadyOpen()
    {
        DialogueLogPanel panel = CreatePanel(out GameObject logPanelRoot);

        panel.Open();
        panel.Open();

        Assert.IsTrue(panel.IsOpen);
        Assert.IsTrue(logPanelRoot.activeSelf);
    }

    [Test]
    public void Close_IsIdempotent_WhenAlreadyClosed()
    {
        DialogueLogPanel panel = CreatePanel(out _);

        Assert.DoesNotThrow(() => panel.Close());
        Assert.IsFalse(panel.IsOpen);
    }

    [Test]
    public void Open_SetsTimeScaleZero_Close_RestoresOne()
    {
        Time.timeScale = 1f;
        DialogueLogPanel panel = CreatePanel(out _);

        panel.Open();
        Assert.AreEqual(0f, Time.timeScale);

        panel.Close();
        Assert.AreEqual(1f, Time.timeScale);
    }

    // ---------------------------------------------------------------
    //  DialogueLogButton
    // ---------------------------------------------------------------

    [Test]
    public void ButtonToggle_DelegatesToPanelInstance()
    {
        DialogueLogPanel panel = CreatePanel(out _);

        Assert.IsFalse(panel.IsOpen);

        DialogueLogButton button = CreateLogButton();

        button.Toggle();
        Assert.IsTrue(panel.IsOpen);

        button.Toggle();
        Assert.IsFalse(panel.IsOpen);
    }

    [Test]
    public void ButtonToggle_DoesNotThrow_WhenPanelInstanceMissing()
    {
        DialogueLogButton button = CreateLogButton();

        Assert.DoesNotThrow(() => button.Toggle());
    }

    private DialogueLogPanel CreatePanel(out GameObject logPanelRoot)
    {
        var root = Track(new GameObject("DialogueLogPanelRoot"));
        logPanelRoot = Track(new GameObject("LogPanel"));
        logPanelRoot.transform.SetParent(root.transform);
        logPanelRoot.SetActive(false);

        DialogueLogPanel panel = root.AddComponent<DialogueLogPanel>();
        DialogueLogPanel.EnsureInstanceForTests(panel);
        SetPrivateField(panel, "logPanel", logPanelRoot);
        return panel;
    }

    private DialogueLogButton CreateLogButton()
    {
        var buttonObject = Track(new GameObject("LogButton"));
        buttonObject.AddComponent<Button>();
        return buttonObject.AddComponent<DialogueLogButton>();
    }

    private GameObject Track(GameObject go)
    {
        createdObjects.Add(go);
        return go;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field not found: {fieldName}");
        field.SetValue(target, value);
    }

    private static void DestroyAll<T>() where T : MonoBehaviour
    {
        T[] found;
        do
        {
            found = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject != null)
                    Object.DestroyImmediate(found[i].gameObject);
            }
        }
        while (found.Length > 0);
    }
}
