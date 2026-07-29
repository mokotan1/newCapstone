#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Input;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TutorRoom QA must open the real quiz input panel instead of failing as a Task-12 stub.
/// </summary>
[TestFixture]
public class TutorRoomQaAdapterTests
{
    GameObject root;
    GameObject panel;
    QuizInputHandler handler;

    [SetUp]
    public void SetUp()
    {
        TutorRoomQaAdapter.ResetQuizInputHandlerResolverForTests();

        root = new GameObject("TutorRoomQaTestRoot");
        panel = new GameObject("QuizInputPanel");
        panel.transform.SetParent(root.transform, false);
        panel.SetActive(false);

        handler = root.AddComponent<QuizInputHandler>();
        SetPrivateField(handler, "inputPanel", panel);
        SetPrivateField(handler, "inputField", null);
        SetPrivateField(handler, "debugLogInputField", false);

        TutorRoomQaAdapter.QuizInputHandlerResolverForTests = () => handler;
    }

    [TearDown]
    public void TearDown()
    {
        TutorRoomQaAdapter.ResetQuizInputHandlerResolverForTests();
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TryClick_QuizInput_ActivatesPanel()
    {
        var adapter = new TutorRoomQaAdapter();

        bool ok = adapter.TryClick(
            QaTargetId.Create(TutorRoomQaAdapter.QuizInputTargetIdValue),
            out string error);

        Assert.IsTrue(ok, error);
        Assert.IsTrue(panel.activeSelf);
        Assert.IsTrue(string.IsNullOrEmpty(error));
    }

    [Test]
    public void TryClick_WhenHandlerMissing_ReturnsExplicitError()
    {
        TutorRoomQaAdapter.QuizInputHandlerResolverForTests = () => null;
        var adapter = new TutorRoomQaAdapter();

        bool ok = adapter.TryClick(
            QaTargetId.Create(TutorRoomQaAdapter.QuizInputTargetIdValue),
            out string error);

        Assert.IsFalse(ok);
        StringAssert.Contains("QuizInputHandler", error);
        StringAssert.Contains(SceneNames.TutorRoom, error);
    }

    [Test]
    public void CaptureSnapshot_ReportsQuizInputFound()
    {
        var adapter = new TutorRoomQaAdapter();

        QaSceneSnapshot snapshot = adapter.CaptureSnapshot();

        Assert.AreEqual(bool.TrueString, snapshot.Values["quizInputFound"]);
        Assert.AreEqual(bool.FalseString, snapshot.Values["quizInputPanelActive"]);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly);
            if (field == null)
            {
                continue;
            }

            field.SetValue(target, value);
            return;
        }

        Assert.Fail("Field not found: " + fieldName);
    }
}
#endif
