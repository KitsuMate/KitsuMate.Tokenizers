namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Options for the native WordPiece tokenizer implementation.
    /// </summary>
    public sealed class WordPieceTokenizerOptions
    {
        public string UnknownToken { get; set; } = "[UNK]";

        public string ContinuingSubwordPrefix { get; set; } = "##";

        public int MaxInputCharsPerWord { get; set; } = 100;

        public bool LowerCaseBeforeTokenization { get; set; } = true;

        public bool ApplyBasicTokenization { get; set; } = true;

        public bool CleanUpTokenizationSpaces { get; set; } = true;

        public string ClassificationToken { get; set; } = "[CLS]";

        public string SeparatorToken { get; set; } = "[SEP]";

        public string PaddingToken { get; set; } = "[PAD]";

        public string MaskToken { get; set; } = "[MASK]";
    }
}