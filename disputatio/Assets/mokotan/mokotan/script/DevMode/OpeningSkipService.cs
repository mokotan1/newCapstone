using UnityEngine;
using UnityEngine.SceneManagement;

public class OpeningSkipService : MonoBehaviour
{
    [SerializeField] private string mainGameplaySceneName = "Hall_playerble";

    public void SkipOpening()
    {
        if (string.IsNullOrEmpty(mainGameplaySceneName))
        {
            GameLog.LogWarning("[OpeningSkipService] mainGameplaySceneName이 비어 있습니다.");
            return;
        }

        InventoryAccessState.Unlock();
        SceneManager.LoadScene(mainGameplaySceneName);
    }
}
