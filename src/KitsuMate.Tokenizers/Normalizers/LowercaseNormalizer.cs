using System;

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for converting text to lowercase.
    /// </summary>
    public class LowercaseNormalizer : INormalizer
    {
        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return original.ToLowerInvariant();
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            return original.ToString().ToLowerInvariant();
        }
    }
}
