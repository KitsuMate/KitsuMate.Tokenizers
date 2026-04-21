using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Concrete factory for the native tokenizer runtime.
    /// </summary>
    public sealed class TokenizerFactory
    {
        private readonly ITokenizerJsonSerializer _jsonSerializer;

        public TokenizerFactory(ITokenizerJsonSerializer? jsonSerializer = null)
        {
            _jsonSerializer = jsonSerializer ?? new DefaultTokenizerJsonSerializer();
        }

        public ITokenizer CreateFromTokenizerJson(string tokenizerJsonPath)
        {
            if (string.IsNullOrWhiteSpace(tokenizerJsonPath))
            {
                throw new ArgumentException("Tokenizer JSON path cannot be null or empty.", nameof(tokenizerJsonPath));
            }

            if (!File.Exists(tokenizerJsonPath))
            {
                throw new FileNotFoundException($"tokenizer.json not found: {tokenizerJsonPath}", tokenizerJsonPath);
            }

            var pipeline = ParseTokenizerJsonPipeline(tokenizerJsonPath);
            return Create(pipeline);
        }

        public ITokenizer CreateFromTokenizerJson(byte[] tokenizerJson, byte[]? sentencePieceModel = null, byte[]? tokenizerConfigJson = null)
        {
            if (tokenizerJson == null)
            {
                throw new ArgumentNullException(nameof(tokenizerJson));
            }

            using var tokenizerJsonStream = new MemoryStream(tokenizerJson, writable: false);
            Stream? sentencePieceModelStream = sentencePieceModel == null ? null : new MemoryStream(sentencePieceModel, writable: false);
            Stream? tokenizerConfigStream = tokenizerConfigJson == null ? null : new MemoryStream(tokenizerConfigJson, writable: false);
            using (sentencePieceModelStream)
            using (tokenizerConfigStream)
            {
                return CreateFromTokenizerJson(tokenizerJsonStream, sentencePieceModelStream, tokenizerConfigStream);
            }
        }

        public ITokenizer CreateFromTokenizerJson(Stream tokenizerJsonStream, Stream? sentencePieceModelStream = null, Stream? tokenizerConfigStream = null)
        {
            if (tokenizerJsonStream == null)
            {
                throw new ArgumentNullException(nameof(tokenizerJsonStream));
            }

            var root = ParseJsonObject(tokenizerJsonStream);
            var tokenizerConfigRoot = ParseOptionalJsonObject(tokenizerConfigStream);
            var pipeline = ParseTokenizerJsonPipeline(root, tokenizerConfigRoot: tokenizerConfigRoot);

            if (pipeline.BackendType == TokenizerBackendType.WordPiece || pipeline.BackendType == TokenizerBackendType.Bpe)
            {
                return Create(pipeline);
            }

            if (pipeline.BackendType == TokenizerBackendType.SentencePieceUnigram && sentencePieceModelStream != null)
            {
                return CreateSentencePieceUnigram(pipeline, sentencePieceModelStream, "sentencepiece");
            }

            if (pipeline.BackendType == TokenizerBackendType.SentencePieceBpe && sentencePieceModelStream != null)
            {
                return CreateSentencePieceBpe(pipeline, sentencePieceModelStream, "sentencepiece");
            }

            throw TokenizerModelRuntimeFactory.CreateUnsupportedBackendException(
                pipeline.Name,
                pipeline.BackendType,
                "In-memory tokenizer.json loading supports self-contained WordPiece and BPE payloads. SentencePiece tokenizer.json payloads also require the companion .model bytes or stream.");
        }

        public ITokenizer CreateWordPiece(string vocabPath, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(vocabPath, options);
        }

        public ITokenizer CreateWordPiece(byte[] vocab, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(vocab, options);
        }

        public ITokenizer CreateWordPiece(Stream vocabStream, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(vocabStream, options);
        }

        public ITokenizer CreateBpe(string vocabPath, string mergesPath, BpeTokenizerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(mergesPath))
            {
                throw new ArgumentException("Merges path cannot be null or empty.", nameof(mergesPath));
            }

            return CreateBpeRuntime(vocabPath, mergesPath, options);
        }

        public ITokenizer CreateBpe(byte[] vocab, byte[] merges, BpeTokenizerOptions? options = null)
        {
            return CreateBpeRuntime(vocab, merges, options);
        }

        public ITokenizer CreateBpe(Stream vocabStream, Stream mergesStream, BpeTokenizerOptions? options = null)
        {
            return CreateBpeRuntime(vocabStream, mergesStream, options);
        }

        public ITokenizer CreateTiktoken(string vocabPath, string? encodingName = null)
        {
            return CreateTiktokenRuntime(vocabPath, encodingName);
        }

        public ITokenizer CreateTiktoken(byte[] vocab, string? encodingName = null)
        {
            return CreateTiktokenRuntime(vocab, encodingName);
        }

        public ITokenizer CreateTiktoken(Stream vocabStream, string? encodingName = null)
        {
            return CreateTiktokenRuntime(vocabStream, encodingName);
        }

        public ITokenizer CreateSentencePieceUnigram(byte[] model, bool applyIdOffset = false)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            using var modelStream = new MemoryStream(model, writable: false);
            return CreateSentencePieceUnigram(modelStream, applyIdOffset);
        }

        public ITokenizer CreateSentencePieceUnigram(Stream modelStream, bool applyIdOffset = false)
        {
            if (modelStream == null)
            {
                throw new ArgumentNullException(nameof(modelStream));
            }

            return CreateSentencePieceUnigramRuntime(modelStream, applyIdOffset: applyIdOffset);
        }

        public ITokenizer CreateSentencePieceBpe(byte[] model, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            using var modelStream = new MemoryStream(model, writable: false);
            return CreateSentencePieceBpe(modelStream, applyIdOffset, addDummyPrefix);
        }

        public ITokenizer CreateSentencePieceBpe(Stream modelStream, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            if (modelStream == null)
            {
                throw new ArgumentNullException(nameof(modelStream));
            }

            return CreateSentencePieceBpeRuntime(modelStream, applyIdOffset: applyIdOffset, addDummyPrefix: addDummyPrefix);
        }

        internal static ITokenizer Create(TokenizerJsonPipeline pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            return pipeline.BackendType switch
            {
                TokenizerBackendType.WordPiece => CreateWordPiece(pipeline),
                TokenizerBackendType.Bpe => CreateBpe(pipeline),
                TokenizerBackendType.SentencePieceUnigram when !string.IsNullOrWhiteSpace(pipeline.SentencePieceModelPath)
                    => CreateSentencePieceUnigram(pipeline),
                TokenizerBackendType.SentencePieceBpe when !string.IsNullOrWhiteSpace(pipeline.SentencePieceModelPath)
                    => CreateSentencePieceBpe(pipeline),
                _ => throw TokenizerModelRuntimeFactory.CreateUnsupportedBackendException(
                    pipeline.Name,
                    pipeline.BackendType,
                    "Backend inference is implemented, but the concrete tokenizer runtime has not been ported yet."),
            };
        }

        internal static ITokenizer CreateWordPieceRuntime(string vocabPath, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(WordPieceModel.FromVocab(vocabPath, options));
        }

        internal static ITokenizer CreateWordPieceRuntime(byte[] vocab, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(WordPieceModel.FromBytes(vocab, options));
        }

        internal static ITokenizer CreateWordPieceRuntime(Stream vocabStream, WordPieceTokenizerOptions? options = null)
        {
            return CreateWordPieceRuntime(WordPieceModel.FromStream(vocabStream, options));
        }

        internal static ITokenizer CreateWordPieceRuntime(JObject root, JObject? tokenizerConfigRoot = null, WordPieceTokenizerOptions? options = null)
        {
            var effectiveOptions = options ?? WordPieceTokenizerConfigLoader.CreateOptions(root, tokenizerConfigRoot);
            return CreateWordPieceRuntime(WordPieceModel.FromTokenizerJson(root, effectiveOptions));
        }

        internal static ITokenizer CreateBpeRuntime(string vocabPath, string mergesPath, BpeTokenizerOptions? options = null)
        {
            return CreateBpeRuntime(BpeModel.FromFiles(vocabPath, mergesPath, options));
        }

        internal static ITokenizer CreateBpeRuntime(byte[] vocab, byte[] merges, BpeTokenizerOptions? options = null)
        {
            return CreateBpeRuntime(BpeModel.FromBytes(vocab, merges, options));
        }

        internal static ITokenizer CreateBpeRuntime(Stream vocabStream, Stream mergesStream, BpeTokenizerOptions? options = null)
        {
            return CreateBpeRuntime(BpeModel.FromStreams(vocabStream, mergesStream, options));
        }

        internal static ITokenizer CreateBpeRuntime(JObject root, JObject? tokenizerConfigRoot = null, BpeTokenizerOptions? options = null)
        {
            var model = BpeModel.FromTokenizerJson(root, tokenizerConfigRoot, options);
            var normalizer = TokenizerJsonComponentFactory.CreateNormalizer(root["normalizer"] as JObject);
            var preTokenizerConfig = TokenizerJsonComponentFactory.ParsePreTokenizerConfig(root["pre_tokenizer"] as JObject);
            var postProcessorConfig = TokenizerJsonComponentFactory.ParsePostProcessorConfig(root["post_processor"] as JObject);
            var preTokenizer = TokenizerJsonComponentFactory.CreatePreTokenizer(root["pre_tokenizer"] as JObject);
            var decoder = TokenizerJsonComponentFactory.CreateDecoder(root["decoder"] as JObject);
            var addedTokensById = TokenizerModelRuntimeFactory.ReadBpeAddedTokens(root, tokenizerConfigRoot, model);
            var postProcessor = TokenizerModelRuntimeFactory.CreateBpePostProcessor(postProcessorConfig, preTokenizerConfig, addedTokensById);
            TokenizerModelRuntimeFactory.ApplyBpeAddedTokenNormalization(addedTokensById, normalizer);
            return CreateBpeRuntime(model, normalizer, preTokenizer, postProcessor, decoder, addedTokensById);
        }

        internal static ITokenizer CreateTiktokenRuntime(string vocabPath, string? encodingName = null)
        {
            return CreateTiktokenRuntime(TiktokenModel.FromFile(vocabPath, encodingName));
        }

        internal static ITokenizer CreateTiktokenRuntime(byte[] vocab, string? encodingName = null)
        {
            return CreateTiktokenRuntime(TiktokenModel.FromBytes(vocab, encodingName));
        }

        internal static ITokenizer CreateTiktokenRuntime(Stream vocabStream, string? encodingName = null)
        {
            return CreateTiktokenRuntime(TiktokenModel.FromStream(vocabStream, encodingName));
        }

        internal static ITokenizer CreateSentencePieceUnigramRuntime(string modelPath, bool applyIdOffset = false)
        {
            return CreateSentencePieceUnigramRuntime(SentencePieceUnigramModel.FromFile(modelPath, applyIdOffset));
        }

        internal static ITokenizer CreateSentencePieceBpeRuntime(string modelPath, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            return CreateSentencePieceBpeRuntime(SentencePieceBpeModel.FromFile(modelPath, applyIdOffset, addDummyPrefix));
        }

        internal static TokenizerBackendType InferBackendType(JObject root)
        {
            if (root == null)
            {
                return TokenizerBackendType.Unknown;
            }

            var model = root["model"] as JObject;
            if (model == null)
            {
                return TokenizerBackendType.Unknown;
            }

            var explicitType = model["type"]?.Value<string>()?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(explicitType))
            {
                return MapModelType(explicitType!);
            }

            var continuingSubwordPrefix = model["continuing_subword_prefix"]?.Value<string>();
            if (continuingSubwordPrefix != null)
            {
                var merges = model["merges"];
                if (merges == null)
                {
                    return TokenizerBackendType.WordPiece;
                }

                var mergesEmpty = merges.Type == JTokenType.Array && !merges.HasValues;
                if (mergesEmpty || continuingSubwordPrefix == "##")
                {
                    return TokenizerBackendType.WordPiece;
                }

                return TokenizerBackendType.Bpe;
            }

            if (model["merges"] != null)
            {
                return TokenizerBackendType.Bpe;
            }

            if (model["scores"] != null)
            {
                return TokenizerBackendType.SentencePieceUnigram;
            }

            return TokenizerBackendType.Unknown;
        }

        internal TokenizerJsonPipeline ParseTokenizerJsonPipeline(string tokenizerJsonPath)
        {
            var json = File.ReadAllText(tokenizerJsonPath);
            var root = _jsonSerializer.ParseObject(json);
            var tokenizerConfigRoot = LoadTokenizerConfigSibling(tokenizerJsonPath);
            return ParseTokenizerJsonPipeline(root, tokenizerJsonPath, tokenizerConfigRoot, resolveSiblingArtifacts: true);
        }

        internal TokenizerJsonPipeline ParseTokenizerJsonPipeline(JObject root, string tokenizerJsonPath = "<memory>", JObject? tokenizerConfigRoot = null)
        {
            return ParseTokenizerJsonPipeline(root, tokenizerJsonPath, tokenizerConfigRoot, resolveSiblingArtifacts: false);
        }

        private JObject ParseJsonObject(Stream jsonStream)
        {
            using var reader = new StreamReader(jsonStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return _jsonSerializer.ParseObject(reader.ReadToEnd());
        }

        private JObject? ParseOptionalJsonObject(Stream? jsonStream)
        {
            return jsonStream == null ? null : ParseJsonObject(jsonStream);
        }

        private TokenizerJsonPipeline ParseTokenizerJsonPipeline(JObject root, string tokenizerJsonPath, JObject? tokenizerConfigRoot, bool resolveSiblingArtifacts)
        {
            var backendType = InferBackendType(root);
            var siblingSentencePieceType = resolveSiblingArtifacts ? TryInspectSentencePieceSibling(tokenizerJsonPath) : TokenizerBackendType.Unknown;
            if (resolveSiblingArtifacts && siblingSentencePieceType == TokenizerBackendType.SentencePieceBpe && backendType == TokenizerBackendType.Bpe)
            {
                backendType = TokenizerBackendType.SentencePieceBpe;
            }

            if (resolveSiblingArtifacts && backendType == TokenizerBackendType.Unknown)
            {
                backendType = InferBackendTypeFromSiblingArtifacts(tokenizerJsonPath, tokenizerConfigRoot);
            }

            var normalizerConfig = TokenizerJsonComponentFactory.ParseNormalizerConfig(root["normalizer"] as JObject);
            var preTokenizerConfig = TokenizerJsonComponentFactory.ParsePreTokenizerConfig(root["pre_tokenizer"] as JObject);
            var postProcessorConfig = TokenizerJsonComponentFactory.ParsePostProcessorConfig(root["post_processor"] as JObject);
            var decoderConfig = TokenizerJsonComponentFactory.ParseDecoderConfig(root["decoder"] as JObject);
            var addedTokens = ParseAddedTokens(root["added_tokens"] as JArray);
            var truncation = ParseTruncation(root["truncation"] as JObject);
            var padding = ParsePadding(root["padding"] as JObject);
            var sentencePieceModelPath = resolveSiblingArtifacts ? FindSentencePieceModelSibling(tokenizerJsonPath) : null;
            var applySentencePieceIdOffset = ShouldApplySentencePieceIdOffset(sentencePieceModelPath, tokenizerConfigRoot);

            return new TokenizerJsonPipeline(
                tokenizerJsonPath,
                Path.GetFileNameWithoutExtension(tokenizerJsonPath) ?? "tokenizer-json",
                root,
                tokenizerConfigRoot,
                backendType,
                addedTokens,
                truncation,
                padding,
                normalizerConfig,
                preTokenizerConfig,
                postProcessorConfig,
                decoderConfig,
                TokenizerJsonComponentFactory.CreateNormalizer(root["normalizer"] as JObject),
                TokenizerJsonComponentFactory.CreatePreTokenizer(root["pre_tokenizer"] as JObject),
                TokenizerJsonComponentFactory.CreatePostProcessor(postProcessorConfig, preTokenizerConfig),
                TokenizerJsonComponentFactory.CreateDecoder(root["decoder"] as JObject),
                sentencePieceModelPath,
                applySentencePieceIdOffset,
                ShouldAddDummyPrefixForSentencePieceBpe(preTokenizerConfig));
        }

        private static IReadOnlyList<TokenizerJsonAddedToken> ParseAddedTokens(JArray? addedTokens)
        {
            if (addedTokens == null)
            {
                return Array.Empty<TokenizerJsonAddedToken>();
            }

            var parsed = new List<TokenizerJsonAddedToken>(addedTokens.Count);
            foreach (var addedToken in addedTokens.OfType<JObject>())
            {
                var id = addedToken["id"]?.Value<int?>();
                var content = addedToken["content"]?.Value<string>();
                if (!id.HasValue || string.IsNullOrEmpty(content))
                {
                    continue;
                }

                var isSpecial = addedToken["special"]?.Value<bool?>() ?? false;
                parsed.Add(new TokenizerJsonAddedToken(
                    id.Value,
                    content!,
                    addedToken["single_word"]?.Value<bool?>() ?? false,
                    addedToken["lstrip"]?.Value<bool?>() ?? false,
                    addedToken["rstrip"]?.Value<bool?>() ?? false,
                    addedToken["normalized"]?.Value<bool?>() ?? !isSpecial,
                    isSpecial));
            }

            return parsed;
        }

        private static Truncation? ParseTruncation(JObject? truncation)
        {
            if (truncation == null)
            {
                return null;
            }

            var maxLength = truncation["max_length"]?.Value<int?>();
            if (!maxLength.HasValue)
            {
                return null;
            }

            return new Truncation(
                truncation["direction"]?.Value<string>(),
                maxLength.Value,
                truncation["strategy"]?.Value<string>(),
                truncation["stride"]?.Value<int?>() ?? 0);
        }

        private static Padding? ParsePadding(JObject? padding)
        {
            if (padding == null)
            {
                return null;
            }

            return new Padding(
                padding["strategy"]?.Value<string>(),
                padding["direction"]?.Value<string>(),
                padding["length"]?.Value<int?>(),
                padding["pad_id"]?.Value<int?>() ?? 0,
                padding["pad_type_id"]?.Value<int?>() ?? 0,
                padding["pad_token"]?.Value<string>(),
                padding["pad_to_multiple_of"]?.Value<int?>());
        }

        private static TokenizerBackendType MapModelType(string explicitType)
        {
            return explicitType switch
            {
                "wordpiece" => TokenizerBackendType.WordPiece,
                "bpe" => TokenizerBackendType.Bpe,
                "unigram" => TokenizerBackendType.SentencePieceUnigram,
                "sentencepiecebpe" => TokenizerBackendType.SentencePieceBpe,
                "sentencepiece_bpe" => TokenizerBackendType.SentencePieceBpe,
                _ => TokenizerBackendType.Unknown,
            };
        }

        private JObject? LoadTokenizerConfigSibling(string tokenizerJsonPath)
        {
            var directory = Path.GetDirectoryName(tokenizerJsonPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            var tokenizerConfigPath = Path.Combine(directory, "tokenizer_config.json");
            if (!File.Exists(tokenizerConfigPath))
            {
                return null;
            }

            try
            {
                return _jsonSerializer.ParseObject(File.ReadAllText(tokenizerConfigPath));
            }
            catch
            {
                return null;
            }
        }

        private static string? FindSentencePieceModelSibling(string tokenizerJsonPath)
        {
            var directory = Path.GetDirectoryName(tokenizerJsonPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return null;
            }

            var modelFiles = Directory.GetFiles(directory, "*.model");
            if (modelFiles.Length == 0)
            {
                return null;
            }

            return modelFiles[0];
        }

        private static TokenizerBackendType InferBackendTypeFromSiblingArtifacts(string tokenizerJsonPath, JObject? tokenizerConfigRoot)
        {
            var modelType = tokenizerConfigRoot?["model_type"]?.Value<string>()?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(modelType))
            {
                if (modelType.IndexOf("bpe", StringComparison.Ordinal) >= 0)
                {
                    return TokenizerBackendType.SentencePieceBpe;
                }

                if (modelType.IndexOf("unigram", StringComparison.Ordinal) >= 0 || modelType.IndexOf("sentencepiece", StringComparison.Ordinal) >= 0)
                {
                    return TokenizerBackendType.SentencePieceUnigram;
                }
            }

            var modelPath = FindSentencePieceModelSibling(tokenizerJsonPath);
            if (modelPath == null)
            {
                return TokenizerBackendType.Unknown;
            }

            try
            {
                var inspectedType = SentencePieceModelInspector.DetectBackendType(modelPath);
                if (inspectedType != TokenizerBackendType.Unknown)
                {
                    return inspectedType;
                }
            }
            catch
            {
                // Fall back to filename heuristics when the model cannot be parsed.
            }

            var modelFileName = Path.GetFileName(modelPath);
            if (modelFileName.IndexOf(".bpe.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TokenizerBackendType.SentencePieceBpe;
            }

            return TokenizerBackendType.SentencePieceUnigram;
        }

        private static TokenizerBackendType TryInspectSentencePieceSibling(string tokenizerJsonPath)
        {
            var modelPath = FindSentencePieceModelSibling(tokenizerJsonPath);
            if (modelPath == null)
            {
                return TokenizerBackendType.Unknown;
            }

            try
            {
                return SentencePieceModelInspector.DetectBackendType(modelPath);
            }
            catch
            {
                return TokenizerBackendType.Unknown;
            }
        }

        private static bool ShouldApplySentencePieceIdOffset(string? modelPath, JObject? tokenizerConfigRoot)
        {
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                var directory = Path.GetDirectoryName(modelPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    var directoryName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (directoryName.IndexOf("xlm-roberta", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        directoryName.IndexOf("xlm_roberta", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            var modelType = tokenizerConfigRoot?["model_type"]?.Value<string>();
            return modelType?.IndexOf("xlm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldAddDummyPrefixForSentencePieceBpe(PreTokenizerConfig? preTokenizer)
        {
            return !TokenizerJsonComponentFactory.IsSplitOnSpaceMergedWithPrevious(preTokenizer);
        }

        private static ITokenizer CreateWordPieceRuntime(WordPieceModel model)
        {
            return TokenizerModelRuntimeFactory.CreateWordPieceRuntime(model).Assemble();
        }

        private static ITokenizer CreateWordPieceRuntime(WordPieceModel model, INormalizer? normalizer, IPreTokenizer? preTokenizer, IDecoder decoder, IPostProcessor? postProcessor, Truncation? truncation = null, Padding? padding = null)
        {
            return TokenizerModelRuntimeFactory.CreateWordPiece(model, normalizer, preTokenizer, decoder, postProcessor, truncation, padding).Assemble();
        }

        private static ITokenizer CreateBpeRuntime(BpeModel model)
        {
            return TokenizerModelRuntimeFactory.CreateBpeRuntime(model).Assemble();
        }

        private static ITokenizer CreateBpeRuntime(BpeModel model, INormalizer? normalizer, IPreTokenizer? preTokenizer, IPostProcessor? postProcessor, IDecoder? decoder, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById, Truncation? truncation = null, Padding? padding = null)
        {
            return TokenizerModelRuntimeFactory.CreateBpe(model, normalizer, preTokenizer, postProcessor, decoder, addedTokensById, truncation, padding).Assemble();
        }

        private static ITokenizer CreateTiktokenRuntime(TiktokenModel model)
        {
            return TokenizerModelRuntimeFactory.CreateTiktoken(model).Assemble();
        }

        private static ITokenizer CreateSentencePieceUnigramRuntime(Stream modelStream, string name = "sentencepiece", IPostProcessor? postProcessor = null, bool applyIdOffset = false, INormalizer? normalizer = null, IPreTokenizer? preTokenizer = null, IDecoder? decoder = null)
        {
            return CreateSentencePieceUnigramRuntime(SentencePieceUnigramModel.FromStream(modelStream, name, applyIdOffset), postProcessor, truncation: null, padding: null, normalizer, preTokenizer, decoder);
        }

        private static ITokenizer CreateSentencePieceUnigramRuntime(SentencePieceUnigramModel model, IPostProcessor? postProcessor = null, Truncation? truncation = null, Padding? padding = null, INormalizer? normalizer = null, IPreTokenizer? preTokenizer = null, IDecoder? decoder = null, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById = null)
        {
            return TokenizerModelRuntimeFactory.CreateSentencePieceUnigram(model, postProcessor, truncation, padding, normalizer, preTokenizer, decoder, addedTokensById).Assemble();
        }

        private static ITokenizer CreateSentencePieceBpeRuntime(Stream modelStream, string name = "sentencepiece", IPostProcessor? postProcessor = null, bool applyIdOffset = false, bool addDummyPrefix = true, INormalizer? normalizer = null, IPreTokenizer? preTokenizer = null, IDecoder? decoder = null)
        {
            return CreateSentencePieceBpeRuntime(SentencePieceBpeModel.FromStream(modelStream, name, applyIdOffset, addDummyPrefix), postProcessor, truncation: null, padding: null, normalizer, preTokenizer, decoder);
        }

        private static ITokenizer CreateSentencePieceBpeRuntime(SentencePieceBpeModel model, IPostProcessor? postProcessor = null, Truncation? truncation = null, Padding? padding = null, INormalizer? normalizer = null, IPreTokenizer? preTokenizer = null, IDecoder? decoder = null, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById = null)
        {
            return TokenizerModelRuntimeFactory.CreateSentencePieceBpe(model, postProcessor, truncation, padding, normalizer, preTokenizer, decoder, addedTokensById).Assemble();
        }

        private static ITokenizer CreateWordPiece(TokenizerJsonPipeline pipeline)
        {
            var options = WordPieceTokenizerConfigLoader.CreateOptions(pipeline);
            var model = WordPieceModel.FromTokenizerJson(pipeline.Root, options);
            var normalizer = pipeline.Normalizer ?? (options.LowerCaseBeforeTokenization ? new LowercaseNormalizer() : null);
            var preTokenizer = pipeline.PreTokenizer ?? (options.ApplyBasicTokenization ? new BertPreTokenizer() : null);
            var decoder = pipeline.Decoder ?? new WordPieceDecoder(options.ContinuingSubwordPrefix, options.CleanUpTokenizationSpaces);
            return CreateWordPieceRuntime(model, normalizer, preTokenizer, decoder, pipeline.PostProcessor, pipeline.Truncation, pipeline.Padding);
        }

        private static ITokenizer CreateBpe(TokenizerJsonPipeline pipeline)
        {
            var model = BpeModel.FromTokenizerJson(pipeline.Root, pipeline.TokenizerConfigRoot, options: null);
            var addedTokensById = TokenizerModelRuntimeFactory.ReadBpeAddedTokens(pipeline.AddedTokens, pipeline.Root, pipeline.TokenizerConfigRoot, model);
            var postProcessor = TokenizerModelRuntimeFactory.CreateBpePostProcessor(pipeline.PostProcessorConfig, pipeline.PreTokenizerConfig, addedTokensById);
            TokenizerModelRuntimeFactory.ApplyBpeAddedTokenNormalization(addedTokensById, pipeline.Normalizer);
            return CreateBpeRuntime(model, pipeline.Normalizer, pipeline.PreTokenizer, postProcessor, pipeline.Decoder, addedTokensById, pipeline.Truncation, pipeline.Padding);
        }

        private static ITokenizer CreateSentencePieceUnigram(TokenizerJsonPipeline pipeline)
        {
            using var stream = File.OpenRead(pipeline.SentencePieceModelPath!);
            var modelName = Path.GetFileNameWithoutExtension(pipeline.SentencePieceModelPath) ?? "sentencepiece";
            return CreateSentencePieceUnigram(pipeline, stream, modelName);
        }

        private static ITokenizer CreateSentencePieceBpe(TokenizerJsonPipeline pipeline)
        {
            using var stream = File.OpenRead(pipeline.SentencePieceModelPath!);
            var modelName = Path.GetFileNameWithoutExtension(pipeline.SentencePieceModelPath) ?? "sentencepiece";
            return CreateSentencePieceBpe(pipeline, stream, modelName);
        }

        private static ITokenizer CreateSentencePieceUnigram(TokenizerJsonPipeline pipeline, Stream modelStream, string modelName)
        {
            var model = SentencePieceUnigramModel.FromStream(modelStream, modelName, pipeline.ApplySentencePieceIdOffset);
            var addedTokensById = TokenizerModelRuntimeFactory.ReadSentencePieceAddedTokens(pipeline.AddedTokens, pipeline.PostProcessorConfig);
            return CreateSentencePieceUnigramRuntime(model, pipeline.PostProcessor, pipeline.Truncation, pipeline.Padding, pipeline.Normalizer, pipeline.PreTokenizer, pipeline.Decoder, addedTokensById);
        }

        private static ITokenizer CreateSentencePieceBpe(TokenizerJsonPipeline pipeline, Stream modelStream, string modelName)
        {
            var model = SentencePieceBpeModel.FromStream(modelStream, modelName, pipeline.ApplySentencePieceIdOffset, pipeline.AddDummyPrefixForSentencePieceBpe);
            var addedTokensById = TokenizerModelRuntimeFactory.ReadSentencePieceAddedTokens(pipeline.AddedTokens, pipeline.PostProcessorConfig);
            return CreateSentencePieceBpeRuntime(model, pipeline.PostProcessor, pipeline.Truncation, pipeline.Padding, pipeline.Normalizer, pipeline.PreTokenizer, pipeline.Decoder, addedTokensById);
        }

    }
}