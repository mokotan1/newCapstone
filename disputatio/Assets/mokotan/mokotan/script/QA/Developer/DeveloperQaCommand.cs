#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaCommand
    {
        public string Id { get; }
        public string Family { get; }
        public string Name { get; }
        public string TargetId { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        private DeveloperQaCommand(
            string id,
            string family,
            string name,
            string targetId,
            IReadOnlyDictionary<string, string> parameters)
        {
            Id = id;
            Family = family;
            Name = name;
            TargetId = targetId;
            Parameters = parameters ?? new Dictionary<string, string>();
        }

        public static DeveloperQaCommand Create(
            string id,
            string family,
            string name,
            string targetId = null,
            IReadOnlyDictionary<string, string> parameters = null)
        {
            return new DeveloperQaCommand(id, family, name, targetId, parameters);
        }
    }
}
#endif
