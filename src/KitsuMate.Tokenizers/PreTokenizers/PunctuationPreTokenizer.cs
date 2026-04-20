using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class PunctuationPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;
        // Rust's `is_punc` splits on each individual punctuation character.
        // So, the regex should match a single punctuation character, not a sequence.
        private static readonly Regex PunctuationRegex = new Regex(@"\p{P}", RegexOptions.Compiled);

        public PunctuationPreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            var behavior = _config.Behavior?.ToLowerInvariant() ?? "isolated";
            return TokenizerUtils.SplitByRegex(text, PunctuationRegex, behavior);
        }
    }
}
