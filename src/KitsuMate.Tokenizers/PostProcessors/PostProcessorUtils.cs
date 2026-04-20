using System;
using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers; // Add this using statement

namespace KitsuMate.Tokenizers.PostProcessors
{
    public static class PostProcessorUtils
    {
        private static readonly Dictionary<byte, char> BytesChar = CreateBytesCharMap();
        private static readonly Dictionary<char, byte> CharBytes = CreateCharBytesMap();

        private static Dictionary<byte, char> CreateBytesCharMap()
        {
            var bs = new List<byte>();
            for (int i = '!'; i <= '~'; i++) bs.Add((byte)i);
            for (int i = 0xA1; i <= 0xAC; i++) bs.Add((byte)i);
            for (int i = 0xAE; i <= 0xFF; i++) bs.Add((byte)i);

            var cs = bs.Select(b => (uint)b).ToList();
            uint n = 0;

            for (uint b = 0; b <= 255; b++)
            {
                if (!bs.Contains((byte)b))
                {
                    bs.Add((byte)b);
                    cs.Add(256 + n);
                    n += 1;
                }
            }

            var map = new Dictionary<byte, char>();
            for (int i = 0; i < bs.Count; i++)
            {
                map[bs[i]] = (char)cs[i];
            }
            return map;
        }

        private static Dictionary<char, byte> CreateCharBytesMap()
        {
            return BytesChar.ToDictionary(entry => entry.Value, entry => entry.Key);
        }

        // Port of process_offsets from Rust byte_level.rs
        internal static void ProcessOffsets(EncodingResult encoding, bool addPrefixSpace)
        {
            for (int i = 0; i < encoding.Tokens.Count; i++)
            {
                string token = encoding.Tokens[i];
                (int start, int end) offsets = encoding.Offsets[i];

                int leadingSpaces = 0;
                foreach (char c in token)
                {
                    if (c == BytesChar[(byte)' '] || char.IsWhiteSpace(c))
                    {
                        leadingSpaces++;
                    }
                    else
                    {
                        break;
                    }
                }

                int trailingSpaces = 0;
                for (int j = token.Length - 1; j >= 0; j--)
                {
                    char c = token[j];
                    if (c == BytesChar[(byte)' '] || char.IsWhiteSpace(c))
                    {
                        trailingSpaces++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (leadingSpaces > 0 || trailingSpaces > 0)
                {
                    if (leadingSpaces > 0)
                    {
                        bool isFirst = i == 0 || offsets.start == 0;
                        if (isFirst && addPrefixSpace && leadingSpaces == 1)
                        {
                            leadingSpaces = 0;
                        }
                        offsets.start = Math.Min(offsets.start + leadingSpaces, offsets.end);
                    }
                    if (trailingSpaces > 0 && offsets.end >= trailingSpaces)
                    {
                        offsets.end = Math.Max(offsets.end - trailingSpaces, offsets.start);
                    }
                    encoding.Offsets[i] = offsets;
                }
            }
        }
    }
}
