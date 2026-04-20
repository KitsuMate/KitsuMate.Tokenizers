using System;
using KitsuMate.Tokenizers; // For NormalizerConfig

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for stripping whitespace from text.
    /// </summary>
    public class StripNormalizer : INormalizer
    {
        private readonly NormalizerConfig _config;

        public StripNormalizer(NormalizerConfig config)
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

            string result = original.ToString();

            // Strip from left if enabled (default true)
            if (_config.Left != false)
            {
                result = result.TrimStart();
            }

            // Strip from right if enabled (default true)
            if (_config.Right != false)
            {
                result = result.TrimEnd();
            }

            return result;
        }
    }
}
