using System.Threading;
using System.Threading.Tasks;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;
using AsyncTask = System.Threading.Tasks.Task;

[TestFixture]
public class SayDialogSequenceHostTests
{
    GameObject root;
    SayDialogSequenceHost host;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("SayDialogSequenceHostTests");
        host = root.AddComponent<SayDialogSequenceHost>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void WaitAsync_NonPositive_CompletesImmediately()
    {
        AsyncTask task = host.WaitAsync(0, CancellationToken.None);
        Assert.IsTrue(task.IsCompleted);
        Assert.IsFalse(task.IsFaulted);
        Assert.IsFalse(task.IsCanceled);
    }

    [Test]
    public void SayAsync_EmptyLine_CompletesImmediately()
    {
        AsyncTask task = host.SayAsync("maid", "  ", CancellationToken.None);
        Assert.IsTrue(task.IsCompleted);
        Assert.IsFalse(task.IsFaulted);
    }

    [Test]
    public void SayAsync_MissingDialog_CompletesWithoutThrow()
    {
        AsyncTask task = host.SayAsync("maid", "문이 잠겨 있다.", CancellationToken.None);
        Assert.IsTrue(task.IsCompleted);
        Assert.IsFalse(task.IsFaulted);
        Assert.IsFalse(task.IsCanceled);
    }

    [Test]
    public void WaitAsync_Cancel_CancelsTask()
    {
        var cts = new CancellationTokenSource();
        AsyncTask task = host.WaitAsync(60000, cts.Token);
        Assert.IsFalse(task.IsCompleted);

        cts.Cancel();

        Assert.IsTrue(task.IsCompleted);
        Assert.Throws<TaskCanceledException>(() => task.GetAwaiter().GetResult());
    }
}
