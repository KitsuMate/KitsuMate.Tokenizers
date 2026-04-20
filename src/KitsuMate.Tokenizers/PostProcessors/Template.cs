using System.Collections.Generic;
using System.Linq;
using System;
using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    /// <summary>
    /// A Template represents a List of Piece.
    /// </summary>
    public class Template : List<Piece>
    {
        public Template() { }
        public Template(IEnumerable<Piece> collection) : base(collection) { }

        public static Template FromString(string s)
        {
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var pieces = new List<Piece>();
            foreach (var part in parts)
            {
                pieces.Add(Piece.FromString(part));
            }
            return new Template(pieces);
        }
    }
}
