using System;
using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    public class TemplatePostProcessor : IPostProcessor
    {
        private readonly Template _single;
        private readonly Template _pair;
        private readonly Tokens _specialTokens;
        private readonly int _addedSingle;
        private readonly int _addedPair;

        public TemplatePostProcessor(Template single, Template pair, Tokens specialTokens)
        {
            _single = single;
            _pair = pair;
            _specialTokens = specialTokens;

            _addedSingle = CountAdded(_single, _specialTokens);
            _addedPair = CountAdded(_pair, _specialTokens);
        }

        private int CountAdded(Template template, Tokens specialTokens)
        {
            int count = 0;
            foreach (var piece in template)
            {
                if (piece.SpecialTokenId != null)
                {
                    if (specialTokens.TryGetValue(piece.SpecialTokenId, out var specialToken))
                    {
                        count += specialToken.Ids.Count;
                    }
                }
            }
            return count;
        }

        public int AddedTokens(bool isPair)
        {
            return isPair ? _addedPair : _addedSingle;
        }

        public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
        {
            Template templateToUse;
            if (encodings.Count == 2)
            {
                templateToUse = _pair;
            }
            else if (encodings.Count == 1)
            {
                templateToUse = _single;
            }
            else
            {
                throw new ArgumentException("TemplatePostProcessor expects 1 or 2 encodings.");
            }

            return ApplyTemplate(templateToUse, encodings, addSpecialTokens);
        }

        private EncodingResult ApplyTemplate(Template template, List<EncodingResult> encodings, bool addSpecialTokens)
        {
            var finalEncodings = new List<EncodingResult>();
            
            foreach (var piece in template)
            {
                if (piece.SequenceId.HasValue)
                {
                    int index = (piece.SequenceId.Value == Sequence.A) ? 0 : 1;
                    if (encodings.Count > index)
                    {
                        var encoding = encodings[index].Clone(); // Clone to avoid modifying original
                        for (int i = 0; i < encoding.TypeIds.Count; i++)
                        {
                            encoding.TypeIds[i] = piece.TypeId;
                        }
                        encoding.SetSequenceId(index);
                        finalEncodings.Add(encoding);
                    }
                }
                else if (piece.SpecialTokenId != null)
                {
                    if (addSpecialTokens && _specialTokens.TryGetValue(piece.SpecialTokenId, out var specialToken))
                    {
                        var len = specialToken.Ids.Count;
                        var newEncoding = new EncodingResult(
                            specialToken.Ids,
                            Enumerable.Repeat(piece.TypeId, len).ToList(),
                            specialToken.Tokens,
                            Enumerable.Repeat<int?>(null, len).ToList(), // words
                            Enumerable.Repeat((0, 0), len).ToList(), // offsets
                            Enumerable.Repeat(1, len).ToList(), // special_tokens_mask
                            Enumerable.Repeat(1, len).ToList(), // attention_mask
                            new List<EncodingResult>(), // overflowing
                            new Dictionary<int, (int Start, int End)>() // sequence_ranges
                        );
                        finalEncodings.Add(newEncoding);
                    }
                }
            }
            
            return EncodingResult.Merge(finalEncodings, false); // Merge all pieces into one EncodingResult
        }
    }
}
