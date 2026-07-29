#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// In-memory catalog of developer-QA capabilities and optional handlers.
    /// Version bumps on each successful Register / RegisterAsync.
    /// </summary>
    public sealed class DeveloperQaCapabilityRegistry
    {
        private readonly Dictionary<string, DeveloperQaCapability> _byId =
            new Dictionary<string, DeveloperQaCapability>(StringComparer.Ordinal);

        private readonly Dictionary<string, DeveloperQaCapabilityHandler> _handlers =
            new Dictionary<string, DeveloperQaCapabilityHandler>(StringComparer.Ordinal);

        private readonly Dictionary<string, DeveloperQaAsyncCapabilityHandler> _asyncHandlers =
            new Dictionary<string, DeveloperQaAsyncCapabilityHandler>(StringComparer.Ordinal);

        private int _versionNumber;

        public string Version => _versionNumber.ToString();

        public void Register(
            DeveloperQaCapability capability,
            DeveloperQaCapabilityHandler handler = null)
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
            _asyncHandlers.Remove(capability.Id);
            if (handler != null)
            {
                _handlers[capability.Id] = handler;
            }
            else
            {
                _handlers.Remove(capability.Id);
            }

            _versionNumber++;
        }

        /// <summary>
        /// Registers an async handler (preferred when the step must yield Unity frames).
        /// Clears any sync handler for the same id.
        /// </summary>
        public void RegisterAsync(
            DeveloperQaCapability capability,
            DeveloperQaAsyncCapabilityHandler handler)
        {
            if (capability == null)
            {
                throw new ArgumentNullException(nameof(capability));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (string.IsNullOrWhiteSpace(capability.Id))
            {
                throw new ArgumentException("Capability id is required.", nameof(capability));
            }

            _byId[capability.Id] = capability;
            _handlers.Remove(capability.Id);
            _asyncHandlers[capability.Id] = handler;
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

        public bool TryGetHandler(string id, out DeveloperQaCapabilityHandler handler)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                handler = null;
                return false;
            }

            return _handlers.TryGetValue(id, out handler);
        }

        public bool TryGetAsyncHandler(string id, out DeveloperQaAsyncCapabilityHandler handler)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                handler = null;
                return false;
            }

            return _asyncHandlers.TryGetValue(id, out handler);
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
