using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen R6-B에서 Flowchart.ExecuteBlock → OnInteraction으로 이관된 UI(Execute 박스)가
    /// 투명·전면 오버레이로 남아 다른 클릭을 가로채지 않도록 레이캐스트를 보정합니다.
    /// </summary>
    public static class KitchenFlowchartExecuteUiRaycastPolicy
    {
        internal const float TransparentAlphaThreshold = 0.05f;

        public static void Apply(KitchenInteractionController controller)
        {
            if (controller == null)
                return;

            var configuredButtons = new HashSet<Button>();
            KitchenPanelRegistry registry = controller.GetComponent<KitchenPanelRegistry>();
            if (registry != null)
            {
                foreach (GameObject panel in registry.GetAllPanels())
                {
                    DisableDecorativePanelBackgroundRaycast(panel);
                    EnsurePanelCanvasGroup(panel);
                }
            }

            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsExecuteRoutedButton(button, controller) || !configuredButtons.Add(button))
                    continue;

                ConfigureExecuteButton(button, registry);
            }
        }

        internal static bool IsExecuteRoutedButton(Button button, KitchenInteractionController controller)
        {
            if (button == null || controller == null)
                return false;

            int callCount = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < callCount; i++)
            {
                if (button.onClick.GetPersistentTarget(i) != controller)
                    continue;

                if (button.onClick.GetPersistentMethodName(i) == nameof(RoomInteractionController.OnInteraction))
                    return true;
            }

            return false;
        }

        internal static void ConfigureExecuteButton(Button button, KitchenPanelRegistry registry = null)
        {
            if (button == null)
                return;

            Graphic graphic = button.targetGraphic;
            if (graphic == null)
                return;

            if (!IsTransparentGraphic(graphic))
                return;

            if (IsPanelHitTargetButton(button, registry))
                return;

            graphic.raycastTarget = false;
        }

        internal static bool IsPanelHitTargetButton(Button button, KitchenPanelRegistry registry)
        {
            if (button == null || registry == null)
                return false;

            Transform buttonTransform = button.transform;
            foreach (GameObject panel in registry.GetAllPanels())
            {
                if (panel == null)
                    continue;

                if (buttonTransform.IsChildOf(panel.transform) || buttonTransform == panel.transform)
                    return true;
            }

            return false;
        }

        internal static void EnsurePanelCanvasGroup(GameObject panelRoot)
        {
            if (panelRoot == null)
                return;

            CanvasGroup group = panelRoot.GetComponent<CanvasGroup>();
            if (group == null)
                group = panelRoot.AddComponent<CanvasGroup>();

            group.blocksRaycasts = true;
            group.interactable = true;
        }

        internal static void DisableDecorativePanelBackgroundRaycast(GameObject panelRoot)
        {
            if (panelRoot == null)
                return;

            Image background = panelRoot.GetComponent<Image>();
            if (background == null || panelRoot.GetComponent<Selectable>() != null)
                return;

            background.raycastTarget = false;
        }

        internal static bool IsTransparentGraphic(Graphic graphic)
        {
            return graphic != null && graphic.color.a < TransparentAlphaThreshold;
        }
    }
}
