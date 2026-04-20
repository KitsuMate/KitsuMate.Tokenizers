using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; // For Encoding.UTF8.GetByteCount

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class SequencePreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;
        private readonly Func<PreTokenizerConfig, IPreTokenizer> _preTokenizerFactory;

        public SequencePreTokenizer(PreTokenizerConfig config, Func<PreTokenizerConfig, IPreTokenizer> preTokenizerFactory)
        {
            _config = config;
            _preTokenizerFactory = preTokenizerFactory;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Enumerable.Empty<(int, int)>();
            }

            // Represents the current state of splits: (segment text, original offset in the initial 'text')
            var currentSegments = new List<(string SegmentText, int OriginalOffset)>
            {
                (text, 0)
            };
            
            // Apply each pre-tokenizer in the sequence
            if (_config.PreTokenizers != null)
            {
                foreach (var preTokenizerConfig in _config.PreTokenizers)
                {
                    var nextSegments = new List<(string SegmentText, int OriginalOffset)>();
                    var tempPreTokenizer = _preTokenizerFactory(preTokenizerConfig);
                    
                    foreach (var currentSegment in currentSegments)
                    {
                        // Pre-tokenize the current segment
                        var subResults = tempPreTokenizer.PreTokenize(currentSegment.SegmentText);
                        
                        foreach (var subResult in subResults)
                        {
                            // Calculate the actual substring from the current segment
                            var newSegmentText = currentSegment.SegmentText.Substring(subResult.Offset, subResult.Length);
                            // Calculate the original offset in the initial 'text'
                            var newOriginalOffset = currentSegment.OriginalOffset + subResult.Offset;
                            
                            nextSegments.Add((newSegmentText, newOriginalOffset));
                        }
                    }
                    
                    currentSegments = nextSegments;
                }
            }
            
            // Convert the final segments to (byte offset, byte length) tuples
            var finalResults = new List<(int Offset, int Length)>();
            foreach (var segment in currentSegments)
            {
                // Calculate the byte offset and length based on the original text
                // This requires re-calculating byte offsets from the start of the original text
                // or maintaining byte offsets throughout the process.
                // For simplicity and correctness, let's re-calculate byte offset from original text.
                
                // Find the byte offset of segment.OriginalOffset in the original 'text'
                int byteOffset = Encoding.UTF8.GetByteCount(text.Substring(0, segment.OriginalOffset));
                int byteLength = Encoding.UTF8.GetByteCount(segment.SegmentText);
                
                finalResults.Add((byteOffset, byteLength));
            }
            
            return finalResults;
        }
    }
}
