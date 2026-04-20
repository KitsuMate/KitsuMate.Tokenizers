using System.Collections.Generic;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class CharDelimiterSplitPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;

        public CharDelimiterSplitPreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            if (string.IsNullOrEmpty(_config.Delimiter))
                return TokenizerUtils.SplitByWhitespace(text);
            
            // Rust implementation uses SplitDelimiterBehavior::Removed
            return TokenizerUtils.SplitByDelimiter(text, _config.Delimiter, "removed");
        }
    }
}
