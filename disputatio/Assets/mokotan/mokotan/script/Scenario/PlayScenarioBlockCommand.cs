using System.Collections;
using UnityEngine;

namespace Fungus
{
    [CommandInfo("Scenario",
        "Play Scenario Block",
        "block_id로 JSON 시나리오를 찾아 로컬라이징된 Talk Standing 대사를 재생합니다.")]
    [AddComponentMenu("")]
    public sealed class PlayScenarioBlockCommand : Command
    {
        [Header("Scenario")]
        [SerializeField] private TextAsset scenarioJson;
        [SerializeField] private string blockId;

        [Header("Localization")]
        [SerializeField] private TextAsset dialogueLocalizationCsv;
        [SerializeField] private TextAsset speakerLocalizationCsv;

        [Tooltip(
            "When false (default), language comes from CheshireLocaleResolver "
            + "(Fungus SetLanguage / Localization). When true, uses Language Code below.")]
        [SerializeField] private bool useInspectorLanguageOverride;

        [Tooltip(
            "Inspector override only. Game authority is Fungus SetLanguage / CheshireLocaleResolver "
            + "unless Use Inspector Language Override is enabled.")]
        [SerializeField] private string languageCode = "ko";

        [Header("Dialogue")]
        [SerializeField] private SayDialog overrideSayDialog;

        public override void OnEnter()
        {
            ScenarioTalkLine[] lines = BuildLines();
            if (lines.Length == 0)
            {
                Continue();
                return;
            }

            StartCoroutine(PlayLines(lines));
        }

        /// <summary>
        /// Resolves CSV language: CheshireLocaleResolver by default; optional Inspector override.
        /// </summary>
        public static string ResolveLanguageCode(bool useInspectorLanguageOverride, string inspectorLanguageCode)
        {
            if (useInspectorLanguageOverride)
                return CheshireLocaleResolver.NormalizeLocale(inspectorLanguageCode);

            return CheshireLocaleResolver.ResolveCurrentLocale();
        }

        public string ResolveLanguageCode()
        {
            return ResolveLanguageCode(useInspectorLanguageOverride, languageCode);
        }

        public ScenarioTalkLine[] BuildLines()
        {
            string resolvedLanguage = ResolveLanguageCode();
            ScenarioScript script = ScenarioScript.FromJson(scenarioJson != null ? scenarioJson.text : "");
            ScenarioLocalizationTable dialogue = ScenarioLocalizationTable.FromCsv(
                dialogueLocalizationCsv != null ? dialogueLocalizationCsv.text : "",
                resolvedLanguage,
                "line_id");
            ScenarioLocalizationTable speakers = ScenarioLocalizationTable.FromCsv(
                speakerLocalizationCsv != null ? speakerLocalizationCsv.text : "",
                resolvedLanguage,
                "speaker_id");

            return ScenarioBlockResolver.BuildTalkLines(script, blockId, dialogue, speakers);
        }

        private IEnumerator PlayLines(ScenarioTalkLine[] lines)
        {
            SayDialog sayDialog = ResolveSayDialog();
            if (sayDialog == null)
            {
                Continue();
                yield break;
            }

            SayDialog.ActiveSayDialog = sayDialog;
            sayDialog.gameObject.SetActive(true);

            foreach (ScenarioTalkLine line in lines)
            {
                bool done = false;
                sayDialog.SetCharacterName(line.speakerName, Color.white);
                sayDialog.Say(line.text, true, true, true, false, false, null, () => done = true);

                while (!done)
                    yield return null;
            }

            Continue();
        }

        private SayDialog ResolveSayDialog()
        {
            if (overrideSayDialog == null)
                return SayDialog.GetSayDialog();

            if (overrideSayDialog.gameObject.scene.IsValid())
                return overrideSayDialog;

            GameObject go = Instantiate(overrideSayDialog.gameObject);
            go.SetActive(false);
            go.name = overrideSayDialog.name;
            return go.GetComponent<SayDialog>();
        }

        public override string GetSummary()
        {
            string lang = ResolveLanguageCode();
            return string.IsNullOrWhiteSpace(blockId)
                ? "Error: No scenario block id"
                : $"{blockId} ({lang})";
        }

        public override Color GetButtonColor()
        {
            return new Color32(170, 210, 185, 255);
        }
    }
}
