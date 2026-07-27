#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// In-memory catalog of developer-QA capabilities. Version bumps on each successful Register.
    /// </summary>
    public sealed class DeveloperQaCapabilityRegistry
    {
        private readonly Dictionary<string, DeveloperQaCapability> _byId =
            new Dictionary<string, DeveloperQaCapability>(StringComparer.Ordinal);

        private int _versionNumber;

        public string Version => _versionNumber.ToString();

        public void Register(DeveloperQaCapability capability)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            if (string.IsNullOrWhiteSpace(capability.Id))
            {
                throw new ArgumentException("Capability id is required.", nameof(capability));
            }

            _byId[capability.Id] = capability;
            _versionNumber++;
        }

        public IReadOnlyCollection<DeveloperQaCapability> List()
        {
            return _byId.Values.ToList();
        }

        public bool TryGet(string id, out DeveloperQaCapability capability)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                capability = null;
                return false;
            }

            return _byId.TryGetValue(id, out capability);
        }

        public string FormatCurrentCapabilityIds()
        {
            if (_byId.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", _byId.Keys.OrderBy(k => k, StringComparer.Ordinal));
        }
    }
}
#endif
