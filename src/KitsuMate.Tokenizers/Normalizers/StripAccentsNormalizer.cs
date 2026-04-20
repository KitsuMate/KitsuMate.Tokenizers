using System;
using KitsuMate.Tokenizers; // For TokenizerUtils

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for stripping accents from text.
    /// </summary>
    public class StripAccentsNormalizer : INormalizer
    {
        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return TokenizerUtils.StripAccents(original);
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            return TokenizerUtils.StripAccents(original.ToString());
        }
    }
}
