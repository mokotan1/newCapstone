using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위한 네임스페이스 추가

public class SceneLoader : MonoBehaviour
{
    // public 함수로 만들어 버튼에 연결할 수 있게 함
    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

}