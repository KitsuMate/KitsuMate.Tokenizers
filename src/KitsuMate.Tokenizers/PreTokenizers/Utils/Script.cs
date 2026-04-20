namespace KitsuMate.Tokenizers.PreTokenizers.Utils
{
    internal enum Script
    {
        Any,
        Han,
        Hiragana,
        Katakana,
        Latin,
        Common, // For general punctuation, symbols, etc.
        Unknown // Fallback for anything not explicitly handled
    }
}
