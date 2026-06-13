using UnityEngine.SceneManagement;

/// <summary>
/// Fungus 글로벌 bool·활성 씬에서 <see cref="TutorialQuestWorldFlags"/>를 읽습니다.
/// </summary>
public static class TutorialQuestWorldReader
{
    public static TutorialQuestWorldFlags ReadCurrent()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return new TutorialQuestWorldFlags(
            sceneName,
            ReadBool(FungusVariableKeys.ElectricOn),
            ReadBool(FungusVariableKeys.GetBottle),
            ReadBool(FungusVariableKeys.FaucetClicked),
            ReadBool(FungusVariableKeys.BottleDragged));
    }

    static bool ReadBool(string key)
    {
        return !string.IsNullOrEmpty(key) && FlowchartLocator.GetFungusGlobalBoolean(key);
    }
}
