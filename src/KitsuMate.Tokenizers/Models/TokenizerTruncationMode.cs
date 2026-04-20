namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Truncation policy for a tokenizer encode call.
    /// </summary>
    public enum TokenizerTruncationMode
    {
        None,
        OnlyFirst,
        OnlySecond,
        LongestFirst
    }
}