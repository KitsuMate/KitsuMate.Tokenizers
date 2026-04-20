using System;
using KitsuMate.Tokenizers; // For NormalizerConfig

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Normalizer that applies a sequence of other normalizers.
    /// </summary>
    public class SequenceNormalizer : INormalizer
    {
        private readonly INormalizer[] _normalizers;

        public SequenceNormalizer(NormalizerConfig config, Func<NormalizerConfig, INormalizer> normalizerFactory)
        {
            if (config == null || config.Normalizers == null)
            {
                _normalizers = new INormalizer[0];
                return;
            }

            _normalizers = new INormalizer[config.Normalizers.Count];
            for (int i = 0; i < config.Normalizers.Count; i++)
            {
                _normalizers[i] = normalizerFactory(config.Normalizers[i]);
            }
        }

        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            string result = original;
            foreach (var normalizer in _normalizers)
            {
                result = normalizer.Normalize(result);
            }
            return result;
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
                return string.Empty;

            string result = original.ToString();
            foreach (var normalizer in _normalizers)
            {
                result = normalizer.Normalize(result);
            }
            return result;
        }
    }
}
