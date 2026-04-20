using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Utility methods for text tokenization and normalization.
    /// </summary>
    public static class TokenizerUtils
    {
        private static readonly Regex WhitespaceRegex = new Regex(@"\S+", RegexOptions.Compiled);
        private static readonly Dictionary<byte, char> _byteToCharMap = CreateBytesCharMap();
        private static readonly Dictionary<char, byte> _charToByteMap = CreateCharBytesMap();

        private static Dictionary<byte, char> CreateBytesCharMap()
        {
            var map = new Dictionary<byte, char>();
            int n = 0;
            for (int i = 0; i <= 255; i++)
            {
                byte b = (byte)i;
                // Bytes 33-126, 161-172, 174-255 map to themselves as chars
                if ((b >= 33 && b <= 126) || (b >= 161 && b <= 172) || (b >= 174 && b <= 255))
                {
                    map[b] = (char)b;
                }
                else
                {
                    // Other bytes map to U+0100 onwards
                    map[b] = (char)(256 + n);
                    n++;
                }
            }
            return map;
        }

        private static Dictionary<char, byte> CreateCharBytesMap()
        {
            return _byteToCharMap.ToDictionary(entry => entry.Value, entry => entry.Key);
        }

        /// <summary>
        /// Apply byte-level character mapping as per HuggingFace ByteLevel implementation
        /// Maps each byte value to a unique visible character from a 256-character alphabet
        /// </summary>
        /// <param name="text">Input text to map</param>
        /// <returns>Text with byte-level character mapping applied</returns>
        internal static string ApplyByteLevelMapping(string text)
        {
            var result = new StringBuilder();
            var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
            
            foreach (byte b in textBytes)
            {
                result.Append(_byteToCharMap[b]);
            }
            
            return result.ToString();
        }

        internal static string ApplyByteLevelMapping(IEnumerable<byte> bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var result = new StringBuilder();
            foreach (var value in bytes)
            {
                result.Append(_byteToCharMap[value]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Checks whether a character is whitespace.
        /// </summary>
        public static bool IsWhitespace(char c)
        {
            // These are technically control characters but we count them as whitespace
            return c == '\t' || c == '\n' || c == '\r' || char.IsWhiteSpace(c);
        }

        /// <summary>
        /// Checks whether a character is BERT punctuation.
        /// Matches the Rust tokenizers library definition: char::is_ascii_punctuation(&x) || x.is_punctuation()
        /// </summary>
        public static bool IsBertPunc(char c)
        {
            // ASCII punctuation characters (matching Rust's char::is_ascii_punctuation)
            // These are: !"#$%&'()*+,-./:;<=>?@[\]^_`{|}~
            if (c >= 33 && c <= 47) return true;  // ! " # $ % & ' ( ) * + , - . /
            if (c >= 58 && c <= 64) return true;  // : ; < = > ? @
            if (c >= 91 && c <= 96) return true;  // [ \ ] ^ _ `
            if (c >= 123 && c <= 126) return true; // { | } ~
            
            // For non-ASCII characters, use C#'s IsPunctuation
            // `char.IsPunctuation` in C# covers a broader range than ASCII punctuation.
            return char.IsPunctuation(c);
        }

        /// <summary>
        /// Checks whether a character is a control character.
        /// Note: Surrogate pairs are NOT considered control characters as they are valid
        /// UTF-16 encoding for characters outside the BMP (including emojis).
        /// </summary>
        public static bool IsControl(char c)
        {
            // The definition of `is_control` here is quite large and contains also
            // Cc, Cf, Cn or Co
            // cf. https://unicode.org/reports/tr44/ (Table 12)
            // NOTE: We exclude UnicodeCategory.Surrogate because surrogate pairs are valid
            // UTF-16 encoding for characters like emojis and should not be filtered out.
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.OtherNotAssigned ||
                   category == UnicodeCategory.Control ||
                   category == UnicodeCategory.Format ||
                   category == UnicodeCategory.PrivateUse;
        }

        /// <summary>
        /// Checks whether a character is Chinese.
        /// This defines a "Chinese character" as anything in the CJK Unicode block.
        /// </summary>
        public static bool IsChineseChar(int code)
        
        {
            return (code >= 0x4E00 && code <= 0x9FFF) ||
                   (code >= 0x3400 && code <= 0x4DBF) ||
                   (code >= 0x20000 && code <= 0x2A6DF) || // CJK Unified Ideographs Extension B
                   (code >= 0x2A700 && code <= 0x2B73F) || // CJK Unified Ideographs Extension C
                   (code >= 0x2B740 && code <= 0x2B81F) || // CJK Unified Ideographs Extension D
                   (code >= 0x2B920 && code <= 0x2CEAF) || // CJK Unified Ideographs Extension E
                   (code >= 0xF900 && code <= 0xFAFF) || // CJK Compatibility Ideographs
                   (code >= 0x2F800 && code <= 0x2FA1F); // CJK Compatibility Ideographs Supplement
        }

        /// <summary>
        /// Strips accents from the input text.
        /// Handles emoji and other Unicode characters gracefully by catching normalization errors.
        /// </summary>
        public static string StripAccents(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                var normalizedString = text.Normalize(NormalizationForm.FormD);
                var stringBuilder = new StringBuilder();

                foreach (char c in normalizedString)
                {
                    var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    {
                        stringBuilder.Append(c);
                    }
                }

                return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                // If normalization fails (e.g., due to invalid Unicode sequences like unpaired surrogates),
                // just strip combining marks manually without normalization
                var stringBuilder = new StringBuilder();
                foreach (char c in text)
                {
                    var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                    {
                        stringBuilder.Append(c);
                    }
                }
                return stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Handles Chinese characters by adding spaces around them.
        /// </summary>
        public static string HandleChineseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                int codePoint = char.ConvertToUtf32(text, i);
                if (IsChineseChar(codePoint))
                {
                    sb.Append(' ');
                    sb.Append(char.ConvertFromUtf32(codePoint));
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(text[i]);
                }

                if (char.IsHighSurrogate(text[i]))
                {
                    i++; // Skip the low surrogate
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Cleans text by removing control characters and normalizing whitespace characters.
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                // Filter out NULL, REPLACEMENT CHARACTER, and control characters
                if (c == '\u0000' || c == '\uFFFD' || IsControl(c))
                {
                    continue;
                }

                // Replace whitespace characters with a single space
                if (IsWhitespace(c))
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
            }

            // Clean up multiple spaces and trim
            string result = sb.ToString();
            result = Regex.Replace(result, @"\s+", " "); // Use Regex for multiple spaces
            return result.Trim();
        }

        internal static IEnumerable<(int Offset, int Length)> SplitByWhitespace(string text)
        {
            var results = new List<(int, int)>();
            var matches = WhitespaceRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                results.Add((match.Index, match.Length));
            }
            
            return results;
        }

        internal static IEnumerable<(int Offset, int Length)> SplitByRegex(string text, Regex regex, string behavior = "removed", bool invert = false)
        {
            var results = new List<(int, int)>();
            var matches = regex.Matches(text);
            
            if (invert)
            {
                // Split on non-matches (text between matches)
                var currentOffset = 0;
                foreach (Match match in matches)
                {
                    if (match.Index > currentOffset)
                    {
                        results.Add((currentOffset, match.Index - currentOffset));
                    }
                    currentOffset = match.Index + match.Length;
                }
                
                if (currentOffset < text.Length)
                {
                    results.Add((currentOffset, text.Length - currentOffset));
                }
            }
            else
            {
                // Handle different behaviors for split patterns
                switch (behavior.ToLowerInvariant())
                {
                    case "removed":
                        // Split on matches, don't include delimiters
                        var currentPos = 0;
                        foreach (Match match in matches)
                        {
                            if (match.Index > currentPos)
                            {
                                results.Add((currentPos, match.Index - currentPos));
                            }
                            currentPos = match.Index + match.Length;
                        }
                        if (currentPos < text.Length)
                        {
                            results.Add((currentPos, text.Length - currentPos));
                        }
                        break;
                        
                    case "isolated":
                        // Include delimiters as separate tokens
                        var pos = 0;
                        foreach (Match match in matches)
                        {
                            if (match.Index > pos)
                            {
                                results.Add((pos, match.Index - pos));
                            }
                            results.Add((match.Index, match.Length));
                            pos = match.Index + match.Length;
                        }
                        if (pos < text.Length)
                        {
                            results.Add((pos, text.Length - pos));
                        }
                        break;
                        
                    case "merged_with_previous":
                        // Merge delimiters with previous tokens
                        var prevEnd = 0;
                        foreach (Match match in matches)
                        {
                            if (match.Index + match.Length <= text.Length)
                            {
                                results.Add((prevEnd, match.Index + match.Length - prevEnd));
                                prevEnd = match.Index + match.Length;
                            }
                        }
                        if (prevEnd < text.Length)
                        {
                            results.Add((prevEnd, text.Length - prevEnd));
                        }
                        break;
                        
                    case "merged_with_next":
                        // Merge delimiters with next tokens
                        var startPos = 0;
                        for (int i = 0; i < matches.Count; i++)
                        {
                            var match = matches[i];
                            var nextMatch = i + 1 < matches.Count ? matches[i + 1] : null;
                            var endPos = nextMatch?.Index ?? text.Length;
                            
                            if (endPos > startPos)
                            {
                                results.Add((startPos, endPos - startPos));
                                startPos = endPos;
                            }
                            else if (nextMatch == null && startPos < text.Length) // Handle case where last token is not followed by a delimiter
                            {
                                results.Add((startPos, text.Length - startPos));
                            }
                        }
                        break;
                        
                    case "contiguous":
                        // Group contiguous matches together
                        if (matches.Count > 0)
                        {
                            var start = matches[0].Index;
                            var end = matches[0].Index + matches[0].Length;
                            var beforeStart = 0;
                            
                            if (start > 0)
                            {
                                results.Add((beforeStart, start));
                            }
                            
                            for (int i = 1; i < matches.Count; i++)
                            {
                                if (matches[i].Index == end)
                                {
                                    // Contiguous, extend
                                    end = matches[i].Index + matches[i].Length;
                                }
                                else
                                {
                                    // Gap, finalize current group
                                    results.Add((start, end - start));
                                    
                                    // Add gap if any
                                    if (matches[i].Index > end)
                                    {
                                        results.Add((end, matches[i].Index - end));
                                    }
                                    
                                    start = matches[i].Index;
                                    end = matches[i].Index + matches[i].Length;
                                }
                            }
                            
                            results.Add((start, end - start));
                            
                            if (end < text.Length)
                            {
                                results.Add((end, text.Length - end));
                            }
                        }
                        else
                        {
                            results.Add((0, text.Length));
                        }
                        break;
                        
                    default:
                        // Default to "isolated" behavior
                        goto case "isolated";
                }
            }
            
            // Filter out empty results
            return results.Where(r => r.Item2 > 0);
        }

        internal static IEnumerable<(int Offset, int Length)> SplitByDelimiter(string text, string delimiter, string behavior = "removed")
        {
            var results = new List<(int, int)>();
            
            if (string.IsNullOrEmpty(delimiter))
            {
                return new List<(int, int)> { (0, text.Length) };
            }
            
            var parts = text.Split(new[] { delimiter }, StringSplitOptions.None);
            var currentOffset = 0;
            
            switch (behavior.ToLowerInvariant())
            {
                case "removed":
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Length > 0)
                        {
                            results.Add((currentOffset, parts[i].Length));
                        }
                        currentOffset += parts[i].Length;
                        
                        if (i < parts.Length - 1)
                        {
                            currentOffset += delimiter.Length;
                        }
                    }
                    break;
                case "merged_with_next":
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var partLength = parts[i].Length;
                        var tokenLength = partLength;
                        
                        if (i < parts.Length - 1)
                        {
                            tokenLength += delimiter.Length; // Include delimiter with the current part
                        }
                        
                        if (tokenLength > 0)
                        {
                            results.Add((currentOffset, tokenLength));
                        }
                        currentOffset += tokenLength;
                    }
                    break;
                default:
                    // Fallback to removed if behavior is not recognized
                    goto case "removed";
            }
            
            return results.Where(r => r.Item2 > 0); // Filter out empty results
        }

        /// <summary>
        /// Creates a Regex from a pattern string, handling tokenizers.Regex wrapper format
        /// </summary>
        /// <param name="pattern">Pattern string or tokenizers.Regex format</param>
        /// <returns>Compiled Regex object</returns>
        internal static Regex CreateRegexFromPattern(string pattern)
        {
            // Check if pattern is in tokenizers.Regex wrapper format
            if (pattern.StartsWith("Regex(") && pattern.EndsWith(")"))
            {
                // Extract the actual regex pattern from tokenizers.Regex("pattern") format
                var startIndex = pattern.IndexOf("\"") + 1;
                var endIndex = pattern.LastIndexOf("\"");
                if (startIndex > 0 && endIndex > startIndex)
                {
                    var regexPattern = pattern.Substring(startIndex, endIndex - startIndex);
                    return new Regex(regexPattern, RegexOptions.Compiled);
                }
            }
            
            // Treat as regular string pattern - try as regex first, fallback to literal
            try
            {
                return new Regex(pattern, RegexOptions.Compiled);
            }
            catch (ArgumentException)
            {
                // If pattern is not a valid regex, escape it to treat as literal string
                return new Regex(Regex.Escape(pattern), RegexOptions.Compiled);
            }
        }
    }
}
