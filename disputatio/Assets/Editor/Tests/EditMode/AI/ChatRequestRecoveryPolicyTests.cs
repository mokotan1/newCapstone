using NUnit.Framework;
using UnityEngine.Networking;

[TestFixture]
public class ChatRequestRecoveryPolicyTests
{
    [TestCase(UnityWebRequest.Result.ConnectionError, 0, 0, true)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 503, 0, true)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 500, 0, true)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 408, 0, true)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 429, 0, true)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 400, 0, false)]
    [TestCase(UnityWebRequest.Result.Success, 200, 0, false)]
    [TestCase(UnityWebRequest.Result.ConnectionError, 0, 1, false)]
    [TestCase(UnityWebRequest.Result.ProtocolError, 503, 1, false)]
    public void ShouldRetry_OnlyRetriesOneTransientFailure(
        UnityWebRequest.Result result,
        long code,
        int attempt,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            ChatRequestRecoveryPolicy.ShouldRetry(result, code, attempt));
    }

    [Test]
    public void ShouldRetry_TreatsConnectionErrorAsRetryable_IncludingTimeouts()
    {
        // Unity commonly reports request timeouts as ConnectionError.
        Assert.IsTrue(
            ChatRequestRecoveryPolicy.ShouldRetry(
                UnityWebRequest.Result.ConnectionError,
                responseCode: 0,
                attempt: 0));
    }
}
