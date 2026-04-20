using System;
using System.Text;
using KitsuMate.Tokenizers; // For NormalizerConfig and TokenizerUtils

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Default normalizer implementation.
    /// </summary>
    public class DefaultNormalizer : INormalizer
    {
        private readonly NormalizerConfig _config;

        public DefaultNormalizer()
        {
            _config = null; // No specific config for default
        }

        public DefaultNormalizer(NormalizerConfig config)
        {
            _config = config;
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

            if (_config == null)
                return result;

            // Apply individual settings if specified
            if (_config.Lowercase == true)
            {
                result = result.ToLowerInvariant();
            }

            if (_config.StripAccents == true)
            {
                result = TokenizerUtils.StripAccents(result);
            }

            if (_config.HandleChineseChars == true)
            {
                result = TokenizerUtils.HandleChineseCharacters(result);
            }

            return result;
        }
    }
}
