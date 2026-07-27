#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.Evidence;
using Godlotto.QA.Profile;
using Godlotto.QA.SceneAdapters;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;

namespace Godlotto.QA.EditorCli
{
    /// <summary>
    /// Editor CLI bridge for <see cref="IDeveloperQaService"/> (Task 8).
    /// Parses the same family/name/target/parameters payloads as
    /// <see cref="DeveloperQaPanelBridge"/> and executes through the shared
    /// <see cref="DeveloperQaServiceFactory"/>.
    /// </summary>
    public static class DeveloperQaCliBridge
    {
        private static IDeveloperQaService service;
        private static bool disableDefaultServiceCreationForTests;

        /// <summary>Test-only: when true, default production service is not auto-created.</summary>
        public static bool DisableDefaultServiceCreationForTests
        {
            get { return disableDefaultServiceCreationForTests; }
            set { disableDefaultServiceCreationForTests = value; }
        }

        public static void Configure(IDeveloperQaService developerQaService)
        {
            service = developerQaService;
        }

        public static void ResetForTests()
        {
            service = null;
            disableDefaultServiceCreationForTests = false;
        }

        /// <summary>
        /// Production Editor service: StudyRoom capabilities + optional profile +
        /// <see cref="EditorQaEvidenceRecorder"/> rooted at <c>docs/qa/runs</c>
        /// (or <paramref name="runsRootDirectoryOverride"/> for tests).
        /// </summary>
        public static IDeveloperQaService CreateProductionService(
            IQaProfileService profileService = null,
            string runsRootDirectoryOverride = null)
        {
            IQaEvidenceRecorder recorder = new EditorQaEvidenceRecorder(
                runsRootDirectoryOverride: runsRootDirectoryOverride);
            return DeveloperQaServiceFactory.Create(profileService, recorder);
        }

        /// <summary>
        /// Pure JSON → <see cref="DeveloperQaCommand"/> parse (no side effects).
        /// Accepts <c>command_id</c>, <c>family</c>, <c>name</c>, <c>target</c>/<c>target_id</c>,
        /// and optional <c>parameters</c> object of string values.
        /// </summary>
        public static DeveloperQaCommand BuildCommandForCli(JObject @params)
        {
            if (@params == null)
            {
                @params = new JObject();
            }

            var p = new ToolParams(@params);
            string commandId = QaCliToolSupport.ResolveCommandId(p);
            string family = p.Get("family") ?? string.Empty;
            string name = p.Get("name") ?? string.Empty;
            string target = p.Get("target");
            if (string.IsNullOrWhiteSpace(target))
            {
                target = p.Get("target_id");
            }

            IReadOnlyDictionary<string, string> parameters = ParseParameters(@params["parameters"]);
            return DeveloperQaCommand.Create(commandId, family, name, target, parameters);
        }

        public static Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetService(out IDeveloperQaService resolved))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "DeveloperQaService unavailable."));
            }

            return resolved.ExecuteAsync(command, cancellationToken);
        }

        internal static object ToCliPayload(DeveloperQaCommand command, DeveloperQaResult result)
        {
            return new
            {
                commandId = command != null ? command.Id : null,
                code = result != null ? result.Code.ToString() : null,
                message = result != null ? result.Message : null,
                missingCapabilityId = result != null ? result.MissingCapabilityId : null,
                checkpointId = result != null ? result.CheckpointId : null,
                data = result != null ? result.Data : null
            };
        }

        private static bool TryGetService(out IDeveloperQaService resolved)
        {
            if (service != null)
            {
                resolved = service;
                return true;
            }

            if (disableDefaultServiceCreationForTests)
            {
                resolved = null;
                return false;
            }

            service = CreateProductionService();
            resolved = service;
            return true;
        }

        private static IReadOnlyDictionary<string, string> ParseParameters(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            JObject obj = token as JObject;
            if (obj == null)
            {
                return null;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JProperty property in obj.Properties())
            {
                if (property.Value == null || property.Value.Type == JTokenType.Null)
                {
                    map[property.Name] = string.Empty;
                    continue;
                }

                map[property.Name] = property.Value.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : property.Value.ToString(Newtonsoft.Json.Formatting.None);
            }

            return map;
        }
    }

    /// <summary>
    /// <c>qa_dev_exec</c>: execute one DeveloperQa command through the shared CLI bridge.
    /// Returns CLI JSON with code, message, missingCapabilityId, checkpointId, and data.
    /// </summary>
    [UnityCliTool(Name = "qa_dev_exec", Group = "qa",
        Description = "Execute a DeveloperQaService command (family/name/target/parameters) with the same payloads as the StudyRoom developer panel bridge.")]
    public static class QaDevExec
    {
        public class Parameters
        {
            [ToolParameter("Correlation id stamped on DeveloperQaCommand. Generated if omitted.")]
            public string CommandId { get; set; }

            [ToolParameter("Command family (e.g. interaction, state, evidence, capability).", Required = true)]
            public string Family { get; set; }

            [ToolParameter("Command name (e.g. invoke, capture, list).", Required = true)]
            public string Name { get; set; }

            [ToolParameter("Capability / target id (e.g. studyroom.mirror.grant-bookmark).")]
            public string Target { get; set; }

            [ToolParameter("Optional string-keyed parameters object (e.g. {\"run_id\":\"...\"}).")]
            public string ParametersJson { get; set; }
        }

        public static async Task<object> HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                return new ErrorResponse("Parameters cannot be null.");
            }

            var p = new ToolParams(@params);
            Result<string> familyResult = p.GetRequired("family");
            if (!familyResult.IsSuccess)
            {
                return new ErrorResponse(familyResult.ErrorMessage);
            }

            Result<string> nameResult = p.GetRequired("name");
            if (!nameResult.IsSuccess)
            {
                return new ErrorResponse(nameResult.ErrorMessage);
            }

            DeveloperQaCommand command = DeveloperQaCliBridge.BuildCommandForCli(@params);
            DeveloperQaResult result = await DeveloperQaCliBridge
                .ExecuteAsync(command, CancellationToken.None)
                .ConfigureAwait(false);

            object payload = DeveloperQaCliBridge.ToCliPayload(command, result);
            return result.Code == DeveloperQaResultCode.Ok
                ? new SuccessResponse(result.Message, payload)
                : new ErrorResponse(result.Message, payload);
        }
    }

    /// <summary>
    /// On Editor load, wires the panel bridge to the same production DeveloperQaService
    /// the CLI uses (StudyRoom caps + <c>docs/qa/runs</c> evidence recorder).
    /// </summary>
    [InitializeOnLoad]
    internal static class DeveloperQaEditorServiceInstaller
    {
        static DeveloperQaEditorServiceInstaller()
        {
            IDeveloperQaService production = DeveloperQaCliBridge.CreateProductionService(
                profileService: new QaProfileService(QaFileProfileMarkerStore.CreateDefault()));
            DeveloperQaCliBridge.Configure(production);
            DeveloperQaPanelBridge.Configure(production);
        }
    }
}
#endif
