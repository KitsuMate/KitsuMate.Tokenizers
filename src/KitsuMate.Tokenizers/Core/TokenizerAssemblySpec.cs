using System;
using System.Collections.Generic;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;

namespace KitsuMate.Tokenizers.Core
{
    internal sealed class TokenizerAssemblySpec
    {
        public TokenizerAssemblySpec(
            ITokenizerModel model,
            Func<global::KitsuMate.Tokenizers.Tokenizer, string, int, EncodingResult> encodeCore,
            Func<global::KitsuMate.Tokenizers.Tokenizer, IEnumerable<int>, bool, string?> decodeCore)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            EncodeCore = encodeCore ?? throw new ArgumentNullException(nameof(encodeCore));
            DecodeCore = decodeCore ?? throw new ArgumentNullException(nameof(decodeCore));
        }

        public ITokenizerModel Model { get; }

        public INormalizer? Normalizer { get; init; }

        public IPreTokenizer? PreTokenizer { get; init; }

        public IPostProcessor? PostProcessor { get; init; }

        public IDecoder? Decoder { get; init; }

        public Func<global::KitsuMate.Tokenizers.Tokenizer, string, int, EncodingResult> EncodeCore { get; }

        public Func<global::KitsuMate.Tokenizers.Tokenizer, IEnumerable<int>, bool, string?> DecodeCore { get; }

        public Func<global::KitsuMate.Tokenizers.Tokenizer, EncodingResult, bool, int, EncodingResult>? FinalizeSingle { get; init; }

        public Func<global::KitsuMate.Tokenizers.Tokenizer, EncodingResult, EncodingResult, bool, int, EncodingResult>? FinalizePair { get; init; }

        public Truncation? Truncation { get; init; }

        public Padding? Padding { get; init; }

        public Func<global::KitsuMate.Tokenizers.Tokenizer, bool, int>? AddedTokensResolver { get; init; }

        public global::KitsuMate.Tokenizers.Tokenizer Assemble()
        {
            return new global::KitsuMate.Tokenizers.Tokenizer(
                Model,
                Normalizer,
                PreTokenizer,
                PostProcessor,
                Decoder,
                EncodeCore,
                DecodeCore,
                FinalizeSingle,
                FinalizePair,
                Truncation,
                Padding,
                AddedTokensResolver);
        }
    }
}