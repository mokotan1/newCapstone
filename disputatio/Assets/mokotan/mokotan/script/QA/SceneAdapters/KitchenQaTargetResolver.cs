#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Resolves Kitchen QA target ids to clickable scene <see cref="GameObject"/>s for
    /// <see cref="Godlotto.QA.Input.QaEventSystemInputDriver"/> (RealInput).
    /// Supports faucet, sink dropzone, and active MaidRoomKey. Returns <c>null</c> when
    /// unresolved (caller maps to UnknownTarget / EnvironmentBlocked — never fake Ok).
    /// </summary>
    public static class KitchenQaTargetResolver
    {
        private static readonly string[] FaucetButtonObjectNames = { "Faucet", "FaucetOpen" };
        private const string SinkDropzoneObjectName = "SinkDropzone";
        private const string MaidRoomKeyObjectName = "MaidRoomKey";

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

            if (string.Equals(
                    targetId.Value,
                    KitchenQaAdapter.SinkDropzoneTargetIdValue,
                    StringComparison.Ordinal))
            {
                return TryResolveNamedActiveObject(SinkDropzoneObjectName);
            }

            if (string.Equals(
                    targetId.Value,
                    KitchenQaAdapter.MaidKeyTargetIdValue,
                    StringComparison.Ordinal))
            {
                return TryResolveMaidKey();
            }

            return null;
        }

        private static GameObject TryResolveMaidKey()
        {
            ItemPickup[] pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                ItemPickup pickup = pickups[i];
                if (pickup == null || !pickup.isActiveAndEnabled)
                {
                    continue;
                }

                if (string.Equals(pickup.gameObject.name, MaidRoomKeyObjectName, StringComparison.Ordinal))
                {
                    return pickup.gameObject;
                }

                if (pickup.item != null
                    && pickup.item.itemId == KitchenQaAdapter.MaidRoomKeyItemId)
                {
                    return pickup.gameObject;
                }
            }

            return TryResolveNamedActiveObject(MaidRoomKeyObjectName);
        }

        private static GameObject TryResolveNamedActiveObject(string objectName)
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject found = FindNamedActiveDescendant(roots[i].transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            // Fallback: name search across loaded objects (active only).
            GameObject byName = GameObject.Find(objectName);
            if (byName != null && byName.activeInHierarchy)
            {
                return byName;
            }

            return null;
        }

        private static GameObject FindNamedActiveDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, objectName, StringComparison.Ordinal)
                && root.gameObject.activeInHierarchy)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindNamedActiveDescendant(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
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
