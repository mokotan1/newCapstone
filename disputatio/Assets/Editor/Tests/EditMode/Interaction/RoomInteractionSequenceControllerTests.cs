using Fungus;
using Godlotto.Interaction;
using Godlotto.Sequence;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class RoomInteractionSequenceControllerTests
{
    GameObject root;
    RoomInteractionController controller;
    RoomInteractionSequenceHost sequenceHost;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("SequenceInteractionTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<RoomInteractionController>();
        sequenceHost = root.AddComponent<RoomInteractionSequenceHost>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "sequenceHost", sequenceHost);
    }

    [TearDown]
    public void TearDown()
    {
        RoomInteractionController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void OnInteraction_SequenceRoute_SkipsFungusBlock()
    {
        var document = new SequenceDocument
        {
            schemaVersion = SequenceLimits.CurrentSchemaVersion,
            blocks = new[]
            {
                new SequenceBlock
                {
                    id = "start",
                    commands = new[] { new SequenceOp { command = "set_bool", key = "done", bool_value = true } }
                }
            }
        };
        sequenceHost.RegisterForTests("look", document, "start");

        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute
            {
                interactionId = "look",
                fungusBlockName = "LegacyBlock",
                sequenceStartBlock = "start",
                sequenceDocument = CreateTextAsset(
                    "{\"schemaVersion\":1,\"blocks\":[{\"id\":\"start\",\"commands\":[{\"command\":\"set_bool\",\"key\":\"done\",\"bool_value\":true}]}]}")
            }
        });
        RebuildLookupCaches(controller);

        bool fungusCalled = false;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, __) =>
        {
            fungusCalled = true;
            return true;
        };

        controller.OnInteraction("look");

        Assert.IsFalse(fungusCalled);
        Assert.IsTrue(sequenceHost.Flags.GetBool("done"));
    }

    [Test]
    public void OnInteraction_FungusRoute_StillExecutesBlock()
    {
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "bookcase", fungusBlockName = "Bookcase_Clicked" }
        });
        RebuildLookupCaches(controller);

        string executed = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, block) =>
        {
            executed = block;
            return true;
        };

        controller.OnInteraction("bookcase");

        Assert.AreEqual("Bookcase_Clicked", executed);
    }

    [Test]
    public void OnInteraction_SequenceRoute_LoadSceneOutcome_InvokesSceneHandler()
    {
        var document = new SequenceDocument
        {
            schemaVersion = SequenceLimits.CurrentSchemaVersion,
            blocks = new[]
            {
                new SequenceBlock
                {
                    id = "start",
                    commands = new[]
                    {
                        new SequenceOp
                        {
                            command = "set_string",
                            key = SequenceBlockOutcomeMapper.LoadSceneKey,
                            string_value = "Hall"
                        }
                    }
                }
            }
        };
        sequenceHost.RegisterForTests("exit", document, "start");

        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute
            {
                interactionId = "exit",
                sequenceStartBlock = "start",
                sequenceDocument = CreateTextAsset("{}")
            }
        });
        RebuildLookupCaches(controller);

        string loaded = null;
        RoomInteractionController.SceneLoadHandlerForTests = scene => loaded = scene;

        controller.OnInteraction("exit");

        Assert.AreEqual("Hall", loaded);
    }

    [Test]
    public void OnInteraction_SequenceRoute_GoBackOutcome_InvokesGoBackHandler()
    {
        var document = new SequenceDocument
        {
            schemaVersion = SequenceLimits.CurrentSchemaVersion,
            blocks = new[]
            {
                new SequenceBlock
                {
                    id = "start",
                    commands = new[]
                    {
                        new SequenceOp
                        {
                            command = "set_bool",
                            key = SequenceBlockOutcomeMapper.GoBackKey,
                            bool_value = true
                        }
                    }
                }
            }
        };
        sequenceHost.RegisterForTests("back", document, "start");

        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute
            {
                interactionId = "back",
                sequenceStartBlock = "start",
                sequenceDocument = CreateTextAsset("{}")
            }
        });
        RebuildLookupCaches(controller);

        bool goBack = false;
        RoomInteractionController.GoBackHandlerForTests = () => goBack = true;

        controller.OnInteraction("back");

        Assert.IsTrue(goBack);
    }

    [Test]
    public void OnInteraction_SequenceOnlyRoute_WithoutHost_LogsAndSkipsFungus()
    {
        SetPrivateField(controller, "sequenceHost", null);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute
            {
                interactionId = "look",
                sequenceStartBlock = "start",
                sequenceDocument = CreateTextAsset(
                    "{\"schemaVersion\":1,\"blocks\":[{\"id\":\"start\",\"commands\":[]}]}")
            }
        });
        RebuildLookupCaches(controller);

        bool fungusCalled = false;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, __) =>
        {
            fungusCalled = true;
            return true;
        };

        controller.OnInteraction("look");

        Assert.IsFalse(fungusCalled);
    }

    static TextAsset CreateTextAsset(string json)
    {
        return new TextAsset(json);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);
        Assert.NotNull(field, "Missing field " + fieldName);
        field.SetValue(target, value);
    }

    static void RebuildLookupCaches(RoomInteractionController target)
    {
        var method = typeof(RoomInteractionController).GetMethod(
            "BuildLookupCaches",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(target, null);
    }
}
