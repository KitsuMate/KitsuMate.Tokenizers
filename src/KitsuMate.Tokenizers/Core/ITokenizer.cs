using System.Collections.Generic;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Primary public tokenizer contract for the native runtime.
    /// </summary>
    public interface ITokenizer
    {
        string Name { get; }

        TokenizerBackendType BackendType { get; }

        bool SupportsDecode { get; }

        ITokenizerModel? Model { get; }

        INormalizer? Normalizer { get; set; }

        IPreTokenizer? PreTokenizer { get; set; }

        IPostProcessor? PostProcessor { get; set; }

        IDecoder? Decoder { get; set; }

        Truncation? Truncation { get; set; }

        Padding? Padding { get; set; }

        IReadOnlyList<int> EncodeToIds(string text, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue);

        IReadOnlyList<int> EncodePairToIds(string text, string pair, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue);

        string? Decode(IEnumerable<int> ids, bool skipSpecialTokens = false);

        EncodingResult Encode(string text, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue);

        EncodingResult Encode(string text, TokenizerEncodeOptions? options);

        EncodingResult EncodePair(string text, string pair, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue);

        IReadOnlyList<EncodingResult> EncodeBatch(IEnumerable<string> texts, TokenizerEncodeOptions? options = null);

        int CountTokens(string text, bool addSpecialTokens = true);
    }
}