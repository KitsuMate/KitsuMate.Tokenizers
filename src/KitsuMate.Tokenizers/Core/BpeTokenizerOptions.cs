namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Shared BPE options for character-level and byte-level runtimes.
    /// </summary>
    public sealed class BpeTokenizerOptions
    {
        public string? UnknownToken { get; set; }

        public string? ContinuingSubwordPrefix { get; set; }

        public string? EndOfWordSuffix { get; set; }

        public bool UseByteLevel { get; set; }

        public bool AddPrefixSpace { get; set; }

        public bool UseRegex { get; set; } = true;

        public bool CleanUpTokenizationSpaces { get; set; } = true;
    }
}