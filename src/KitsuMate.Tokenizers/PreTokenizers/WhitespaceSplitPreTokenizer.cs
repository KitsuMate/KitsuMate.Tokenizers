using System.Collections.Generic;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class WhitespaceSplitPreTokenizer : IPreTokenizer
    {
        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            // Simple whitespace splitting
            return TokenizerUtils.SplitByWhitespace(text);
        }
    }
}
