using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Fungus;

public class SettingPanelButtonActions : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = SceneNames.MainMenu;
    [SerializeField] private Flowchart targetFlowchart;
    [SerializeField] private string fungusVariableName = FungusVariableKeys.IsClicked;
    [SerializeField] private Fungus.DialogInput dialogInput;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private GameObject panelRoot;

    private bool isReturningToMainMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterMainMenuPanelGuard()
    {
        SceneManager.sceneLoaded -= HidePanelsOnMainMenuLoaded;
        SceneManager.sceneLoaded += HidePanelsOnMainMenuLoaded;
    }

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnEnable()
    {
        isReturningToMainMenu = false;
        ResolveReferences();
        BindButtons();
    }

    public void BackToMainMenu()
    {
        if (isReturningToMainMenu)
            return;

        isReturningToMainMenu = true;
        ResolveReferences();
        Time.timeScale = 1f;
        SettingPanelWorldInputBlocker.End();

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
            fc.SetBooleanVariable(fungusVariableName, false);

        if (dialogInput != null)
            dialogInput.enabled = true;

        HidePanel();

        // InGameSettingsPanel의 "메인메뉴로" 버튼과 동일한 DDOL 정리를 수행한다.
        // 그렇지 않으면 이 버튼으로 나간 회차의 Fungus/퀘스트 상태가 다음
        // New Game까지 살아남는다.
        CleanupDontDestroyGameplayRoots();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Runtime entry point: discovers every current DontDestroyOnLoad root and
    /// wipes the ones not covered by <see cref="DontDestroyGameplayCleanup.ShouldPreserveRoot"/>
    /// (GlobalSettingManager and this object are preserved; Fungus globals and
    /// quest tracker systems are not).
    /// </summary>
    public void CleanupDontDestroyGameplayRoots()
    {
        CleanupDontDestroyGameplayRoots(DontDestroyGameplayCleanup.FindDontDestroyOnLoadRoots());
    }

    /// <summary>
    /// EditMode-testable overload: applies the shared cleanup policy to an
    /// explicit root list with an injectable destroy callback instead of the
    /// Play-Mode-only DontDestroyOnLoad discovery.
    /// </summary>
    public void CleanupDontDestroyGameplayRoots(IList<GameObject> roots, Action<GameObject> destroyRoot = null)
    {
        DontDestroyGameplayCleanup.DestroyUnpreservedRoots(roots, gameObject, destroyRoot);
    }

    public void ReturnToGame()
    {
        ResolveReferences();
        if (panelRoot != null)
            panelRoot.SetActive(false);

        SettingPanelWorldInputBlocker.End();

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
            fc.SetBooleanVariable(fungusVariableName, false);

        if (dialogInput != null)
            dialogInput.enabled = true;

        Time.timeScale = 1f;
    }

    private void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (mainMenuButton == null)
            mainMenuButton = FindButtonByName("Main Button");

        if (returnButton == null)
            returnButton = FindButtonByName("Return Button", "ReturnButton", "Back Button");
    }

    private void BindButtons()
    {
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(BackToMainMenu);
            mainMenuButton.onClick.AddListener(BackToMainMenu);
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(ReturnToGame);
            returnButton.onClick.AddListener(ReturnToGame);
        }
    }

    private Button FindButtonByName(params string[] names)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (buttons[i].name == names[j])
                    return buttons[i];
            }
        }

        return null;
    }

    private static void HidePanelsOnMainMenuLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.MainMenu)
            return;

        SettingPanelWorldInputBlocker.End();

        SettingPanelButtonActions[] actions = Resources.FindObjectsOfTypeAll<SettingPanelButtonActions>();
        for (int i = 0; i < actions.Length; i++)
        {
            SettingPanelButtonActions action = actions[i];
            if (action == null || !action.gameObject.scene.IsValid())
                continue;

            action.ResolveReferences();
            action.isReturningToMainMenu = false;
            action.HidePanel();
        }
    }
}
