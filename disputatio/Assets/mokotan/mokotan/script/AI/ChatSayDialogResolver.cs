using Fungus;
using UnityEngine;

public static class ChatSayDialogResolver
{
    public static SayDialog ResolveExistingOrInstantiate(
        string sayDialogObjectName,
        SayDialog sayDialogPrefab)
    {
        SayDialog existing = FindByName(sayDialogObjectName);
        if (existing != null)
        {
            SayDialog.ActiveSayDialog = existing;
            return existing;
        }

        if (sayDialogPrefab == null)
            return null;

        SayDialog created = Object.Instantiate(sayDialogPrefab);
        created.gameObject.name = !string.IsNullOrWhiteSpace(sayDialogObjectName)
            ? sayDialogObjectName
            : sayDialogPrefab.gameObject.name;
        created.gameObject.SetActive(false);
        SayDialog.ActiveSayDialog = created;
        return created;
    }

    private static SayDialog FindByName(string sayDialogObjectName)
    {
        if (string.IsNullOrWhiteSpace(sayDialogObjectName))
            return null;

        SayDialog[] sayDialogs = Object.FindObjectsByType<SayDialog>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (SayDialog sayDialog in sayDialogs)
        {
            if (sayDialog.gameObject.name == sayDialogObjectName)
                return sayDialog;
        }

        return null;
    }
}
