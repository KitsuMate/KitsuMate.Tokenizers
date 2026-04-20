using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// ByteLevel decoder implementation for GPT-2/RoBERTa style byte-level BPE.
    /// This decoder converts Unicode characters back to bytes and then decodes as UTF-8.
    /// </summary>
    public class ByteLevelDecoder : IDecoder
    {
        private static readonly Dictionary<char, byte> _charToByte;

        static ByteLevelDecoder()
        {
            // Initialize the character-to-byte mapping
            // This is the inverse of the bytes_to_unicode() function used in GPT-2/RoBERTa
            _charToByte = BuildCharToByteMapping();
        }

        /// <summary>
        /// Builds the character-to-byte mapping used in GPT-2/RoBERTa byte-level BPE.
        /// This mapping avoids control characters by using a shifted Unicode range.
        /// </summary>
        private static Dictionary<char, byte> BuildCharToByteMapping()
        {
            var charToByte = new Dictionary<char, byte>();

            // Start with printable ASCII characters
            var bs = new List<int>();
            var cs = new List<int>();

            // Add printable ASCII range
            for (int b = (int)'!'; b <= (int)'~'; b++) bs.Add(b);
            for (int b = (int)'¡'; b <= (int)'¬'; b++) bs.Add(b);
            for (int b = (int)'®'; b <= (int)'ÿ'; b++) bs.Add(b);

            cs.AddRange(bs);

            // Map remaining bytes to unused Unicode range (0x100+)
            int n = 0;
            for (int b = 0; b < 256; b++)
            {
                if (!bs.Contains(b))
                {
                    bs.Add(b);
                    cs.Add(256 + n);
                    n++;
                }
            }

            // Create the mapping from character to byte
            for (int i = 0; i < bs.Count; i++)
            {
                charToByte[(char)cs[i]] = (byte)bs[i];
            }

            return charToByte;
        }

        public string Decode(IEnumerable<string> tokens)
        {
            try
            {
                // Concatenate all tokens
                var text = string.Join("", tokens);
                
                // Convert each character to its corresponding byte
                var bytes = new List<byte>();
                foreach (char c in text)
                {
                    if (_charToByte.TryGetValue(c, out byte b))
                    {
                        bytes.Add(b);
                    }
                    else
                    {
                        // If character is not in mapping, this shouldn't happen in valid byte-level BPE
                        // Fall back to UTF-8 encoding of the character
                        bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                    }
                }

                // Decode the bytes as UTF-8
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
            catch
            {
                // Fallback to simple concatenation if decoding fails
                return string.Join("", tokens);
            }
        }
    }
}
