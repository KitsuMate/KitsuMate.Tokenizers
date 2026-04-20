using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class SplitPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;

        public SplitPreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            var pattern = _config.Pattern;
            if (string.IsNullOrEmpty(pattern))
                return TokenizerUtils.SplitByWhitespace(text);
            
            try
            {
                var regex = TokenizerUtils.CreateRegexFromPattern(pattern);
                var behavior = _config.Behavior?.ToLowerInvariant() ?? "removed";
                var invert = _config.Invert ?? false;
                
                return TokenizerUtils.SplitByRegex(text, regex, behavior, invert);
            }
            catch (Exception)
            {
                // Debug: Split pre-tokenization failed
                return TokenizerUtils.SplitByWhitespace(text);
            }
        }
    }
}
