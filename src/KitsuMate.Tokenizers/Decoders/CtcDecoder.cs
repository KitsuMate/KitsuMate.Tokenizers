using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// CTC decoder implementation.
    /// </summary>
    public class CtcDecoder : IDecoder
    {
        private readonly string _padToken;
        private readonly string _wordDelimiterToken;
        private readonly bool _cleanup;

        // Cached regex patterns for performance
        private static readonly Regex SpaceBeforePunctuationRegex = new Regex(@"\s+([.!?,:;])", RegexOptions.Compiled);
        private static readonly Regex EnglishContractionRegex = new Regex(@"\s+('(t|re|ve|ll|d|s|m))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public CtcDecoder(string padToken, string wordDelimiterToken, bool cleanup)
        {
            _padToken = padToken;
            _wordDelimiterToken = wordDelimiterToken;
            _cleanup = cleanup;
        }

        public string Decode(IEnumerable<string> tokens)
        {
            var result = new StringBuilder();
            string previousToken = null;

            foreach (var token in tokens)
            {
                // Skip pad tokens
                if (token == _padToken)
                    continue;

                // Skip repeated tokens (CTC behavior)
                if (token == previousToken)
                    continue;

                if (token == _wordDelimiterToken)
                {
                    result.Append(" ");
                }
                else
                {
                    result.Append(token);
                }

                previousToken = token;
            }

            var decoded = result.ToString();

            if (_cleanup)
            {
                decoded = ApplyCleanup(decoded);
            }

            return decoded;
        }

        private string ApplyCleanup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove spaces before punctuation
            text = SpaceBeforePunctuationRegex.Replace(text, "$1");

            // Fix English contractions
            text = EnglishContractionRegex.Replace(text, "$1");

            // Trim extra whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
    }
}
