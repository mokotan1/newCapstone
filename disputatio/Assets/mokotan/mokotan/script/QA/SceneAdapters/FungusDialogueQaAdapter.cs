#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Scene-agnostic Fungus Say/Menu hops for the live QA runner.
    /// Hall→Kitchen hops themselves are Fade/Wait/LoadScene; Kitchen Start and
    /// later rooms still block on Writer/Menu. Probe never spawns dialog prefabs.
    /// </summary>
    public static class FungusDialogueQaAdapter
    {
        public const string ProbeCapabilityId = "fungus.dialogue.probe";
        public const string AdvanceCapabilityId = "fungus.dialogue.advance";
        public const string ChooseCapabilityId = "fungus.dialogue.choose";

        /// <summary>
        /// Registers probe/advance/choose. SceneId is Hall_playerble (phase-1 owner)
        /// but handlers inspect the active Play Mode scene, so later rooms reuse them.
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.HallPlayable;
            registry.Register(
                new DeveloperQaCapability(
                    ProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{sayActive:bool,waitingForInput:bool,writing:bool,menuActive:bool,optionCount:int}"),
                _ => MapProbe());

            registry.Register(
                new DeveloperQaCapability(
                    AdvanceCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{advanced:int}"),
                _ => MapAdvance());

            registry.Register(
                new DeveloperQaCapability(
                    ChooseCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{chosen:bool,optionCount:int}"),
                _ => MapChoose());
        }

        /// <summary>
        /// Play Mode snapshot of the active Say writer and Menu options.
        /// Does not call GetSayDialog/GetMenuDialog (those instantiate prefabs).
        /// </summary>
        public static DeveloperQaResult MapProbe()
        {
            string error;
            if (!RequirePlayMode("probe", out error))
            {
                return Blocked(error);
            }

            SayDialog say = ResolveSayDialog();
            bool sayActive = say != null && say.gameObject.activeInHierarchy;
            Writer writer = sayActive ? say.GetComponentInChildren<Writer>(true) : null;
            MenuDialog menu = ResolveMenuDialog();
            int optionCount = CountDisplayedOptions(menu);
            bool menuActive = menu != null && menu.IsActive() && optionCount > 0;

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Fungus dialogue probe.",
                data: new Dictionary<string, string>
                {
                    ["sayActive"] = sayActive.ToString(),
                    ["waitingForInput"] = (writer != null && writer.IsWaitingForInput).ToString(),
                    ["writing"] = (writer != null && writer.IsWriting).ToString(),
                    ["menuActive"] = menuActive.ToString(),
                    ["optionCount"] = optionCount.ToString()
                });
        }

        /// <summary>
        /// Forwards one Writer.OnNextLineEvent through the existing say pump.
        /// </summary>
        public static DeveloperQaResult MapAdvance()
        {
            string error;
            if (!RequirePlayMode("advance", out error))
            {
                return Blocked(error);
            }

            int advanced = DeveloperQaFungusSayPump.TryAdvanceActiveWriters();
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Fungus say advanced (" + advanced + ").",
                data: new Dictionary<string, string>
                {
                    ["advanced"] = advanced.ToString()
                });
        }

        /// <summary>
        /// Invokes the first interactable displayed Menu button. Index 0 is the
        /// Hall_Left Clicked_Yes / Kitchen yes path used by the frozen route.
        /// </summary>
        public static DeveloperQaResult MapChoose()
        {
            string error;
            if (!RequirePlayMode("choose", out error))
            {
                return Blocked(error);
            }

            MenuDialog menu = ResolveMenuDialog();
            int optionCount = CountDisplayedOptions(menu);
            if (menu == null || !menu.IsActive() || optionCount <= 0)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "No active Fungus menu option to choose.",
                    data: new Dictionary<string, string>
                    {
                        ["chosen"] = "False",
                        ["optionCount"] = optionCount.ToString()
                    });
            }

            Button[] buttons = menu.CachedButtons;
            if (buttons != null)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button == null || !button.gameObject.activeSelf || !button.interactable)
                    {
                        continue;
                    }

                    button.onClick.Invoke();
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.Ok,
                        "Fungus menu option chosen.",
                        data: new Dictionary<string, string>
                        {
                            ["chosen"] = "True",
                            ["optionCount"] = optionCount.ToString(),
                            ["index"] = i.ToString()
                        });
                }
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                "Fungus menu has displayed options but no interactable button.",
                data: new Dictionary<string, string>
                {
                    ["chosen"] = "False",
                    ["optionCount"] = optionCount.ToString()
                });
        }

        private static bool RequirePlayMode(string action, out string error)
        {
            if (!Application.isPlaying)
            {
                error = "Fungus dialogue " + action + " requires Play Mode.";
                return false;
            }

            error = null;
            return true;
        }

        private static DeveloperQaResult Blocked(string message)
        {
            return new DeveloperQaResult(DeveloperQaResultCode.EnvironmentBlocked, message);
        }

        private static SayDialog ResolveSayDialog()
        {
            if (SayDialog.ActiveSayDialog != null)
            {
                return SayDialog.ActiveSayDialog;
            }

            return UnityEngine.Object.FindFirstObjectByType<SayDialog>();
        }

        private static MenuDialog ResolveMenuDialog()
        {
            if (MenuDialog.ActiveMenuDialog != null)
            {
                return MenuDialog.ActiveMenuDialog;
            }

            return UnityEngine.Object.FindFirstObjectByType<MenuDialog>();
        }

        private static int CountDisplayedOptions(MenuDialog menu)
        {
            if (menu == null)
            {
                return 0;
            }

            return menu.DisplayedOptionsCount;
        }
    }
}
#endif
