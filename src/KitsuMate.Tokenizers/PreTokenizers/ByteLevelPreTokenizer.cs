using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class ByteLevelPreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;
        private static readonly Regex WordOrNonWordRegex = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.Compiled);

        public ByteLevelPreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            var currentText = text;
            var results = new List<(int, int)>();

            // 1. Handle add_prefix_space
            var prefixAdded = false;
            if (_config.AddPrefixSpace == true && !string.IsNullOrEmpty(currentText) && !char.IsWhiteSpace(currentText[0]))
            {
                currentText = " " + currentText;
                prefixAdded = true;
            }

            // 2. Apply regex split or return the whole string if regex is not used
            IEnumerable<(int Offset, int Length)> initialSplits;
            if (_config.UseRegex != false) // Default to true
            {
                initialSplits = TokenizerUtils.SplitByRegex(currentText, WordOrNonWordRegex, "isolated"); // Rust uses Isolated for this regex
            }
            else
            {
                // If use_regex is false, the Rust implementation does not split, it returns the whole string.
                initialSplits = new List<(int Offset, int Length)> { (0, currentText.Length) };
            }

            // 3. Apply byte-level mapping to each segment and adjust offsets
            foreach (var (offset, length) in initialSplits)
            {
                var segment = currentText.Substring(offset, length);
                var byteLevelSegment = TokenizerUtils.ApplyByteLevelMapping(segment);
                
                // The length of the byte-level segment is the new length
                // The offset remains the same relative to the original text (or currentText after prefixing)
                results.Add((offset, byteLevelSegment.Length));
            }

            // Adjust offsets back to original text if we added a prefix
            if (prefixAdded)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var (offset, length) = results[i];
                    if (offset == 0) // This is the first token, which might have had the prefix added
                    {
                        if (length > 0) // Ensure it's not an empty token
                        {
                            results[i] = (0, length - 1); // Remove the length of the added space
                        }
                    }
                    else
                    {
                        results[i] = (offset - 1, length); // Shift offset back by 1
                    }
                }
                // Filter out any tokens that became empty after adjustment (e.g., if the prefix was the only content)
                results = results.Where(r => r.Item2 > 0).ToList();
            }

            return results;
        }
    }
}
