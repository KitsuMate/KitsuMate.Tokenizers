using System.Collections.Generic;
using KitsuMate.Tokenizers;

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class DefaultPreTokenizer : IPreTokenizer
    {
        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            return TokenizerUtils.SplitByWhitespace(text);
        }
    }
}
