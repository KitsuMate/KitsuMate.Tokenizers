using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using KitsuMate.Tokenizers.Core;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using KitsuMate.Tokenizers.Remote;

namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Primary public tokenizer facade over the native runtime.
    /// </summary>
    public sealed class Tokenizer : ITokenizer
    {
        private readonly string _name;
        private readonly TokenizerBackendType _backendType;
        private readonly bool _supportsDecode;
        private readonly ITokenizerModel? _model;
        private INormalizer? _normalizer;
        private IPreTokenizer? _preTokenizer;
        private IPostProcessor? _postProcessor;
        private IDecoder? _decoder;
        private readonly Func<Tokenizer, string, int, EncodingResult> _encodeCore;
        private readonly Func<Tokenizer, IEnumerable<int>, bool, string?> _decodeCore;
        private readonly Func<Tokenizer, EncodingResult, bool, int, EncodingResult>? _finalizeSingle;
        private readonly Func<Tokenizer, EncodingResult, EncodingResult, bool, int, EncodingResult>? _finalizePair;
        private Truncation? _truncation;
        private Padding? _padding;
        private readonly Func<Tokenizer, bool, int>? _addedTokensResolver;

        internal Tokenizer(
            ITokenizerModel model,
            INormalizer? normalizer,
            IPreTokenizer? preTokenizer,
            IPostProcessor? postProcessor,
            IDecoder? decoder,
            Func<Tokenizer, string, int, EncodingResult> encodeCore,
            Func<Tokenizer, IEnumerable<int>, bool, string?> decodeCore,
            Func<Tokenizer, EncodingResult, bool, int, EncodingResult>? finalizeSingle = null,
            Func<Tokenizer, EncodingResult, EncodingResult, bool, int, EncodingResult>? finalizePair = null,
            Truncation? truncation = null,
            Padding? padding = null,
            Func<Tokenizer, bool, int>? addedTokensResolver = null)
            : this(
                model?.Name ?? throw new ArgumentNullException(nameof(model)),
                model.BackendType,
                model.SupportsDecode,
                model,
                normalizer,
                preTokenizer,
                postProcessor,
                decoder,
                encodeCore,
                decodeCore,
                finalizeSingle,
                finalizePair,
                truncation,
                padding,
                addedTokensResolver)
        {
        }

        internal Tokenizer(
            string name,
            TokenizerBackendType backendType,
            bool supportsDecode,
            ITokenizerModel? model,
            INormalizer? normalizer,
            IPreTokenizer? preTokenizer,
            IPostProcessor? postProcessor,
            IDecoder? decoder,
            Func<Tokenizer, string, int, EncodingResult> encodeCore,
            Func<Tokenizer, IEnumerable<int>, bool, string?> decodeCore,
            Func<Tokenizer, EncodingResult, bool, int, EncodingResult>? finalizeSingle = null,
            Func<Tokenizer, EncodingResult, EncodingResult, bool, int, EncodingResult>? finalizePair = null,
            Truncation? truncation = null,
            Padding? padding = null,
            Func<Tokenizer, bool, int>? addedTokensResolver = null)
        {
            _name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Tokenizer name cannot be null or empty.", nameof(name)) : name;
            _backendType = backendType;
            _supportsDecode = supportsDecode;
            _model = model;
            _normalizer = normalizer;
            _preTokenizer = preTokenizer;
            _postProcessor = postProcessor;
            _decoder = decoder;
            _encodeCore = encodeCore ?? throw new ArgumentNullException(nameof(encodeCore));
            _decodeCore = decodeCore ?? throw new ArgumentNullException(nameof(decodeCore));
            _finalizeSingle = finalizeSingle;
            _finalizePair = finalizePair;
            _truncation = truncation;
            _padding = padding;
            _addedTokensResolver = addedTokensResolver;
        }

        public string Name => _name;

        public TokenizerBackendType BackendType => _backendType;

        public bool SupportsDecode => _supportsDecode;

        public ITokenizerModel? Model => _model;

        public INormalizer? Normalizer
        {
            get => _normalizer;
            set => _normalizer = value;
        }

        public IPreTokenizer? PreTokenizer
        {
            get => _preTokenizer;
            set => _preTokenizer = value;
        }

        public IPostProcessor? PostProcessor
        {
            get => _postProcessor;
            set => _postProcessor = value;
        }

        public IDecoder? Decoder
        {
            get => _decoder;
            set => _decoder = value;
        }

        public Truncation? Truncation
        {
            get => _truncation;
            set => _truncation = value;
        }

        public Padding? Padding
        {
            get => _padding;
            set => _padding = value;
        }

        public static Tokenizer Load(string pathOrUrl, HttpMessageInvoker? httpClient = null, TokenizerLoadOptions? loadOptions = null)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                throw new ArgumentException("Path or URL cannot be null or empty", nameof(pathOrUrl));
            }

            if (Directory.Exists(pathOrUrl))
            {
                return FromLocal(pathOrUrl, loadOptions);
            }

            if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                if (uri.Host.IndexOf("huggingface.co", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                    if (pathParts.Length >= 2)
                    {
                        pathOrUrl = $"{pathParts[0]}/{pathParts[1]}";
                    }
                }
            }

            return FromPretrained(pathOrUrl, httpClient, revision: null, loadOptions: loadOptions);
        }

        public static Tokenizer FromPretrained(string modelId, HttpMessageInvoker? httpClient = null, string? revision = null, TokenizerLoadOptions? loadOptions = null)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));
            }

            var downloader = new RemoteDownloader(httpClient);
            try
            {
                var localPath = downloader.DownloadTokenizerFilesAsync(modelId, revision)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                return FromLocal(localPath, loadOptions);
            }
            finally
            {
                downloader.Dispose();
            }
        }

        public static Tokenizer FromLocal(string modelDirectory, TokenizerLoadOptions? loadOptions = null)
        {
            return RequireConcrete(TokenizerLoader.FromLocal(modelDirectory, loadOptions));
        }

        public static Tokenizer FromTokenizerJson(string tokenizerJsonPath, TokenizerLoadOptions? loadOptions = null)
        {
            if (!File.Exists(tokenizerJsonPath))
            {
                throw new FileNotFoundException($"File not found: {tokenizerJsonPath}");
            }

            var modelDirectory = Path.GetDirectoryName(tokenizerJsonPath);
            if (!string.IsNullOrWhiteSpace(modelDirectory) &&
                string.Equals(Path.GetFileName(tokenizerJsonPath), "tokenizer.json", StringComparison.OrdinalIgnoreCase))
            {
                return FromLocal(modelDirectory, loadOptions);
            }

            return RequireConcrete(new TokenizerFactory().CreateFromTokenizerJson(tokenizerJsonPath));
        }

        public static Tokenizer FromTokenizerJson(byte[] tokenizerJson, byte[]? sentencePieceModel = null, byte[]? tokenizerConfigJson = null)
        {
            return RequireConcrete(new TokenizerFactory().CreateFromTokenizerJson(tokenizerJson, sentencePieceModel, tokenizerConfigJson));
        }

        public static Tokenizer FromTokenizerJson(Stream tokenizerJsonStream, Stream? sentencePieceModelStream = null, Stream? tokenizerConfigStream = null)
        {
            return RequireConcrete(new TokenizerFactory().CreateFromTokenizerJson(tokenizerJsonStream, sentencePieceModelStream, tokenizerConfigStream));
        }

        public static Tokenizer Create(ITokenizerModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return TokenizerModelRuntimeFactory.Create(model).Assemble();
        }

        public static Tokenizer CreateWordPiece(string vocabPath, WordPieceTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateWordPieceRuntime(vocabPath, options));
        }

        public static Tokenizer CreateWordPiece(byte[] vocab, WordPieceTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateWordPieceRuntime(vocab, options));
        }

        public static Tokenizer CreateWordPiece(Stream vocabStream, WordPieceTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateWordPieceRuntime(vocabStream, options));
        }

        public static Tokenizer CreateBpe(string vocabPath, string mergesPath, BpeTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateBpeRuntime(vocabPath, mergesPath, options));
        }

        public static Tokenizer CreateBpe(byte[] vocab, byte[] merges, BpeTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateBpeRuntime(vocab, merges, options));
        }

        public static Tokenizer CreateBpe(Stream vocabStream, Stream mergesStream, BpeTokenizerOptions? options = null)
        {
            return RequireConcrete(TokenizerFactory.CreateBpeRuntime(vocabStream, mergesStream, options));
        }

        public static Tokenizer CreateTiktoken(string vocabPath, string? encodingName = null)
        {
            return RequireConcrete(TokenizerFactory.CreateTiktokenRuntime(vocabPath, encodingName));
        }

        public static Tokenizer CreateTiktoken(byte[] vocab, string? encodingName = null)
        {
            return RequireConcrete(TokenizerFactory.CreateTiktokenRuntime(vocab, encodingName));
        }

        public static Tokenizer CreateTiktoken(Stream vocabStream, string? encodingName = null)
        {
            return RequireConcrete(TokenizerFactory.CreateTiktokenRuntime(vocabStream, encodingName));
        }

        public static Tokenizer CreateSentencePieceUnigram(string modelPath, bool applyIdOffset = false)
        {
            return RequireConcrete(TokenizerFactory.CreateSentencePieceUnigramRuntime(modelPath, applyIdOffset));
        }

        public static Tokenizer CreateSentencePieceUnigram(byte[] model, bool applyIdOffset = false)
        {
            return RequireConcrete(new TokenizerFactory().CreateSentencePieceUnigram(model, applyIdOffset));
        }

        public static Tokenizer CreateSentencePieceUnigram(Stream modelStream, bool applyIdOffset = false)
        {
            return RequireConcrete(new TokenizerFactory().CreateSentencePieceUnigram(modelStream, applyIdOffset));
        }

        public static Tokenizer CreateSentencePieceBpe(string modelPath, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            return RequireConcrete(TokenizerFactory.CreateSentencePieceBpeRuntime(modelPath, applyIdOffset, addDummyPrefix));
        }

        public static Tokenizer CreateSentencePieceBpe(byte[] model, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            return RequireConcrete(new TokenizerFactory().CreateSentencePieceBpe(model, applyIdOffset, addDummyPrefix));
        }

        public static Tokenizer CreateSentencePieceBpe(Stream modelStream, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            return RequireConcrete(new TokenizerFactory().CreateSentencePieceBpe(modelStream, applyIdOffset, addDummyPrefix));
        }

        public int? TokenToId(string token)
        {
            return _model?.TokenToId(token);
        }

        public string? IdToToken(int id)
        {
            return _model?.IdToToken(id);
        }

        public IReadOnlyList<int> EncodeToIds(string text, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue)
        {
            return Encode(text, addSpecialTokens, maxTokenCount).Ids;
        }

        public IReadOnlyList<int> EncodePairToIds(string text, string pair, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue)
        {
            return EncodePair(text, pair, addSpecialTokens, maxTokenCount).Ids;
        }

        public string? Decode(IEnumerable<int> ids, bool skipSpecialTokens = false)
        {
            return _decodeCore(this, ids, skipSpecialTokens);
        }

        public EncodingResult Encode(string text, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var encoding = _encodeCore(this, text, maxTokenCount);
            ApplyConfiguredSingleTruncation(encoding, addSpecialTokens);

            var finalized = _finalizeSingle != null
                ? _finalizeSingle(this, encoding, addSpecialTokens, maxTokenCount)
                : FinalizeSingleEncoding(encoding, addSpecialTokens, maxTokenCount, _postProcessor);

            ApplyConfiguredOutputSettings(finalized);
            return finalized;
        }

        public EncodingResult Encode(string text, TokenizerEncodeOptions? options)
        {
            if (options == null)
            {
                return Encode(text);
            }

            var encoding = Encode(text, options.AddSpecialTokens, int.MaxValue).Clone();

            if (options.MaxLength.HasValue && options.Truncation != TokenizerTruncationMode.None)
            {
                encoding.Truncate(options.MaxLength.Value, 0, ResolveTruncationDirection(options));
            }

            if (options.Padding != TokenizerPaddingMode.None && options.MaxLength.HasValue)
            {
                encoding.Pad(
                    options.MaxLength.Value,
                    ResolvePaddingDirection(options),
                    Padding?.PadId ?? 0,
                    Padding?.PadTypeId ?? 0,
                    string.IsNullOrWhiteSpace(Padding?.PadToken) ? "[PAD]" : Padding.PadToken!);
            }

            return encoding;
        }

        public EncodingResult EncodePair(string text, string pair, bool addSpecialTokens = true, int maxTokenCount = int.MaxValue)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }

            var first = _encodeCore(this, text, maxTokenCount);
            var second = _encodeCore(this, pair, maxTokenCount);

            ApplyConfiguredPairTruncation(first, second, addSpecialTokens);

            first.TypeIds = Enumerable.Repeat(0, first.Length).ToList();
            first.SetSequenceId(0);
            second.TypeIds = Enumerable.Repeat(1, second.Length).ToList();
            second.SetSequenceId(1);

            EncodingResult finalized;
            if (_finalizePair != null)
            {
                finalized = _finalizePair(this, first, second, addSpecialTokens, maxTokenCount);
                ApplyConfiguredOutputSettings(finalized);
                return finalized;
            }

            finalized = _postProcessor != null
                ? _postProcessor.Process(new List<EncodingResult> { first, second }, addSpecialTokens)
                : EncodingResult.Merge(new[] { first, second }, false);

            TruncateEncoding(finalized, maxTokenCount);
            ApplyConfiguredOutputSettings(finalized);
            return finalized;
        }

        public IReadOnlyList<EncodingResult> EncodeBatch(IEnumerable<string> texts, TokenizerEncodeOptions? options = null)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            options ??= new TokenizerEncodeOptions();

            var singleOptions = new TokenizerEncodeOptions
            {
                AddSpecialTokens = options.AddSpecialTokens,
                MaxLength = options.MaxLength,
                Truncation = options.Truncation,
                TruncationSide = options.TruncationSide,
                Padding = TokenizerPaddingMode.None,
                PaddingSide = options.PaddingSide,
                ReturnAttentionMask = options.ReturnAttentionMask,
                ReturnTokenTypeIds = options.ReturnTokenTypeIds,
            };

            var encodings = texts
                .Select(text => Encode(text, singleOptions))
                .ToList();

            if (options.Padding == TokenizerPaddingMode.Longest || (options.Padding != TokenizerPaddingMode.None && !options.MaxLength.HasValue))
            {
                var longestLength = encodings.Count == 0 ? 0 : encodings.Max(encoding => encoding.Length);
                foreach (var encoding in encodings)
                {
                    encoding.Pad(
                        longestLength,
                        ResolvePaddingDirection(options),
                        Padding?.PadId ?? 0,
                        Padding?.PadTypeId ?? 0,
                        string.IsNullOrWhiteSpace(Padding?.PadToken) ? "[PAD]" : Padding.PadToken!);
                }
            }
            else if (options.Padding == TokenizerPaddingMode.MaxLength && options.MaxLength.HasValue)
            {
                foreach (var encoding in encodings)
                {
                    encoding.Pad(
                        options.MaxLength.Value,
                        ResolvePaddingDirection(options),
                        Padding?.PadId ?? 0,
                        Padding?.PadTypeId ?? 0,
                        string.IsNullOrWhiteSpace(Padding?.PadToken) ? "[PAD]" : Padding.PadToken!);
                }
            }

            return encodings;
        }

        public int CountTokens(string text, bool addSpecialTokens = true)
        {
            return EncodeToIds(text, addSpecialTokens).Count;
        }

        internal static EncodingResult FinalizeSingleEncoding(EncodingResult encoding, bool addSpecialTokens, int maxTokenCount, IPostProcessor? postProcessor)
        {
            encoding.SetSequenceId(0);

            if (postProcessor != null)
            {
                encoding = postProcessor.Process(new List<EncodingResult> { encoding }, addSpecialTokens);
            }

            TruncateEncoding(encoding, maxTokenCount);
            return encoding;
        }

        internal static void TruncateEncoding(EncodingResult encoding, int maxTokenCount)
        {
            if (encoding.Length <= maxTokenCount)
            {
                return;
            }

            encoding.Ids = encoding.Ids.Take(maxTokenCount).ToList();
            encoding.Tokens = encoding.Tokens.Take(maxTokenCount).ToList();
            encoding.TypeIds = encoding.TypeIds.Take(maxTokenCount).ToList();
            encoding.AttentionMask = encoding.AttentionMask.Take(maxTokenCount).ToList();
            encoding.SpecialTokensMask = encoding.SpecialTokensMask.Take(maxTokenCount).ToList();
            encoding.Words = encoding.Words.Take(maxTokenCount).ToList();
            encoding.Offsets = encoding.Offsets.Take(maxTokenCount).ToList();

            var updatedRanges = new Dictionary<int, (int Start, int End)>();
            foreach (var entry in encoding.SequenceRanges)
            {
                if (entry.Value.Start >= maxTokenCount)
                {
                    continue;
                }

                updatedRanges[entry.Key] = (entry.Value.Start, Math.Min(entry.Value.End, maxTokenCount));
            }

            encoding.SequenceRanges = updatedRanges;
        }

        private static Tokenizer RequireConcrete(ITokenizer tokenizer)
        {
            if (tokenizer is Tokenizer concrete)
            {
                return concrete;
            }

            throw new InvalidOperationException($"Expected a {nameof(Tokenizer)} runtime but received '{tokenizer.GetType().FullName}'.");
        }

        private void ApplyConfiguredSingleTruncation(EncodingResult encoding, bool addSpecialTokens)
        {
            var targetLength = ResolveContentTruncationLength(addSpecialTokens, isPair: false);
            if (!targetLength.HasValue || encoding.Length <= targetLength.Value)
            {
                return;
            }

            encoding.Truncate(targetLength.Value, _truncation!.Stride, NormalizeDirection(_truncation.Direction));
        }

        private void ApplyConfiguredPairTruncation(EncodingResult first, EncodingResult second, bool addSpecialTokens)
        {
            var targetLength = ResolveContentTruncationLength(addSpecialTokens, isPair: true);
            if (!targetLength.HasValue)
            {
                return;
            }

            var maxCombinedLength = targetLength.Value;
            var totalLength = first.Length + second.Length;
            if (totalLength <= maxCombinedLength)
            {
                return;
            }

            var overflow = totalLength - maxCombinedLength;
            var strategy = NormalizeValue(_truncation!.Strategy);
            var direction = NormalizeDirection(_truncation.Direction);

            switch (strategy)
            {
                case "onlyfirst":
                    first.Truncate(Math.Max(0, first.Length - overflow), _truncation.Stride, direction);
                    return;

                case "onlysecond":
                    second.Truncate(Math.Max(0, second.Length - overflow), _truncation.Stride, direction);
                    return;

                default:
                    while (first.Length + second.Length > maxCombinedLength)
                    {
                        if (first.Length >= second.Length && first.Length > 0)
                        {
                            first.Truncate(first.Length - 1, _truncation.Stride, direction);
                        }
                        else if (second.Length > 0)
                        {
                            second.Truncate(second.Length - 1, _truncation.Stride, direction);
                        }
                        else
                        {
                            break;
                        }
                    }

                    return;
            }
        }

        private int? ResolveContentTruncationLength(bool addSpecialTokens, bool isPair)
        {
            if (_truncation == null)
            {
                return null;
            }

            var targetLength = _truncation.MaxLength;
            if (addSpecialTokens)
            {
                targetLength -= GetAddedTokensCount(isPair);
            }

            return Math.Max(0, targetLength);
        }

        private int GetAddedTokensCount(bool isPair)
        {
            if (_addedTokensResolver != null)
            {
                return _addedTokensResolver(this, isPair);
            }

            return _postProcessor?.AddedTokens(isPair) ?? 0;
        }

        private void ApplyConfiguredOutputSettings(EncodingResult encoding)
        {
            if (_truncation != null && encoding.Length > _truncation.MaxLength)
            {
                encoding.Truncate(_truncation.MaxLength, _truncation.Stride, NormalizeDirection(_truncation.Direction));
            }

            if (_padding == null)
            {
                return;
            }

            var targetLength = _padding.Length ?? encoding.Length;
            if (_padding.PadToMultipleOf.HasValue && _padding.PadToMultipleOf.Value > 0)
            {
                var multiple = _padding.PadToMultipleOf.Value;
                targetLength = ((Math.Max(targetLength, encoding.Length) + multiple - 1) / multiple) * multiple;
            }

            if (targetLength <= encoding.Length)
            {
                return;
            }

            encoding.Pad(
                targetLength,
                NormalizeDirection(_padding.Direction),
                _padding.PadId,
                _padding.PadTypeId,
                string.IsNullOrWhiteSpace(_padding.PadToken) ? "[PAD]" : _padding.PadToken!);
        }

        private static string NormalizeDirection(string? direction)
        {
            return NormalizeValue(direction) == "left" ? "left" : "right";
        }

        private string ResolvePaddingDirection(TokenizerEncodeOptions options)
        {
            if (options.PaddingSide.HasValue)
            {
                return options.PaddingSide.Value == TokenizerSide.Left ? "left" : "right";
            }

            return NormalizeDirection(Padding?.Direction);
        }

        private string ResolveTruncationDirection(TokenizerEncodeOptions options)
        {
            if (options.TruncationSide.HasValue)
            {
                return options.TruncationSide.Value == TokenizerSide.Left ? "left" : "right";
            }

            return NormalizeDirection(Truncation?.Direction);
        }

        private static string NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
        }
    }
}