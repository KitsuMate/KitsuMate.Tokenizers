using System;
using System.Text.RegularExpressions;
using KitsuMate.Tokenizers; // For NormalizerConfig

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for replacing patterns in text.
    /// </summary>
    public class ReplaceNormalizer : INormalizer
    {
        private readonly NormalizerConfig _config;

        public ReplaceNormalizer(NormalizerConfig config)
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

            if (_config.Pattern == null || _config.Content == null)
                return original.ToString();

            string result = original.ToString();

            try
            {
                string patternToUse = null;

                // Determine the pattern to use and escape if it's a literal string
                if (!string.IsNullOrEmpty(_config.Pattern.Regex))
                {
                    patternToUse = _config.Pattern.Regex;
                }
                else if (!string.IsNullOrEmpty(_config.Pattern.String))
                {
                    patternToUse = Regex.Escape(_config.Pattern.String); // Escape string for literal regex match
                }

                if (patternToUse != null)
                {
                    var regex = new Regex(patternToUse, RegexOptions.Compiled);
                    result = regex.Replace(result, _config.Content);
                }
            }
            catch (Exception)
            {
                // Log error and return original text if pattern is invalid
                // Debug: Replace operation failed
                return original.ToString();
            }

            return result;
        }
    }
}
