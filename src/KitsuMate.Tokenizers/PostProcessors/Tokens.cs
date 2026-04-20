using System.Collections.Generic;

using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    /// <summary>
    /// A bunch of SpecialToken represented by their ID.
    /// </summary>
    public class Tokens : Dictionary<string, SpecialToken>
    {
        public Tokens() { }
        public Tokens(IEnumerable<SpecialToken> specialTokens)
        {
            foreach (var token in specialTokens)
            {
                Add(token.Id, token);
            }
        }
    }
}
