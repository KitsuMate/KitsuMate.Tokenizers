using System;
using System.Collections.Generic;
using System.Linq;

namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Result of post-processing tokenization.
    /// Matches HuggingFace tokenizers Encoding API.
    /// </summary>
    public class EncodingResult
    {
        public List<string> Tokens { get; set; } = new List<string>();
        public List<int> Ids { get; set; } = new List<int>();
        public List<int> TypeIds { get; set; } = new List<int>();
        public List<int?> Words { get; set; } = new List<int?>(); // Corresponds to Vec<Option<u32>>
        public List<(int Start, int End)> Offsets { get; set; } = new List<(int, int)>();
        public List<int> SpecialTokensMask { get; set; } = new List<int>();
        public List<int> AttentionMask { get; set; } = new List<int>();
        public List<EncodingResult> Overflowing { get; set; } = new List<EncodingResult>(); // Corresponds to Vec<Self>
        public Dictionary<int, (int Start, int End)> SequenceRanges { get; set; } = new Dictionary<int, (int Start, int End)>(); // Corresponds to AHashMap<usize, Range<usize>>

        /// <summary>
        /// Alias for Ids - matches HuggingFace API
        /// </summary>
        public List<int> InputIds => Ids;

        /// <summary>
        /// Alias for TypeIds - matches HuggingFace API
        /// </summary>
        public List<int> TokenTypeIds => TypeIds;

        public int Length => Ids.Count;

        public EncodingResult() { }

        public EncodingResult(
            List<int> ids,
            List<int> typeIds,
            List<string> tokens,
            List<int?> words,
            List<(int Start, int End)> offsets,
            List<int> specialTokensMask,
            List<int> attentionMask,
            List<EncodingResult> overflowing,
            Dictionary<int, (int Start, int End)> sequenceRanges)
        {
            Ids = ids;
            TypeIds = typeIds;
            Tokens = tokens;
            Words = words;
            Offsets = offsets;
            SpecialTokensMask = specialTokensMask;
            AttentionMask = attentionMask;
            Overflowing = overflowing;
            SequenceRanges = sequenceRanges;
        }

        public static EncodingResult FromTokenData(
            IReadOnlyList<int> ids,
            IReadOnlyList<string> tokens,
            int typeId,
            IReadOnlyList<(int Start, int End)>? offsets = null)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            if (ids.Count != tokens.Count)
            {
                throw new ArgumentException("Ids and tokens must have the same number of elements.");
            }

            if (offsets != null && offsets.Count != ids.Count)
            {
                throw new ArgumentException("Offsets must have the same number of elements as ids when provided.", nameof(offsets));
            }

            var length = ids.Count;
            var offsetList = offsets?.ToList() ?? Enumerable.Repeat((0, 0), length).ToList();

            return new EncodingResult
            {
                Ids = ids.ToList(),
                Tokens = tokens.ToList(),
                Offsets = offsetList,
                Words = Enumerable.Repeat<int?>(null, length).ToList(),
                TypeIds = Enumerable.Repeat(typeId, length).ToList(),
                AttentionMask = Enumerable.Repeat(1, length).ToList(),
                SpecialTokensMask = Enumerable.Repeat(0, length).ToList(),
                Overflowing = new List<EncodingResult>(),
                SequenceRanges = new Dictionary<int, (int Start, int End)>()
            };
        }

        public void SetSequenceId(int sequenceId)
        {
            SequenceRanges[sequenceId] = (0, Length);
        }

        public List<int?> GetSequenceIds()
        {
            var sequences = Enumerable.Repeat<int?>(null, Length).ToList();
            for (int seqId = 0; seqId < NSequences(); seqId++)
            {
                var range = GetSequenceRange(seqId);
                for (int i = range.Start; i < range.End; i++)
                {
                    sequences[i] = seqId;
                }
            }
            return sequences;
        }

        public int NSequences()
        {
            return SequenceRanges.Count == 0 ? 1 : SequenceRanges.Count;
        }

        public (int Start, int End) GetSequenceRange(int sequenceId)
        {
            if (SequenceRanges.TryGetValue(sequenceId, out var range))
            {
                return range;
            }
            return (0, Length);
        }

        public int? TokenToSequence(int tokenIndex)
        {
            if (tokenIndex >= Length)
            {
                return null;
            }
            if (SequenceRanges.Count == 0)
            {
                return 0;
            }
            foreach (var entry in SequenceRanges)
            {
                if (tokenIndex >= entry.Value.Start && tokenIndex < entry.Value.End)
                {
                    return entry.Key;
                }
            }
            return null;
        }

        public static EncodingResult Merge(IEnumerable<EncodingResult> encodings, bool growingOffsets)
        {
            var encoding = new EncodingResult();
            foreach (var sub in encodings)
            {
                encoding.MergeWith(sub, growingOffsets);
            }
            return encoding;
        }

        public void MergeWith(EncodingResult pair, bool growingOffsets)
        {
            var overflowings = new List<EncodingResult>();

            // 1. All our overflowings with all the others
            foreach (var selfO in Overflowing)
            {
                // 1. The pair itself
                var nEncoding = selfO.Clone(); // Assuming Clone method exists
                nEncoding.MergeWith(pair.Clone(), growingOffsets);
                overflowings.Add(nEncoding);

                // 2. Its overflowings (this should rarely happen...)
                foreach (var otherO in pair.Overflowing)
                {
                    var nEncoding2 = selfO.Clone();
                    nEncoding2.MergeWith(otherO.Clone(), growingOffsets);
                    overflowings.Add(nEncoding2);
                }
            }
            // 2. Ourself with all the other overflowings (this should rarely happen too...)
            foreach (var otherO in pair.Overflowing)
            {
                var nEncoding = this.Clone(); // Assuming Clone method exists
                nEncoding.MergeWith(otherO.Clone(), growingOffsets);
                overflowings.Add(nEncoding);
            }

            var originalSelfLen = Length;

            foreach (var entry in pair.SequenceRanges)
            {
                SequenceRanges[entry.Key] = (originalSelfLen + entry.Value.Start, originalSelfLen + entry.Value.End);
            }

            Ids.AddRange(pair.Ids);
            TypeIds.AddRange(pair.TypeIds);
            Tokens.AddRange(pair.Tokens);
            Words.AddRange(pair.Words);

            var startingOffset = 0;
            if (growingOffsets && Offsets.Any())
            {
                startingOffset = Offsets.Last().End;
            }

            Offsets.AddRange(pair.Offsets.Select(o => (o.Start + startingOffset, o.End + startingOffset)));
            SpecialTokensMask.AddRange(pair.SpecialTokensMask);
            AttentionMask.AddRange(pair.AttentionMask);
            Overflowing = overflowings;
        }

        // Need a Clone method for EncodingResult
        public EncodingResult Clone()
        {
            return new EncodingResult
            {
                Tokens = new List<string>(Tokens),
                Ids = new List<int>(Ids),
                TypeIds = new List<int>(TypeIds),
                Words = new List<int?>(Words),
                Offsets = new List<(int, int)>(Offsets),
                SpecialTokensMask = new List<int>(SpecialTokensMask),
                AttentionMask = new List<int>(AttentionMask),
                Overflowing = Overflowing.Select(o => o.Clone()).ToList(),
                SequenceRanges = new Dictionary<int, (int Start, int End)>(SequenceRanges)
            };
        }

        /// <summary>
        /// Pads the encoding to the specified length.
        /// Matches HuggingFace tokenizers Encoding.pad() method.
        /// </summary>
        /// <param name="length">Target length to pad to</param>
        /// <param name="direction">Direction to pad (right or left)</param>
        /// <param name="padId">ID to use for padding tokens</param>
        /// <param name="padTypeId">Type ID to use for padding</param>
        /// <param name="padToken">Token string to use for padding</param>
        public void Pad(int length, string direction = "right", int padId = 0, int padTypeId = 0, string padToken = "[PAD]")
        {
            if (Length >= length) return;

            int paddingLength = length - Length;
            var padIds = Enumerable.Repeat(padId, paddingLength).ToList();
            var padTypeIds = Enumerable.Repeat(padTypeId, paddingLength).ToList();
            var padTokens = Enumerable.Repeat(padToken, paddingLength).ToList();
            var padAttentionMask = Enumerable.Repeat(0, paddingLength).ToList();
            var padSpecialTokensMask = Enumerable.Repeat(1, paddingLength).ToList();
            var padWords = Enumerable.Repeat<int?>(null, paddingLength).ToList();
            var padOffsets = Enumerable.Repeat((0, 0), paddingLength).ToList();

            if (direction.ToLower() == "left")
            {
                // Pad on the left
                Ids.InsertRange(0, padIds);
                TypeIds.InsertRange(0, padTypeIds);
                Tokens.InsertRange(0, padTokens);
                AttentionMask.InsertRange(0, padAttentionMask);
                SpecialTokensMask.InsertRange(0, padSpecialTokensMask);
                Words.InsertRange(0, padWords);
                Offsets.InsertRange(0, padOffsets);
            }
            else
            {
                // Pad on the right (default)
                Ids.AddRange(padIds);
                TypeIds.AddRange(padTypeIds);
                Tokens.AddRange(padTokens);
                AttentionMask.AddRange(padAttentionMask);
                SpecialTokensMask.AddRange(padSpecialTokensMask);
                Words.AddRange(padWords);
                Offsets.AddRange(padOffsets);
            }
        }

        /// <summary>
        /// Truncates the encoding to the specified maximum length.
        /// Matches HuggingFace tokenizers Encoding.truncate() method.
        /// </summary>
        /// <param name="maxLength">Maximum length to truncate to</param>
        /// <param name="stride">Length of previous content to include in overflowing pieces (currently not fully implemented)</param>
        /// <param name="direction">Direction to truncate from (right or left)</param>
        public void Truncate(int maxLength, int stride = 0, string direction = "right")
        {
            if (Length <= maxLength) return;

            // TODO: Full stride implementation for overflowing tokens
            // The current basic implementation creates overflowing windows but doesn't properly
            // handle stride overlap as specified in HuggingFace tokenizers.
            // For production use, stride should be 0 until this is fully implemented.
            // See: https://huggingface.co/docs/tokenizers/api/encoding#tokenizers.Encoding.truncate
            if (stride > 0)
            {
                // Basic stride implementation - creates overflowing encodings
                // Note: This is a simplified version and may not match HuggingFace behavior exactly
                var overflows = new List<EncodingResult>();
                
                for (int start = maxLength; start < Length; start += maxLength)
                {
                    int windowStart = Math.Max(0, start - stride);
                    int windowEnd = Math.Min(start + maxLength, Length);
                    
                    if (windowStart >= Length) break;

                    var overflow = new EncodingResult
                    {
                        Ids = Ids.GetRange(windowStart, windowEnd - windowStart),
                        TypeIds = TypeIds.GetRange(windowStart, windowEnd - windowStart),
                        Tokens = Tokens.GetRange(windowStart, windowEnd - windowStart),
                        AttentionMask = AttentionMask.GetRange(windowStart, windowEnd - windowStart),
                        SpecialTokensMask = SpecialTokensMask.GetRange(windowStart, windowEnd - windowStart),
                        Words = Words.GetRange(windowStart, windowEnd - windowStart),
                        Offsets = Offsets.GetRange(windowStart, windowEnd - windowStart)
                    };
                    overflows.Add(overflow);
                }

                Overflowing = overflows;
            }

            if (direction.ToLower() == "left")
            {
                // Truncate from the left
                int removeCount = Length - maxLength;
                Ids.RemoveRange(0, removeCount);
                TypeIds.RemoveRange(0, removeCount);
                Tokens.RemoveRange(0, removeCount);
                AttentionMask.RemoveRange(0, removeCount);
                SpecialTokensMask.RemoveRange(0, removeCount);
                Words.RemoveRange(0, removeCount);
                Offsets.RemoveRange(0, removeCount);
            }
            else
            {
                // Truncate from the right (default)
                Ids = Ids.Take(maxLength).ToList();
                TypeIds = TypeIds.Take(maxLength).ToList();
                Tokens = Tokens.Take(maxLength).ToList();
                AttentionMask = AttentionMask.Take(maxLength).ToList();
                SpecialTokensMask = SpecialTokensMask.Take(maxLength).ToList();
                Words = Words.Take(maxLength).ToList();
                Offsets = Offsets.Take(maxLength).ToList();
            }
        }
    }
}
