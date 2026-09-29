using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequenceSessionTests
{
    static SequenceDocument Doc(params SequenceBlock[] blocks)
    {
        return new SequenceDocument
        {
            schemaVersion = SequenceLimits.CurrentSchemaVersion,
            blocks = blocks
        };
    }

    static SequenceBlock Block(string id, params SequenceOp[] ops)
    {
        return new SequenceBlock { id = id, commands = ops };
    }

    [Test]
    public void Wait_CallsHost_DoesNotSetFlags()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        SequenceDocument doc = Doc(
            Block("start", new SequenceOp { command = "wait", int_value = 500 }));

        session.Play(doc, "start");

        CollectionAssert.AreEqual(new[] { "wait:500" }, host.Events);
        Assert.IsFalse(flags.Has("wait"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(1, input.BlockCount);
        Assert.AreEqual(1, input.UnblockCount);
    }

    [Test]
    public void Say_CallsHostWithSpeakerAndLine()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var session = new SequenceSession(flags, host, new FakeInputLock());
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "say", key = "maid", string_value = "문이 잠겨 있다." }));

        session.Play(doc, "start");

        CollectionAssert.AreEqual(new[] { "say:maid:문이 잠겨 있다." }, host.Events);
        Assert.IsFalse(flags.Has("maid"));
    }

    [Test]
    public void WaitThenSet_AppliesFlagAfterHostWaitReturns()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var session = new SequenceSession(flags, host, new FakeInputLock());
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "wait", int_value = 250 },
                new SequenceOp { command = "set_bool", key = "ready", bool_value = true }));

        session.Play(doc, "start");

        CollectionAssert.AreEqual(new[] { "wait:250" }, host.Events);
        Assert.IsTrue(flags.GetBool("ready"));
    }

    [Test]
    public void NegativeWait_DoesNotLockOrMutate()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_bool", key = "touched", bool_value = true },
                new SequenceOp { command = "wait", int_value = -1 }));

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => session.Play(doc, "start"));
        Assert.AreEqual("invalid_document", ex.Code);
        Assert.IsFalse(flags.Has("touched"));
        Assert.AreEqual(0, input.BlockCount);
        CollectionAssert.IsEmpty(host.Events);
    }

    [Test]
    public void EmptySay_DoesNotLock()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        SequenceDocument doc = Doc(
            Block("start", new SequenceOp { command = "say", string_value = "  " }));

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => session.Play(doc, "start"));
        Assert.AreEqual("invalid_document", ex.Code);
        Assert.AreEqual(0, input.BlockCount);
    }

    [Test]
    public void RuntimeError_UnlocksInput()
    {
        var flags = new FlagStore();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, new FakeHost(), input);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_bool", key = "lit", bool_value = true },
                new SequenceOp { command = "if_bool", key = "missing", then_block = "end", else_block = "end" }),
            Block("end"));

        Assert.Throws<SequencePlayException>(() => session.Play(doc, "start"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(input.BlockCount, input.UnblockCount);
        Assert.IsTrue(input.BlockCount > 0);
    }

    [Test]
    public void WaitWithoutHost_FailsBeforeMutation()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_bool", key = "touched", bool_value = true },
                new SequenceOp { command = "wait", int_value = 10 }));

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
        Assert.AreEqual("async_required", ex.Code);
        Assert.IsFalse(flags.Has("touched"));
    }

    [Test]
    public void PlayAsync_Wait_DoesNotApplyLaterFlagsUntilHostCompletes()
    {
        var flags = new FlagStore();
        var host = new GatedHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "wait", int_value = 250 },
                new SequenceOp { command = "set_bool", key = "ready", bool_value = true }));

        Task play = session.PlayAsync(doc, "start");

        Assert.IsFalse(play.IsCompleted);
        Assert.IsFalse(flags.Has("ready"));
        Assert.IsTrue(input.IsBlocked);
        CollectionAssert.AreEqual(new[] { "wait:250" }, host.Events);

        host.CompletePending();

        Assert.IsTrue(play.IsCompleted);
        Assert.IsTrue(flags.GetBool("ready"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(input.BlockCount, input.UnblockCount);
    }

    [Test]
    public void PlayAsync_Say_DoesNotRunNextCommandUntilHostCompletes()
    {
        var flags = new FlagStore();
        var host = new GatedHost();
        var session = new SequenceSession(flags, host, new FakeInputLock());
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "say", key = "maid", string_value = "문이 잠겨 있다." },
                new SequenceOp { command = "set_bool", key = "after_say", bool_value = true }));

        Task play = session.PlayAsync(doc, "start");

        Assert.IsFalse(play.IsCompleted);
        Assert.IsFalse(flags.Has("after_say"));
        CollectionAssert.AreEqual(new[] { "say:maid:문이 잠겨 있다." }, host.Events);

        host.CompletePending();

        Assert.IsTrue(play.IsCompleted);
        Assert.IsTrue(flags.GetBool("after_say"));
    }

    [Test]
    public void Play_SyncIncomplete_CancelsAndUnlocksInput()
    {
        var flags = new FlagStore();
        var host = new GatedHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "wait", int_value = 100 },
                new SequenceOp { command = "set_bool", key = "ready", bool_value = true }));

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => session.Play(doc, "start"));
        Assert.AreEqual("async_required", ex.Code);
        Assert.IsFalse(flags.Has("ready"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(input.BlockCount, input.UnblockCount);
    }

    [Test]
    public void PlayAsync_Cancel_UnlocksInputWithoutLaterFlags()
    {
        var flags = new FlagStore();
        var host = new GatedHost();
        var input = new FakeInputLock();
        var session = new SequenceSession(flags, host, input);
        var cts = new CancellationTokenSource();
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "wait", int_value = 100 },
                new SequenceOp { command = "set_bool", key = "ready", bool_value = true }));

        Task play = session.PlayAsync(doc, "start", cts.Token);
        Assert.IsTrue(input.IsBlocked);

        cts.Cancel();
        host.CompletePending();

        Assert.IsTrue(play.IsCompleted);
        Assert.Throws<System.OperationCanceledException>(() => play.GetAwaiter().GetResult());
        Assert.IsFalse(flags.Has("ready"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(input.BlockCount, input.UnblockCount);
    }

    sealed class FakeHost : ISequenceHost
    {
        public readonly List<string> Events = new List<string>();

        public Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
        {
            Events.Add("wait:" + milliseconds);
            return Task.CompletedTask;
        }

        public Task SayAsync(string speaker, string line, CancellationToken cancellationToken)
        {
            Events.Add("say:" + speaker + ":" + line);
            return Task.CompletedTask;
        }
    }

    sealed class GatedHost : ISequenceHost
    {
        public readonly List<string> Events = new List<string>();
        TaskCompletionSource<bool> pending;

        public Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
        {
            Events.Add("wait:" + milliseconds);
            return Gate(cancellationToken);
        }

        public Task SayAsync(string speaker, string line, CancellationToken cancellationToken)
        {
            Events.Add("say:" + speaker + ":" + line);
            return Gate(cancellationToken);
        }

        public void CompletePending()
        {
            Assert.IsNotNull(pending);
            pending.TrySetResult(true);
        }

        Task Gate(CancellationToken cancellationToken)
        {
            pending = new TaskCompletionSource<bool>();
            return pending.Task;
        }
    }

    sealed class FakeInputLock : ISequenceInputLock
    {
        public int BlockCount;
        public int UnblockCount;
        public bool IsBlocked
        {
            get { return BlockCount > UnblockCount; }
        }

        public void Block(string reason)
        {
            BlockCount++;
        }

        public void Unblock(string reason)
        {
            UnblockCount++;
        }
    }
}
