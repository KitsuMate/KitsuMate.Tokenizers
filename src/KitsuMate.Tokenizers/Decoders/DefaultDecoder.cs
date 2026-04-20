using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// Default decoder that simply concatenates tokens.
    /// </summary>
    public class DefaultDecoder : IDecoder
    {
        public string Decode(IEnumerable<string> tokens)
        {
            return string.Join("", tokens);
        }
    }
}
