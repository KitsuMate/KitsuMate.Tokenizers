using System;
using System.Collections.Generic;

namespace KitsuMate.Tokenizers.PostProcessors
{
    /// <summary>
    /// Represents a bunch of tokens to be used in a template.
    /// </summary>
    public class SpecialToken
    {
        public string Id { get; set; }
        public List<int> Ids { get; set; } = new List<int>();
        public List<string> Tokens { get; set; } = new List<string>();

        public SpecialToken(string id, List<int> ids, List<string> tokens)
        {
            if (ids.Count != tokens.Count)
            {
                throw new ArgumentException("SpecialToken: ids and tokens must be of the same length");
            }
            Id = id;
            Ids = ids;
            Tokens = tokens;
        }

        public SpecialToken(string id, int singleId, string singleToken)
        {
            Id = id;
            Ids = new List<int> { singleId };
            Tokens = new List<string> { singleToken };
        }
    }
}
