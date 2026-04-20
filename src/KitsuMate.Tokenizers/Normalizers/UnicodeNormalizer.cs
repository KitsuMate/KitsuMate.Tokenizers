using System;
using System.Text;

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer for standard Unicode normalization forms (NFD, NFC, NFKD, NFKC).
    /// </summary>
    public class UnicodeNormalizer : INormalizer
    {
        private readonly NormalizationForm _form;

        public UnicodeNormalizer(NormalizationForm form)
        {
            _form = form;
        }

        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            return original.Normalize(_form);
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            return original.ToString().Normalize(_form);
        }
    }
}
