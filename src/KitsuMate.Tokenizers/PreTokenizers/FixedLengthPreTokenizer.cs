using System;
using System.Collections.Generic;
using System.Text;
using System.Linq; // For Enumerable.Empty

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class FixedLengthPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;

        public FixedLengthPreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Enumerable.Empty<(int, int)>();
            }

            var results = new List<(int, int)>();
            var fixedCharLength = _config.Length ?? 5; // Default to 5 as per Rust implementation

            int currentByteOffset = 0;
            int currentCharIndex = 0;

            while (currentCharIndex < text.Length)
            {
                int segmentStartCharIndex = currentCharIndex;
                int segmentStartByteOffset = currentByteOffset;
                int charsInSegment = 0;
                int bytesInSegment = 0;

                // Iterate through characters to form a segment of fixedCharLength
                while (charsInSegment < fixedCharLength && currentCharIndex < text.Length)
                {
                    int charLen = 1; // Default for most characters
                    if (char.IsHighSurrogate(text[currentCharIndex]))
                    {
                        // Check for surrogate pair
                        if (currentCharIndex + 1 < text.Length && char.IsLowSurrogate(text[currentCharIndex + 1]))
                        {
                            charLen = 2;
                        }
                    }
                    
                    // Get byte count for the current character(s)
                    string charSubstring = text.Substring(currentCharIndex, charLen);
                    bytesInSegment += Encoding.UTF8.GetByteCount(charSubstring);

                    currentCharIndex += charLen;
                    charsInSegment++;
                }
                
                results.Add((segmentStartByteOffset, bytesInSegment));
                currentByteOffset = segmentStartByteOffset + bytesInSegment; // Update current byte offset for the next segment
            }

            return results;
        }
    }
}
