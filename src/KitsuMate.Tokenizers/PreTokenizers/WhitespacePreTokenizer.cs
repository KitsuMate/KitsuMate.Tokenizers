using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class WhitespacePreTokenizer : IPreTokenizer
    {
        // Rust's Whitespace pre-tokenizer uses the regex r"\w+|[^\w\s]+"
        private static readonly Regex WordOrNonWordRegex = new Regex(@"\w+|[^\w\s]+", RegexOptions.Compiled);

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            // Uses regex pattern \w+|[^\w\s]+ and inverts the split
            return TokenizerUtils.SplitByRegex(text, WordOrNonWordRegex, invert: true);
        }
    }
}
