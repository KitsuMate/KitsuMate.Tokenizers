using System;
using System.Text;
using System.Globalization; // For CharUnicodeInfo

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// NMT (Neural Machine Translation) normalizer, matching the Rust implementation.
    /// This involves filtering specific control characters and normalizing various whitespace characters.
    /// </summary>
    public class NmtNormalizer : INormalizer
    {
        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return DoNmt(original.AsSpan()).ToString();
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            return DoNmt(original).ToString();
        }

        private ReadOnlySpan<char> DoNmt(ReadOnlySpan<char> original)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in original)
            {
                uint charCode = c;

                // Filter out specific ASCII control characters
                if ((charCode >= 0x0001 && charCode <= 0x0008) ||
                    charCode == 0x000B ||
                    (charCode >= 0x000E && charCode <= 0x001F) ||
                    charCode == 0x007F ||
                    charCode == 0x008F ||
                    charCode == 0x009F)
                {
                    continue;
                }

                // Map other specific whitespace characters to a regular space
                switch (charCode)
                {
                    case 0x0009: // Horizontal Tab
                    case 0x000A: // Line Feed
                    case 0x000C: // Form Feed
                    case 0x000D: // Carriage Return
                    case 0x1680: // Ogham Space Mark
                    case 0x200B: // Zero Width Space
                    case 0x200C: // Zero Width Non-Joiner
                    case 0x200D: // Zero Width Joiner
                    case 0x200E: // Left-to-Right Mark
                    case 0x200F: // Right-to-Left Mark
                    case 0x2028: // Line Separator
                    case 0x2029: // Paragraph Separator
                    case 0x2581: // Lower One Eighth Block (used in some tokenizers for space)
                    case 0xFEFF: // Zero Width No-Break Space (Byte Order Mark)
                    case 0xFFFD: // Replacement Character
                        sb.Append(' ');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString().AsSpan();
        }
    }
}
