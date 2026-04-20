using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    internal sealed class TokenizerJsonPipeline
    {
        public TokenizerJsonPipeline(
            string tokenizerJsonPath,
            string name,
            JObject root,
            JObject? tokenizerConfigRoot,
            TokenizerBackendType backendType,
            IReadOnlyList<TokenizerJsonAddedToken> addedTokens,
            Truncation? truncation,
            Padding? padding,
            NormalizerConfig? normalizerConfig,
            PreTokenizerConfig? preTokenizerConfig,
            PostProcessorConfig? postProcessorConfig,
            DecoderConfig? decoderConfig,
            INormalizer? normalizer,
            IPreTokenizer? preTokenizer,
            IPostProcessor? postProcessor,
            IDecoder? decoder,
            string? sentencePieceModelPath,
            bool applySentencePieceIdOffset,
            bool addDummyPrefixForSentencePieceBpe)
        {
            TokenizerJsonPath = tokenizerJsonPath;
            Name = name;
            Root = root;
            TokenizerConfigRoot = tokenizerConfigRoot;
            BackendType = backendType;
            AddedTokens = addedTokens;
            Truncation = truncation;
            Padding = padding;
            NormalizerConfig = normalizerConfig;
            PreTokenizerConfig = preTokenizerConfig;
            PostProcessorConfig = postProcessorConfig;
            DecoderConfig = decoderConfig;
            Normalizer = normalizer;
            PreTokenizer = preTokenizer;
            PostProcessor = postProcessor;
            Decoder = decoder;
            SentencePieceModelPath = sentencePieceModelPath;
            ApplySentencePieceIdOffset = applySentencePieceIdOffset;
            AddDummyPrefixForSentencePieceBpe = addDummyPrefixForSentencePieceBpe;
        }

        public string TokenizerJsonPath { get; }

        public string Name { get; }

        public JObject Root { get; }

        public JObject? TokenizerConfigRoot { get; }

        public TokenizerBackendType BackendType { get; }

    public IReadOnlyList<TokenizerJsonAddedToken> AddedTokens { get; }

    public Truncation? Truncation { get; }

    public Padding? Padding { get; }

        public NormalizerConfig? NormalizerConfig { get; }

        public PreTokenizerConfig? PreTokenizerConfig { get; }

        public PostProcessorConfig? PostProcessorConfig { get; }

        public DecoderConfig? DecoderConfig { get; }

        public INormalizer? Normalizer { get; }

        public IPreTokenizer? PreTokenizer { get; }

        public IPostProcessor? PostProcessor { get; }

        public IDecoder? Decoder { get; }

        public string? SentencePieceModelPath { get; }

        public bool ApplySentencePieceIdOffset { get; }

        public bool AddDummyPrefixForSentencePieceBpe { get; }
    }

    internal sealed class TokenizerJsonAddedToken
    {
        public TokenizerJsonAddedToken(int id, string content, bool singleWord, bool lStrip, bool rStrip, bool normalized, bool special)
        {
            Id = id;
            Content = content;
            SingleWord = singleWord;
            LStrip = lStrip;
            RStrip = rStrip;
            Normalized = normalized;
            Special = special;
        }

        public int Id { get; }

        public string Content { get; }

        public bool SingleWord { get; }

        public bool LStrip { get; }

        public bool RStrip { get; }

        public bool Normalized { get; }

        public bool Special { get; }
    }

    public sealed class Truncation
    {
        public Truncation(string? direction, int maxLength, string? strategy, int stride)
        {
            Direction = direction;
            MaxLength = maxLength;
            Strategy = strategy;
            Stride = stride;
        }

        public string? Direction { get; }

        public int MaxLength { get; }

        public string? Strategy { get; }

        public int Stride { get; }
    }

    public sealed class Padding
    {
        public Padding(string? strategy, string? direction, int? length, int padId, int padTypeId, string? padToken, int? padToMultipleOf)
        {
            Strategy = strategy;
            Direction = direction;
            Length = length;
            PadId = padId;
            PadTypeId = padTypeId;
            PadToken = padToken;
            PadToMultipleOf = padToMultipleOf;
        }

        public string? Strategy { get; }

        public string? Direction { get; }

        public int? Length { get; }

        public int PadId { get; }

        public int PadTypeId { get; }

        public string? PadToken { get; }

        public int? PadToMultipleOf { get; }
    }
}