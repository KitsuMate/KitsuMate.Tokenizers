namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Identifies the algorithm family behind a tokenizer implementation.
    /// </summary>
    public enum TokenizerBackendType
    {
        Unknown = 0,
        WordPiece = 1,
        Bpe = 2,
        Tiktoken = 3,
        SentencePieceBpe = 4,
        SentencePieceUnigram = 5,
        Stub = 6,
    }
}