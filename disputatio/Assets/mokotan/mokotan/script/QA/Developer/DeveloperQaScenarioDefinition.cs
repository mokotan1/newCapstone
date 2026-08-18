#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Stable session state strings returned by <c>scenario.status</c> (Task 9).
    /// </summary>
    public static class DeveloperQaScenarioStates
    {
        public const string Idle = "idle";
        public const string Running = "running";
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
    }

    /// <summary>
    /// DeveloperQa scenario JSON schema (Task 9). Unlike <c>QaScenarioDefinition</c>
    /// (interaction.pointer / state.assert), each step is a typed
    /// <see cref="DeveloperQaCommand"/> (family + name + targetId) so StudyRoom
    /// capabilities can be declared without expanding the older QA Driver schema.
    /// </summary>
    public sealed class DeveloperQaScenarioDefinition
    {
        public const int SupportedSchemaVersion = 1;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("scene")]
        public string Scene { get; set; }

        /// <summary>
        /// Room-pack identifier (design §8). Accepted as an alternative to
        /// <see cref="Scene"/> so nested <c>Rooms/**</c> packs without an explicit
        /// Unity scene still validate for listing and capability-probe smoke runs.
        /// </summary>
        [JsonProperty("roomId")]
        public string RoomId { get; set; }

        [JsonProperty("steps")]
        public List<DeveloperQaScenarioStepDefinition> Steps { get; set; }
    }

    /// <summary>One DeveloperQa command step inside a scenario JSON.</summary>
    public sealed class DeveloperQaScenarioStepDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("family")]
        public string Family { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("targetId")]
        public string TargetId { get; set; }

        [JsonProperty("parameters")]
        public Dictionary<string, string> Parameters { get; set; }
    }
}
#endif
