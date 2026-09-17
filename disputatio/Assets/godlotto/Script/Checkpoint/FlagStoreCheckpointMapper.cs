using System;
using System.Collections.Generic;
using Godlotto.Sequence;

public static class FlagStoreCheckpointMapper
{
    public static void Capture(CheckpointSaveData data, FlagStore flags)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (flags == null)
            throw new ArgumentNullException(nameof(flags));

        FlagSnapshot snapshot = flags.Export();
        data.sequenceBooleans = FilterBools(snapshot.Bools);
        data.sequenceIntegers = FilterInts(snapshot.Ints);
        data.sequenceStrings = FilterStrings(snapshot.Strings);
    }

    public static void Restore(CheckpointSaveData data, FlagStore flags)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (flags == null)
            throw new ArgumentNullException(nameof(flags));

        flags.Import(new FlagSnapshot
        {
            Bools = ToFlagBools(data.sequenceBooleans),
            Ints = ToFlagInts(data.sequenceIntegers),
            Strings = ToFlagStrings(data.sequenceStrings)
        });
    }

    static BoolCheckpointEntry[] FilterBools(FlagBoolEntry[] entries)
    {
        if (entries == null)
            return new BoolCheckpointEntry[0];

        var kept = new List<BoolCheckpointEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            FlagBoolEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.Key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.Key))
                kept.Add(new BoolCheckpointEntry(entry.Key, entry.Value));
        }

        return kept.ToArray();
    }

    static IntCheckpointEntry[] FilterInts(FlagIntEntry[] entries)
    {
        if (entries == null)
            return new IntCheckpointEntry[0];

        var kept = new List<IntCheckpointEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            FlagIntEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.Key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.Key))
                kept.Add(new IntCheckpointEntry(entry.Key, entry.Value));
        }

        return kept.ToArray();
    }

    static StringCheckpointEntry[] FilterStrings(FlagStringEntry[] entries)
    {
        if (entries == null)
            return new StringCheckpointEntry[0];

        var kept = new List<StringCheckpointEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            FlagStringEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.Key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.Key))
                kept.Add(new StringCheckpointEntry(entry.Key, entry.Value));
        }

        return kept.ToArray();
    }

    static FlagBoolEntry[] ToFlagBools(BoolCheckpointEntry[] entries)
    {
        if (entries == null)
            return Array.Empty<FlagBoolEntry>();

        var kept = new List<FlagBoolEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            BoolCheckpointEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.key))
                kept.Add(new FlagBoolEntry(entry.key, entry.value));
        }

        return kept.ToArray();
    }

    static FlagIntEntry[] ToFlagInts(IntCheckpointEntry[] entries)
    {
        if (entries == null)
            return Array.Empty<FlagIntEntry>();

        var kept = new List<FlagIntEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            IntCheckpointEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.key))
                kept.Add(new FlagIntEntry(entry.key, entry.value));
        }

        return kept.ToArray();
    }

    static FlagStringEntry[] ToFlagStrings(StringCheckpointEntry[] entries)
    {
        if (entries == null)
            return Array.Empty<FlagStringEntry>();

        var kept = new List<FlagStringEntry>();
        for (int i = 0; i < entries.Length; i++)
        {
            StringCheckpointEntry entry = entries[i];
            if (!SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(entry.key)
                && ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(entry.key))
                kept.Add(new FlagStringEntry(entry.key, entry.value));
        }

        return kept.ToArray();
    }
}
