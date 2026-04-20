using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers.PreTokenizers.Utils;

namespace KitsuMate.Tokenizers.PreTokenizers
{
    public class UnicodeScriptsPreTokenizer : IPreTokenizer
    {
        public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
        {
            var results = new List<(int, int)>();
            if (string.IsNullOrEmpty(text)) return results;

            var lastScript = Script.Unknown; // Use Unknown as initial state
            var currentSegmentStart = 0;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var currentScript = ScriptUtils.FixedScript(c);

                // Handle surrogate pairs for correct character processing
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // This is a surrogate pair, advance i by one more
                    i++;
                }

                if (currentScript != Script.Any && lastScript != Script.Unknown && lastScript != Script.Any && lastScript != currentScript)
                {
                    // Script changed and neither is 'Any', so split
                    results.Add((currentSegmentStart, i - currentSegmentStart));
                    currentSegmentStart = i;
                }

                if (currentScript != Script.Any)
                {
                    lastScript = currentScript;
                }
            }

            // Add the last segment
            if (text.Length > currentSegmentStart)
            {
                results.Add((currentSegmentStart, text.Length - currentSegmentStart));
            }

            return results.Where(r => r.Item2 > 0);
        }
    }
}
