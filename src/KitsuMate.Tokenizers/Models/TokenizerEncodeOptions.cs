namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Tokenizer-owned per-call encode options.
    /// </summary>
    public sealed class TokenizerEncodeOptions
    {
        public bool AddSpecialTokens { get; set; } = true;

        public int? MaxLength { get; set; }

        public TokenizerTruncationMode Truncation { get; set; } = TokenizerTruncationMode.None;

        public TokenizerSide? TruncationSide { get; set; }

        public TokenizerPaddingMode Padding { get; set; } = TokenizerPaddingMode.None;

        public TokenizerSide? PaddingSide { get; set; }

        public bool ReturnAttentionMask { get; set; } = true;

        public bool ReturnTokenTypeIds { get; set; } = true;
    }
}