using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; // Needed for Regex
using KitsuMate.Tokenizers; // For TokenizerUtils

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class BertPreTokenizer : IPreTokenizer
    {
        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Enumerable.Empty<(int, int)>();
            }

            // Step 1: Split by whitespace, removing the whitespace
            // The Rust `char::is_whitespace` includes more than just ' '
            // We'll use a regex for whitespace to match the Rust behavior more closely,
            // or rely on TokenizerUtils.SplitByWhitespace if it's suitable.
            // Looking at TokenizerUtils.SplitByWhitespace, it uses Regex(@"\S+", RegexOptions.Compiled)
            // which splits on non-whitespace. This is equivalent to SplitDelimiterBehavior::Removed.
            var whitespaceSplits = TokenizerUtils.SplitByWhitespace(text).ToList();

            var finalSplits = new List<(int Offset, int Length)>();

            // Step 2: For each split, further split by BERT punctuation, isolating the punctuation
            // We need a Regex that matches BERT punctuation.
            // Since IsBertPunc is a char predicate, we need to convert it to a regex pattern.
            // A simple way is to match any character for which IsBertPunc returns true.
            // This can be done by creating a regex that matches any character in the Unicode Punctuation categories.
            // However, the Rust `is_bert_punc` is `char::is_ascii_punctuation(&x) || x.is_punctuation()`.
            // Our `TokenizerUtils.IsBertPunc` is `char.IsPunctuation(c)`.
            // Let's create a regex that matches any character that `TokenizerUtils.IsBertPunc` identifies as punctuation.
            // This is tricky with Regex directly. A simpler approach is to iterate and split.

            foreach (var wsSplit in whitespaceSplits)
            {
                string segment = text.Substring(wsSplit.Offset, wsSplit.Length);
                int currentSegmentOffset = 0;

                while (currentSegmentOffset < segment.Length)
                {
                    int start = currentSegmentOffset;
                    char currentChar = segment[currentSegmentOffset];

                    if (TokenizerUtils.IsBertPunc(currentChar))
                    {
                        // Isolate punctuation
                        finalSplits.Add((wsSplit.Offset + start, 1));
                        currentSegmentOffset++;
                    }
                    else
                    {
                        // Collect non-punctuation characters
                        while (currentSegmentOffset < segment.Length && !TokenizerUtils.IsBertPunc(segment[currentSegmentOffset]))
                        {
                            currentSegmentOffset++;
                        }
                        if (currentSegmentOffset > start)
                        {
                            finalSplits.Add((wsSplit.Offset + start, currentSegmentOffset - start));
                        }
                    }
                }
            }

            return finalSplits;
        }
    }
}
