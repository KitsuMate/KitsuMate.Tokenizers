namespace KitsuMate.Tokenizers.PreTokenizers.Utils
{
    internal static class ScriptUtils
    {
        internal static Script GetScript(char c)
        {
            // This is a simplified mapping. A full implementation would require a comprehensive Unicode script database.
            // For now, we'll try to mimic the Rust behavior based on common use cases.
            if (char.IsWhiteSpace(c)) return Script.Any;
            if (char.IsLetter(c))
            {
                // Basic check for common scripts
                if (c >= 0x4E00 && c <= 0x9FFF) return Script.Han; // CJK Unified Ideographs
                if (c >= 0x3040 && c <= 0x309F) return Script.Hiragana;
                if (c >= 0x30A0 && c <= 0x30FF) return Script.Katakana;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return Script.Latin;
            }
            // Handle specific fixed_script cases from Rust
            if (c == 0x30FC) return Script.Han; // Long vowel mark in Japanese

            // Fallback for other characters (punctuation, numbers, symbols)
            return Script.Common;
        }

        internal static Script FixedScript(char c)
        {
            var rawScript = GetScript(c);
            if (c == 0x30FC) return Script.Han; // Long vowel mark in Japanese
            if (c == ' ') return Script.Any; // Space is Any

            switch (rawScript)
            {
                case Script.Hiragana:
                case Script.Katakana:
                    return Script.Han;
                default:
                    return rawScript;
            }
        }
    }
}
