using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AudioPlayerPrefabSplitTests
{
    private const string RuntimeBgmPlayerPath = "Assets/godlotto/Resources/Audio/BGM Player.prefab";
    private const string RuntimeSfxPlayerPath = "Assets/godlotto/Resources/Audio/SFX Player.prefab";

    [Test]
    public void RuntimeBgmPlayer_HasBgmControllerOnly()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeBgmPlayerPath);

        Assert.IsNotNull(prefab);
        Assert.IsNotNull(prefab.GetComponent<AudioController>());
        Assert.IsNull(prefab.GetComponent<SfxController>());
    }

    [Test]
    public void RuntimeSfxPlayer_HasSfxControllerOnly()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeSfxPlayerPath);

        Assert.IsNotNull(prefab);
        Assert.IsNotNull(prefab.GetComponent<SfxController>());
        Assert.IsNull(prefab.GetComponent<AudioController>());
    }
}
