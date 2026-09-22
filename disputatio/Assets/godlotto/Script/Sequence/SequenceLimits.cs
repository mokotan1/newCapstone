namespace Godlotto.Sequence
{
    public static class SequenceLimits
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxBlockCount = 128;
        public const int MaxCommandsPerDocument = 512;
        public const int MaxIfDepth = 16;
        public const string InputLockReason = "sequence";
    }
}
