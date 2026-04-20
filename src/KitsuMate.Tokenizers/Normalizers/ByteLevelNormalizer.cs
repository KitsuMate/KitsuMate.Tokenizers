using System;
using System.Text;
using KitsuMate.Tokenizers; // For TokenizerUtils

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Applies ByteLevel normalization by converting each byte of the UTF-8 representation
    /// of the input string into a specific Unicode character.
    /// </summary>
    public class ByteLevelNormalizer : INormalizer
    {
        public ByteLevelNormalizer()
        {
            // No constructor parameters needed as it uses TokenizerUtils
        }

        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return TokenizerUtils.ApplyByteLevelMapping(original);
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            return TokenizerUtils.ApplyByteLevelMapping(original.ToString());
        }
    }
}
