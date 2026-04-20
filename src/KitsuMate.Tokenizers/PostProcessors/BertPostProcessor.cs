using System.Collections.Generic;
using System.Linq;

using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    public class BertPostProcessor : IPostProcessor
    {
        private readonly (string Token, int Id) _cls;
        private readonly (string Token, int Id) _sep;

        public BertPostProcessor((string Token, int Id) cls, (string Token, int Id) sep)
        {
            _cls = cls;
            _sep = sep;
        }

        public int AddedTokens(bool isPair)
        {
            return isPair ? 3 : 2;
        }

        public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
        {
            if (!addSpecialTokens)
            {
                // If no special tokens are to be added, simply merge the encodings
                return EncodingResult.Merge(encodings, false); // Assuming Merge method exists and handles overflowing
            }

            if (encodings.Count == 0)
            {
                return new EncodingResult();
            }

            EncodingResult encodingA = encodings[0];
            EncodingResult encodingB = encodings.Count > 1 ? encodings[1] : null;

            var result = new EncodingResult();

            // Add CLS token
            result.Tokens.Add(_cls.Token);
            result.Ids.Add(_cls.Id);
            result.TypeIds.Add(0);
            result.Offsets.Add((0, 0)); // Special token offset
            result.SpecialTokensMask.Add(1);
            result.AttentionMask.Add(1);
            result.Words.Add(null);

            // Add first sequence
            result.Tokens.AddRange(encodingA.Tokens);
            result.Ids.AddRange(encodingA.Ids);
            result.TypeIds.AddRange(Enumerable.Repeat(0, encodingA.Length));
            result.Offsets.AddRange(encodingA.Offsets);
            result.SpecialTokensMask.AddRange(Enumerable.Repeat(0, encodingA.Length));
            result.AttentionMask.AddRange(Enumerable.Repeat(1, encodingA.Length));
            result.Words.AddRange(encodingA.Words);
            encodingA.SetSequenceId(0); // Set sequence ID for the first sequence

            // Add SEP token
            result.Tokens.Add(_sep.Token);
            result.Ids.Add(_sep.Id);
            result.TypeIds.Add(0);
            result.Offsets.Add((0, 0)); // Special token offset
            result.SpecialTokensMask.Add(1);
            result.AttentionMask.Add(1);
            result.Words.Add(null);

            // Add second sequence if present
            if (encodingB != null && encodingB.Length > 0)
            {
                result.Tokens.AddRange(encodingB.Tokens);
                result.Ids.AddRange(encodingB.Ids);
                result.TypeIds.AddRange(Enumerable.Repeat(1, encodingB.Length));
                result.Offsets.AddRange(encodingB.Offsets);
                result.SpecialTokensMask.AddRange(Enumerable.Repeat(0, encodingB.Length));
                result.AttentionMask.AddRange(Enumerable.Repeat(1, encodingB.Length));
                result.Words.AddRange(encodingB.Words);
                encodingB.SetSequenceId(1); // Set sequence ID for the second sequence

                // Add final SEP token
                result.Tokens.Add(_sep.Token);
                result.Ids.Add(_sep.Id);
                result.TypeIds.Add(1);
                result.Offsets.Add((0, 0)); // Special token offset
                result.SpecialTokensMask.Add(1);
                result.AttentionMask.Add(1);
                result.Words.Add(null);
            }

            // Handle sequence ranges for the combined result
            // This part needs to be carefully implemented to match Rust's AHashMap<usize, Range<usize>> logic
            // For now, a simplified version:
            int currentLength = 0;
            if (encodingA != null)
            {
                result.SequenceRanges[0] = (currentLength + 1, currentLength + 1 + encodingA.Length); // +1 for CLS
                currentLength += (1 + encodingA.Length + 1); // CLS + A + SEP
            }
            if (encodingB != null && encodingB.Length > 0)
            {
                result.SequenceRanges[1] = (currentLength, currentLength + encodingB.Length);
                currentLength += (encodingB.Length + 1); // B + SEP
            }

            // Handle overflowing encodings (this is complex and needs to be ported carefully from Rust)
            // For now, a placeholder:
            // result.Overflowing.AddRange(encodingA.Overflowing);
            // if (encodingB != null) result.Overflowing.AddRange(encodingB.Overflowing);

            return result;
        }
    }
}
