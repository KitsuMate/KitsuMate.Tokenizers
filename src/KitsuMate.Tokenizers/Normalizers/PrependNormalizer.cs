using System;
using KitsuMate.Tokenizers; // For NormalizerConfig

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for prepending a string to the text.
    /// </summary>
    public class PrependNormalizer : INormalizer
    {
        private readonly NormalizerConfig _config;

        public PrependNormalizer(NormalizerConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return Normalize(original.AsSpan()).ToString();
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            if (!string.IsNullOrEmpty(_config.PrependString))
            {
                return _config.PrependString + original.ToString();
            }
            return original.ToString();
        }
    }
}
