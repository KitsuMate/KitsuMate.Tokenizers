using System;
using System.Collections.Generic;

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public interface IPreTokenizer
    {
        IEnumerable<(int Offset, int Length)> PreTokenize(string text);
    }
}
