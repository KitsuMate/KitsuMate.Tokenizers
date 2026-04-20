using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class MetaspacePreTokenizer : IPreTokenizer
    {
        private readonly PreTokenizerConfig _config;

        public MetaspacePreTokenizer(PreTokenizerConfig config)
        {
            _config = config;
        }

        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            var replacement = _config.Replacement ?? " ";
            var prepend = _config.PrependScheme?.ToLowerInvariant() ?? "always";
            var split = _config.Split ?? true; // Default to true as per docs
            
            var originalText = text;
            
            // Replace all individual whitespace characters with replacement character
            // Rust's `normalized.replace(' ', &self.str_rep)` replaces each space.
            // We need to iterate through the string and replace each whitespace character.
            var processedTextBuilder = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    processedTextBuilder.Append(replacement);
                }
                else
                {
                    processedTextBuilder.Append(c);
                }
            }
            var processedText = processedTextBuilder.ToString();
            
            // Handle prepend scheme
            var prefixAdded = false;
            if (prepend == "always" && !processedText.StartsWith(replacement))
            {
                processedText = replacement + processedText;
                prefixAdded = true;
            }
            else if (prepend == "first")
            {
                // "first" means only on the very first token (when no previous tokens exist)
                // For our implementation, we'll treat this as applying to the first token of this text
                // In a full tokenizer pipeline, this would only apply to the absolute first token
                if (!processedText.StartsWith(replacement))
                {
                    processedText = replacement + processedText;
                    prefixAdded = true;
                }
            }
            // "never" means don't prepend
            
            // Split on replacement character if split is enabled
            if (split)
            {
                var results = TokenizerUtils.SplitByDelimiter(processedText, replacement, "merged_with_next").ToList();
                
                // Adjust offsets back to original text if we added a prefix
                if (prefixAdded)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        var (offset, length) = results[i];
                        if (offset > 0) // Skip the added prefix
                        {
                            results[i] = (offset - 1, length);
                        }
                        else if (length > 1) // The first token includes the prefix
                        {
                            results[i] = (0, length - 1);
                        }
                    }
                    results = results.Where(r => r.Item2 > 0).ToList();
                }
                
                return results;
            }
            
            // Return as single token if split is disabled
            if (prefixAdded && processedText.Length > 1)
            {
                return new List<(int, int)> { (0, processedText.Length - 1) };
            }
            
            return new List<(int, int)> { (0, processedText.Length) };
        }
    }
}
