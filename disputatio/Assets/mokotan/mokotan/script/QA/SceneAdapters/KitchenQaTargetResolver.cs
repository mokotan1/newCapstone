#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Godlotto.Interaction;
using Godlotto.QA.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Resolves Kitchen QA target ids to clickable scene <see cref="GameObject"/>s for
    /// <see cref="Godlotto.QA.Input.QaEventSystemInputDriver"/> (RealInput). Prefers
    /// <see cref="RoomUiClickForwarder"/> wired to <see cref="KitchenSinkInteractionGate.FaucetInteractionId"/>,
    /// then the player-visible Button named <c>Faucet</c> / <c>FaucetOpen</c> used by the Kitchen sink UI.
    /// Returns <c>null</c> when nothing resolvable is present (caller maps to UnknownTarget /
    /// EnvironmentBlocked — never fake Ok).
    /// </summary>
    public static class KitchenQaTargetResolver
    {
        private static readonly string[] FaucetButtonObjectNames = { "Faucet", "FaucetOpen" };

        /// <summary>
        /// Resolves <paramref name="targetId"/> to a pointer-capable GameObject, or <c>null</c>.
        /// </summary>
        public static GameObject TryResolve(QaTargetId targetId)
        {
            if (targetId.IsNone)
            {
                return null;
            }

            if (string.Equals(
                    targetId.Value,
                    KitchenQaAdapter.FaucetTargetIdValue,
                    StringComparison.Ordinal))
            {
                return TryResolveFaucetClickTarget();
            }

            return null;
        }

        private static GameObject TryResolveFaucetClickTarget()
        {
            RoomUiClickForwarder[] forwarders =
                UnityEngine.Object.FindObjectsByType<RoomUiClickForwarder>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < forwarders.Length; i++)
            {
                RoomUiClickForwarder forwarder = forwarders[i];
                if (forwarder == null || !forwarder.isActiveAndEnabled)
                {
                    continue;
                }

                if (string.Equals(
                        forwarder.InteractionId,
                        KitchenSinkInteractionGate.FaucetInteractionId,
                        StringComparison.Ordinal))
                {
                    return forwarder.gameObject;
                }
            }

            Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int nameIndex = 0; nameIndex < FaucetButtonObjectNames.Length; nameIndex++)
            {
                string expectedName = FaucetButtonObjectNames[nameIndex];
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button == null || !button.isActiveAndEnabled)
                    {
                        continue;
                    }

                    if (string.Equals(button.gameObject.name, expectedName, StringComparison.Ordinal))
                    {
                        return button.gameObject;
                    }
                }
            }

            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int nameIndex = 0; nameIndex < FaucetButtonObjectNames.Length; nameIndex++)
            {
                string expectedName = FaucetButtonObjectNames[nameIndex];
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour == null
                        || !behaviour.isActiveAndEnabled
                        || !(behaviour is IPointerClickHandler)
                        || !string.Equals(behaviour.gameObject.name, expectedName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return behaviour.gameObject;
                }
            }

            return null;
        }
    }
}
#endif
