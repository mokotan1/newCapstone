using System;
using NUnit.Framework;
using UnityCliConnector;

public class UnityCliHttpListenPortPolicyTests
{
    [Test]
    public void PortWindow_ExtendsPastTheTenPortLeakRange()
    {
        Assert.AreEqual(8090, HttpListenPortPolicy.DefaultPort);
        Assert.That(HttpListenPortPolicy.MaxAttempts, Is.GreaterThan(10));
        Assert.AreEqual(8090, HttpListenPortPolicy.PortForAttempt(0));
        Assert.AreEqual(8099, HttpListenPortPolicy.PortForAttempt(9));
        Assert.AreEqual(
            HttpListenPortPolicy.DefaultPort + HttpListenPortPolicy.MaxAttempts - 1,
            HttpListenPortPolicy.PortForAttempt(HttpListenPortPolicy.MaxAttempts - 1));
    }

    [Test]
    public void PortForAttempt_RejectsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HttpListenPortPolicy.PortForAttempt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HttpListenPortPolicy.PortForAttempt(HttpListenPortPolicy.MaxAttempts));
    }
}
