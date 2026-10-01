using Fungus;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[TestFixture]
public sealed class SecondFloorRoomEntranceSceneTests
{
    [Test]
    public void WifeEntrance_IsRewiredWithoutFlowchart()
    {
        AssertScene(
            "Assets/Scenes/Mokotan/Second Floor/WifeEntrance.unity",
            "Wife_Door",
            SecondFloorRoomEntrance.Wife);
    }

    [Test]
    public void BedEntrance_IsRewiredWithoutFlowchart()
    {
        AssertScene(
            "Assets/Scenes/Mokotan/Second Floor/BedEntrance.unity",
            "Bed_Door",
            SecondFloorRoomEntrance.Bed);
    }

    static void AssertScene(string path, string doorName, SecondFloorRoomEntrance route)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            int flowcharts = 0;
            int controllers = 0;
            SecondFloorRoomEntranceController controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                flowcharts += root.GetComponentsInChildren<Flowchart>(true).Length;
                SecondFloorRoomEntranceController[] found = root.GetComponentsInChildren<SecondFloorRoomEntranceController>(true);
                controllers += found.Length;
                if (found.Length == 1)
                    controller = found[0];
            }

            Assert.AreEqual(0, flowcharts, path);
            Assert.AreEqual(1, controllers, path);
            Assert.AreEqual(route, controller.Route);

            GameObject door = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == doorName)
                        door = transforms[i].gameObject;
                }
            }

            Assert.IsNotNull(door, doorName);
            Collider2D collider = door.GetComponent<Collider2D>();
            Clickable2D clickable = door.GetComponent<Clickable2D>();
            WorldItemDropZone zone = door.GetComponent<WorldItemDropZone>();
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.enabled, doorName);
            Assert.IsNotNull(clickable);
            Assert.IsFalse(clickable.enabled, doorName);
            Assert.IsNotNull(zone);
            Assert.IsNull(zone.flowchart);
            Assert.AreEqual(FungusVariableKeys.IsClicked, zone.dialogBoolName);
            Assert.AreEqual(controller, zone.onUnlock.GetPersistentTarget(0));
            Assert.AreEqual("OnInteraction", zone.onUnlock.GetPersistentMethodName(0));

            Button ribbon = FindRibbon(scene);
            Assert.IsNotNull(ribbon, path);
            Assert.AreEqual(controller, ribbon.onClick.GetPersistentTarget(0));
            Assert.AreEqual("OnInteraction", ribbon.onClick.GetPersistentMethodName(0));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static Button FindRibbon(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].onClick.GetPersistentEventCount() > 0
                    && buttons[i].onClick.GetPersistentMethodName(0) == "OnInteraction")
                    return buttons[i];
            }
        }

        return null;
    }
}
