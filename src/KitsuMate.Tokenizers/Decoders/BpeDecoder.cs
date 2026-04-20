using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// BPE decoder implementation.
    /// </summary>
    public class BpeDecoder : IDecoder
    {
        private readonly string _suffix;

        public BpeDecoder(string suffix)
        {
            _suffix = suffix;
        }

        public string Decode(IEnumerable<string> tokens)
        {
            var tokenList = tokens.ToList();
            var result = new StringBuilder();

            for (int i = 0; i < tokenList.Count; i++)
            {
                var token = tokenList[i];
                var replacement = (i == tokenList.Count - 1) ? "" : " ";
                result.Append(token.Replace(_suffix, replacement));
            }

            return result.ToString().TrimEnd();
        }
    }
}
