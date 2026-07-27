#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.Interaction;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kitchen faucet GameObject resolution for RealInput (design §6.2).
/// </summary>
[TestFixture]
public sealed class KitchenQaTargetResolverTests
{
    private GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
            _root = null;
        }
    }

    [Test]
    public void TryResolve_UnknownTarget_ReturnsNull()
    {
        Assert.IsNull(KitchenQaTargetResolver.TryResolve(QaTargetId.Create("kitchen.unknown.target")));
    }

    [Test]
    public void TryResolve_Faucet_PrefersActiveButtonNamedFaucet()
    {
        _root = new GameObject("KitchenQaTargetResolverFixture");
        var faucetGo = new GameObject("Faucet", typeof(RectTransform));
        faucetGo.transform.SetParent(_root.transform, false);
        faucetGo.AddComponent<Image>();
        faucetGo.AddComponent<Button>();

        GameObject resolved = KitchenQaTargetResolver.TryResolve(
            QaTargetId.Create(KitchenQaAdapter.FaucetTargetIdValue));

        Assert.AreSame(faucetGo, resolved);
    }

    [Test]
    public void TryResolve_Faucet_FindsRoomUiClickForwarderByInteractionId()
    {
        _root = new GameObject("KitchenQaTargetResolverFixture");
        var forwarderGo = new GameObject("SinkFaucetUi", typeof(RectTransform));
        forwarderGo.transform.SetParent(_root.transform, false);
        RoomUiClickForwarder forwarder = forwarderGo.AddComponent<RoomUiClickForwarder>();
        forwarder.SetInteractionIdForTests(KitchenSinkInteractionGate.FaucetInteractionId);

        GameObject resolved = KitchenQaTargetResolver.TryResolve(
            QaTargetId.Create(KitchenQaAdapter.FaucetTargetIdValue));

        Assert.AreSame(forwarderGo, resolved);
    }

    [Test]
    public void TryResolve_SinkDropzone_FindsActiveObjectNamedSinkDropzone()
    {
        _root = new GameObject("KitchenQaTargetResolverFixture");
        var dropzoneGo = new GameObject("SinkDropzone");
        dropzoneGo.transform.SetParent(_root.transform, false);

        GameObject resolved = KitchenQaTargetResolver.TryResolve(
            QaTargetId.Create(KitchenQaAdapter.SinkDropzoneTargetIdValue));

        Assert.AreSame(dropzoneGo, resolved);
    }

    [Test]
    public void TryResolve_MaidKey_FindsActiveMaidRoomKey()
    {
        _root = new GameObject("KitchenQaTargetResolverFixture");
        var keyGo = new GameObject("MaidRoomKey");
        keyGo.transform.SetParent(_root.transform, false);
        keyGo.AddComponent<ItemPickup>();

        GameObject resolved = KitchenQaTargetResolver.TryResolve(
            QaTargetId.Create(KitchenQaAdapter.MaidKeyTargetIdValue));

        Assert.AreSame(keyGo, resolved);
    }

    [Test]
    public void TryResolve_MaidKey_InactiveObject_ReturnsNull()
    {
        _root = new GameObject("KitchenQaTargetResolverFixture");
        var keyGo = new GameObject("MaidRoomKey");
        keyGo.transform.SetParent(_root.transform, false);
        keyGo.SetActive(false);

        GameObject resolved = KitchenQaTargetResolver.TryResolve(
            QaTargetId.Create(KitchenQaAdapter.MaidKeyTargetIdValue));

        Assert.IsNull(resolved);
    }
}
#endif
