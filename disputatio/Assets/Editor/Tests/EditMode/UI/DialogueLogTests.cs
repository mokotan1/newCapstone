using System.Collections.Generic;
using System.Reflection;
using Mokotan.StandingDialogue;
using NUnit.Framework;
using TMPro;
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
        StandingDialogueManager.ActiveStandingDialogue = null;
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
    public void TryAppend_SkipsDuplicateEvenWhenNotLastEntry()
    {
        var entries = new List<DialogueLogEntry>
        {
            new DialogueLogEntry("Alice", "Same line."),
            new DialogueLogEntry("Bob", "Other line."),
        };

        Assert.IsFalse(DialogueLogLogic.TryAppend(entries, "Alice", "Same line."));
        Assert.AreEqual(2, entries.Count);
    }

    [Test]
    public void ContainsDuplicate_NormalizesNullSpeakerAndText()
    {
        var entries = new List<DialogueLogEntry>
        {
            new DialogueLogEntry(null, "Narration."),
            new DialogueLogEntry("Bob", "Line two."),
        };

        Assert.IsTrue(DialogueLogLogic.ContainsDuplicate(entries, string.Empty, "Narration."));
        Assert.IsFalse(DialogueLogLogic.ContainsDuplicate(entries, null, "Line two."));
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

    [Test]
    public void FormatSpeakerLine_Parchment_UsesSafeAsciiPrefix()
    {
        string line = DialogueLogLogic.FormatSpeakerLine("Chester", DialogueLogVisualStyle.ParchmentCodex);

        Assert.AreEqual("> Chester", line);
        Assert.IsFalse(DialogueLogLogic.ContainsRiskyOrnamentCharacters(line));
    }

    [Test]
    public void FormatPanelTitle_Parchment_DoesNotUseRiskyUnicodeOrnaments()
    {
        string title = DialogueLogLogic.FormatPanelTitle(DialogueLogVisualStyle.ParchmentCodex);

        Assert.AreEqual(DialogueLogLogic.ParchmentTitleText, title);
        Assert.IsFalse(DialogueLogLogic.ContainsRiskyOrnamentCharacters(title));
        Assert.IsFalse(DialogueLogLogic.ContainsRiskyOrnamentCharacters(
            DialogueLogLogic.FormatSpeakerRichText("Chester", DialogueLogVisualStyle.ParchmentCodex)));
    }

    [Test]
    public void FormatPanelTitle_DarkConfession_MatchesMockupTitle()
    {
        Assert.AreEqual("L O G", DialogueLogLogic.FormatPanelTitle(DialogueLogVisualStyle.DarkConfession));
    }

    [Test]
    public void ResolveEntryPrefab_SelectsConfiguredStyleLayerPrefab()
    {
        DialogueLogPanel panel = CreatePanelWithStyleLayers(out _);

        var parchment = new GameObject("ParchmentEntry");
        var dark = new GameObject("DarkEntry");
        var legacy = new GameObject("LegacyEntry");
        createdObjects.Add(parchment);
        createdObjects.Add(dark);
        createdObjects.Add(legacy);

        var parchmentLayer = new DialogueLogStyleLayer
        {
            panelRoot = Track(new GameObject("ParchmentPanel")),
            scrollRect = CreateScrollRect(),
            entryPrefab = parchment,
        };
        var darkLayer = new DialogueLogStyleLayer
        {
            panelRoot = Track(new GameObject("DarkPanel")),
            scrollRect = CreateScrollRect(),
            entryPrefab = dark,
        };
        var legacyLayer = new DialogueLogStyleLayer
        {
            panelRoot = Track(new GameObject("LegacyPanel")),
            scrollRect = CreateScrollRect(),
            entryPrefab = legacy,
        };

        SetPrivateField(panel, "parchmentLayer", parchmentLayer);
        SetPrivateField(panel, "darkConfessionLayer", darkLayer);
        SetPrivateField(panel, "legacyLayer", legacyLayer);

        SetPrivateField(panel, "visualStyle", DialogueLogVisualStyle.ParchmentCodex);
        Assert.AreSame(parchment, panel.ResolveEntryPrefab(DialogueLogVisualStyle.ParchmentCodex));

        SetPrivateField(panel, "visualStyle", DialogueLogVisualStyle.DarkConfession);
        Assert.AreSame(dark, panel.ResolveEntryPrefab(DialogueLogVisualStyle.DarkConfession));

        SetPrivateField(panel, "visualStyle", DialogueLogVisualStyle.LegacyNotebook);
        Assert.AreSame(legacy, panel.ResolveEntryPrefab(DialogueLogVisualStyle.LegacyNotebook));
    }

    [Test]
    public void ApplyVisualStyle_ActivatesOnlySelectedLayerRoot()
    {
        DialogueLogPanel panel = CreatePanelWithStyleLayers(out _);

        var parchmentRoot = Track(new GameObject("ParchmentPanel"));
        var darkRoot = Track(new GameObject("DarkPanel"));
        var legacyRoot = Track(new GameObject("LegacyPanel"));

        SetPrivateField(panel, "parchmentLayer", new DialogueLogStyleLayer
        {
            panelRoot = parchmentRoot,
            scrollRect = CreateScrollRect(),
            entryPrefab = Track(new GameObject("ParchmentEntry")),
        });
        SetPrivateField(panel, "darkConfessionLayer", new DialogueLogStyleLayer
        {
            panelRoot = darkRoot,
            scrollRect = CreateScrollRect(),
            entryPrefab = Track(new GameObject("DarkEntry")),
        });
        SetPrivateField(panel, "legacyLayer", new DialogueLogStyleLayer
        {
            panelRoot = legacyRoot,
            scrollRect = CreateScrollRect(),
            entryPrefab = Track(new GameObject("LegacyEntry")),
        });

        SetPrivateField(panel, "visualStyle", DialogueLogVisualStyle.DarkConfession);
        panel.ApplyVisualStyle();

        Assert.IsFalse(parchmentRoot.activeSelf);
        Assert.IsTrue(darkRoot.activeSelf);
        Assert.IsFalse(legacyRoot.activeSelf);
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
    //  DialogueLogButtonSpec
    // ---------------------------------------------------------------

    [Test]
    public void GhostButtonSpec_LoadsEmbeddedDefaults_WhenJsonMissing()
    {
        DialogueLogButtonSpec.ClearCacheForTests();
        DialogueLogButtonSpec.GhostButtonSpec spec = DialogueLogButtonSpec.Load("docs/nonexistent-ghost-button.spec.json");

        Assert.AreEqual(DialogueLogButtonSpec.SpecId, spec.id);
        Assert.AreEqual("LogButton", spec.buttonName);
        Assert.AreEqual(new Vector2(56f, 56f), spec.recommendedHitArea);
        Assert.AreEqual("로그", spec.captionText);
        Assert.AreEqual(12f, spec.captionFontSize);
        Assert.AreEqual(new Color(0.839f, 0.745f, 0.588f, 0.62f), spec.foregroundIdle);
        Assert.AreEqual(new Color(0.906f, 0.788f, 0.471f, 1f), spec.accent);
    }

    [Test]
    public void GhostButtonSpec_LoadsProjectJson_WhenPresent()
    {
        DialogueLogButtonSpec.ClearCacheForTests();
        DialogueLogButtonSpec.GhostButtonSpec spec = DialogueLogButtonSpec.Load();

        Assert.AreEqual(DialogueLogButtonSpec.SpecId, spec.id);
        Assert.AreEqual(new Vector2(24f, 24f), spec.iconRenderSize);
        Assert.AreEqual(4f, spec.layoutSpacing);
        Assert.AreEqual(0.15f, spec.colorBlock.fadeDuration);
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

    [Test]
    public void GhostButtonHover_KeepsUnderlineActive_WhenIdle()
    {
        var buttonRoot = Track(new GameObject("LogButton", typeof(RectTransform)));
        var underline = Track(new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)));
        underline.transform.SetParent(buttonRoot.transform, false);

        var hover = buttonRoot.AddComponent<DialogueLogGhostButtonHover>();
        hover.Initialize(underline, null, null, Color.white, Color.yellow);

        Assert.IsTrue(underline.activeSelf);
        Assert.AreEqual(0f, underline.GetComponent<CanvasGroup>().alpha);
    }

    [Test]
    public void Typography_BodyUsesReadableClampRange()
    {
        DialogueLogStyleSpec.ClearCacheForTests();
        var labelGo = Track(new GameObject("Body", typeof(TextMeshProUGUI)));
        var label = labelGo.GetComponent<TextMeshProUGUI>();

        DialogueLogTypography.ApplyBody(label, DialogueLogVisualStyle.ParchmentCodex);

        Assert.GreaterOrEqual(label.fontSizeMax, DialogueLogTypography.BodyFontMax);
        Assert.GreaterOrEqual(label.fontSizeMin, DialogueLogTypography.BodyFontMin);
        Assert.GreaterOrEqual(label.lineSpacing, 16f);
        Assert.IsTrue(label.enableAutoSizing);
    }

    [Test]
    public void Typography_SpeakerIsSmallerThanBody()
    {
        DialogueLogStyleSpec.ClearCacheForTests();
        var speakerGo = Track(new GameObject("Speaker", typeof(TextMeshProUGUI)));
        var bodyGo = Track(new GameObject("Body", typeof(TextMeshProUGUI)));
        var speaker = speakerGo.GetComponent<TextMeshProUGUI>();
        var body = bodyGo.GetComponent<TextMeshProUGUI>();

        DialogueLogTypography.ApplyEntryTypography(DialogueLogVisualStyle.ParchmentCodex, speaker, body);

        Assert.Less(speaker.fontSizeMax, body.fontSizeMax);
    }

    [Test]
    public void EntryView_Bind_AppliesLargeBodyTypography()
    {
        DialogueLogStyleSpec.ClearCacheForTests();

        var root = Track(new GameObject("Entry", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup), typeof(DialogueLogEntryView)));
        var speaker = Track(new GameObject("Speaker", typeof(RectTransform), typeof(TextMeshProUGUI)));
        var body = Track(new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI)));
        speaker.transform.SetParent(root.transform, false);
        body.transform.SetParent(root.transform, false);

        var entryView = root.GetComponent<DialogueLogEntryView>();
        SetPrivateField(entryView, "style", DialogueLogVisualStyle.ParchmentCodex);
        SetPrivateField(entryView, "speakerLabel", speaker.GetComponent<TextMeshProUGUI>());
        SetPrivateField(entryView, "bodyLabel", body.GetComponent<TextMeshProUGUI>());

        entryView.Bind(new DialogueLogEntry("Alice", "긴 대사 본문 테스트"), DialogueLogStylePalette.ParchmentCodex);

        var bodyLabel = body.GetComponent<TextMeshProUGUI>();
        Assert.GreaterOrEqual(bodyLabel.fontSizeMax, DialogueLogTypography.BodyFontMax);
        Assert.AreEqual("긴 대사 본문 테스트", bodyLabel.text);
    }

    [Test]
    public void GodlottoDialogInput_IsPointerOverLogButton_ReturnsFalse_WithoutPointer()
    {
        Assert.IsFalse(GodlottoDialogInput.IsPointerOverDialogueLogButton());
    }

    [Test]
    public void SayDialogSnapshot_RestoresStandingCharacterLayout_AfterCanvasRebuild()
    {
        var root = Track(new GameObject("StandingRoot"));
        var leftSlot = Track(new GameObject("LeftSlot", typeof(RectTransform)));
        leftSlot.transform.SetParent(root.transform, false);

        var leftCharGo = Track(new GameObject("LeftChar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)));
        leftCharGo.transform.SetParent(leftSlot.transform, false);
        var leftChar = leftCharGo.GetComponent<Image>();
        var leftRect = leftChar.rectTransform;
        leftRect.anchorMin = Vector2.zero;
        leftRect.anchorMax = Vector2.one;
        leftRect.anchoredPosition = new Vector2(0f, -300f);

        StandingDialogueManager standing = root.AddComponent<StandingDialogueManager>();
        SetPrivateField(standing, "leftCharImage", leftChar);
        StandingDialogueManager.ActiveStandingDialogue = standing;

        DialogueLogSayDialogSnapshot snapshot = DialogueLogSayDialogSnapshot.Capture();
        leftRect.anchoredPosition = Vector2.zero;

        snapshot.Restore();

        Assert.AreEqual(new Vector2(0f, -300f), leftRect.anchoredPosition);
    }

    private DialogueLogPanel CreatePanel(out GameObject logPanelRoot)
    {
        DialogueLogPanel panel = CreatePanelWithStyleLayers(out logPanelRoot);
        SetPrivateField(panel, "logPanel", logPanelRoot);
        return panel;
    }

    private DialogueLogPanel CreatePanelWithStyleLayers(out GameObject logPanelRoot)
    {
        var root = Track(new GameObject("DialogueLogPanelRoot"));
        logPanelRoot = Track(new GameObject("LogPanel"));
        logPanelRoot.transform.SetParent(root.transform);
        logPanelRoot.SetActive(false);

        DialogueLogPanel panel = root.AddComponent<DialogueLogPanel>();
        DialogueLogPanel.EnsureInstanceForTests(panel);
        return panel;
    }

    private ScrollRect CreateScrollRect()
    {
        var scrollRoot = Track(DefaultControls.CreateScrollView(new DefaultControls.Resources()));
        return scrollRoot.GetComponent<ScrollRect>();
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
