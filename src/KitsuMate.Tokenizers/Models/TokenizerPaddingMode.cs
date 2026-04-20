namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Padding policy for a tokenizer encode call.
    /// </summary>
    public enum TokenizerPaddingMode
    {
        None,
        Longest,
        MaxLength
    }
}