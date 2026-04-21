namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Controls how tokenizer loading behaves when multiple local artifact variants are available.
    /// </summary>
    public sealed class TokenizerLoadOptions
    {
        /// <summary>
        /// When enabled, a failing or unsupported tokenizer.json can fall back to sibling artifacts such as vocab files or SentencePiece models.
        /// When disabled, the original tokenizer.json error is thrown immediately.
        /// </summary>
        public bool FallbackToOtherVariants { get; set; } = true;
    }
}