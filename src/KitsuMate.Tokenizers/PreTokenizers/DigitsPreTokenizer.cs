using System.Collections.Generic;
using System.Linq; // For Enumerable.Empty
using System; // For char.IsNumber

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class DigitsPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;

        public DigitsPreTokenizer(PreTokenizerConfig config)
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
            int currentOffset = 0;

            while (currentOffset < text.Length)
            {
                int start = currentOffset;
                bool isCurrentCharNumeric = char.IsNumber(text[currentOffset]);

                if (isCurrentCharNumeric)
                {
                    if (_config.IndividualDigits == true)
                    {
                        // Isolated behavior: each digit is a separate token
                        results.Add((start, 1));
                        currentOffset++;
                    }
                    else
                    {
                        // Contiguous behavior: group contiguous digits
                        while (currentOffset < text.Length && char.IsNumber(text[currentOffset]))
                        {
                            currentOffset++;
                        }
                        results.Add((start, currentOffset - start));
                    }
                }
                else
                {
                    // Non-numeric characters: group until a numeric character is found
                    while (currentOffset < text.Length && !char.IsNumber(text[currentOffset]))
                    {
                        currentOffset++;
                    }
                    results.Add((start, currentOffset - start));
                }
            }

            return results.Where(r => r.Item2 > 0); // Filter out empty results (Item2 is Length)
        }
    }
}
