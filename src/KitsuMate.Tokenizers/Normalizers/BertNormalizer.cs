using System;
using KitsuMate.Tokenizers; // For NormalizerConfig and TokenizerUtils

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// BERT-style normalizer implementation.
    /// </summary>
    public class BertNormalizer : INormalizer
    {
        private readonly NormalizerConfig _config;

        public BertNormalizer(NormalizerConfig config)
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

            // Clean text if enabled (remove control characters and normalize whitespace)
            if (_config.CleanText == true)
            {
                result = TokenizerUtils.CleanText(result);
            }

            // Handle Chinese characters if enabled
            if (_config.HandleChineseChars == true)
            {
                result = TokenizerUtils.HandleChineseCharacters(result);
            }

            // Strip accents if enabled, or determine by lowercase setting if not specified
            if (_config.StripAccents == true || (_config.StripAccents == null && _config.Lowercase == true))
            {
                result = TokenizerUtils.StripAccents(result);
            }

            // Apply lowercasing if enabled
            if (_config.Lowercase == true)
            {
                result = result.ToLowerInvariant();
            }

            return result;
        }
    }
}
