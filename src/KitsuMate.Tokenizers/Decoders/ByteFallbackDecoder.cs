using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// ByteFallback decoder implementation.
    /// </summary>
    public class ByteFallbackDecoder : IDecoder
    {
        public string Decode(IEnumerable<string> tokens)
        {
            var newTokens = new List<string>();
            var previousByteTokens = new List<byte>();

            foreach (var token in tokens)
            {
                byte? byteValue = null;
                if (token.Length == 6 && token.StartsWith("<0x") && token.EndsWith(">"))
                {
                    if (byte.TryParse(token.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte parsedByte))
                    {
                        byteValue = parsedByte;
                    }
                }

                if (byteValue.HasValue)
                {
                    previousByteTokens.Add(byteValue.Value);
                }
                else
                {
                    if (previousByteTokens.Any())
                    {
                        try
                        {
                            newTokens.Add(Encoding.UTF8.GetString(previousByteTokens.ToArray()));
                        }
                        catch
                        {
                            for (int i = 0; i < previousByteTokens.Count; i++)
                            {
                                newTokens.Add("\uFFFD"); // Replacement character (U+FFFD REPLACEMENT CHARACTER)
                            }
                        }
                        previousByteTokens.Clear();
                    }
                    newTokens.Add(token);
                }
            }

            if (previousByteTokens.Any())
            {
                try
                {
                    newTokens.Add(Encoding.UTF8.GetString(previousByteTokens.ToArray()));
                }
                catch
                {
                    for (int i = 0; i < previousByteTokens.Count; i++)
                    {
                        newTokens.Add("\uFFFD"); // Replacement character (U+FFFD REPLACEMENT CHARACTER)
                    }
                }
            }

            return string.Join("", newTokens);
        }
    }
}
