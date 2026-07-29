#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Shared empty read-only maps so callers cannot cast-and-mutate defaults
    /// (matches Godlotto.QA.Core empty-parameter pattern).
    /// </summary>
    internal static class DeveloperQaMaps
    {
        internal static readonly IReadOnlyDictionary<string, string> Empty =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        internal static IReadOnlyDictionary<string, string> AsReadOnly(
            IReadOnlyDictionary<string, string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Empty;
            }

            if (source is ReadOnlyDictionary<string, string>)
            {
                return source;
            }

            return new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(source));
        }

        internal static IReadOnlyDictionary<string, string> From(
            Dictionary<string, string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Empty;
            }

            return new ReadOnlyDictionary<string, string>(source);
        }
    }
}
#endif
