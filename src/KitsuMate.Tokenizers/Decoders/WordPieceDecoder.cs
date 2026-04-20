using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// WordPiece decoder implementation.
    /// </summary>
    public class WordPieceDecoder : IDecoder
    {
        private readonly string _prefix;
        private readonly bool _cleanup;

        // Cached regex patterns for performance
        // Note: Python's transformers library only removes spaces before .!?, (and contractions with ')
        // but NOT before :;| - those keep their spaces. This matches HuggingFace tokenizers behavior.
        private static readonly Regex SpaceBeforePunctuationRegex = new Regex(@"\s+([.!?,])", RegexOptions.Compiled);
        // Match contractions: handles both "don't" and "don ' t" patterns
        private static readonly Regex EnglishContractionRegex = new Regex(@"\s+'\s*(t|re|ve|ll|d|s|m)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public WordPieceDecoder(string prefix, bool cleanup)
        {
            _prefix = prefix;
            _cleanup = cleanup;
        }

        public string Decode(IEnumerable<string> tokens)
        {
            var result = new StringBuilder();

            foreach (var token in tokens)
            {
                if (token.StartsWith(_prefix))
                {
                    // Subword - remove prefix and append without space
                    result.Append(token.Substring(_prefix.Length));
                }
                else
                {
                    // Beginning of word - add space before (except for first token)
                    if (result.Length > 0)
                        result.Append(" ");
                    result.Append(token);
                }
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

            // Fix English contractions - replace " ' t" with "'t"
            text = EnglishContractionRegex.Replace(text, "'$1");

            // Trim extra whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text;
        }
    }
}
