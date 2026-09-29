using System;

namespace UnityCliConnector
{
    /// <summary>
    /// Listen port window for the CLI HTTP bridge.
    /// Domain reloads can leak HttpListener prefixes on Windows, so the window must
    /// be wider than the 10 ports the upstream connector tries.
    /// </summary>
    public static class HttpListenPortPolicy
    {
        // Temporary shift: Windows left zombie LISTENING on 8090-8153 after PlayMode hang.
        // CLI discovers the live port via Heartbeat, so the window can move safely.
        public const int DefaultPort = 18290;
        public const int MaxAttempts = 64;

        public static int LastPort => DefaultPort + MaxAttempts - 1;

        public static int PortForAttempt(int attempt)
        {
            if (attempt < 0 || attempt >= MaxAttempts)
                throw new ArgumentOutOfRangeException(nameof(attempt));
            return DefaultPort + attempt;
        }
    }
}
