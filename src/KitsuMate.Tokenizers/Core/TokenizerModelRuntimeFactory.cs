using System;
using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    internal static class TokenizerModelRuntimeFactory
    {
        internal static TokenizerAssemblySpec Create(ITokenizerModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return model switch
            {
                WordPieceModel wordPieceModel => CreateWordPieceRuntime(wordPieceModel),
                BpeModel bpeModel => CreateBpeRuntime(bpeModel),
                TiktokenModel tiktokenModel => CreateTiktoken(tiktokenModel),
                SentencePieceUnigramModel sentencePieceUnigramModel => CreateSentencePieceUnigram(sentencePieceUnigramModel),
                SentencePieceBpeModel sentencePieceBpeModel => CreateSentencePieceBpe(sentencePieceBpeModel),
                _ => throw CreateUnsupportedBackendException(
                    model.Name,
                    model.BackendType,
                    "Concrete runtime creation for this model type has not been ported yet."),
            };
        }

        internal static TokenizerNotSupportedException CreateUnsupportedBackendException(string? name, TokenizerBackendType backendType, string reason)
        {
            var effectiveName = string.IsNullOrWhiteSpace(name) ? "tokenizer" : name;
            return new TokenizerNotSupportedException(
                $"Tokenizer backend '{backendType}' is not implemented for '{effectiveName}'. {reason}",
                backendType);
        }

        internal static TokenizerAssemblySpec CreateWordPieceRuntime(WordPieceModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var normalizer = model.Options.LowerCaseBeforeTokenization ? new LowercaseNormalizer() : null;
            var preTokenizer = model.Options.ApplyBasicTokenization ? new BertPreTokenizer() : null;
            var decoder = new WordPieceDecoder(model.Options.ContinuingSubwordPrefix, model.Options.CleanUpTokenizationSpaces);
            return CreateWordPiece(model, normalizer, preTokenizer, decoder, postProcessor: null);
        }

        internal static TokenizerAssemblySpec CreateBpeRuntime(BpeModel model)
        {
            return CreateBpe(model, normalizer: null, preTokenizer: null, postProcessor: null, decoder: null, addedTokensById: null);
        }

        internal static TokenizerAssemblySpec CreateWordPiece(
            WordPieceModel model,
            INormalizer? normalizer,
            IPreTokenizer? preTokenizer,
            IDecoder decoder,
            IPostProcessor? postProcessor,
            Truncation? truncation = null,
            Padding? padding = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (decoder == null)
            {
                throw new ArgumentNullException(nameof(decoder));
            }

            return new TokenizerAssemblySpec(
                model,
                (tokenizer, text, maxTokenCount) => CreateWordPieceEncodingCore(model, tokenizer.Normalizer, tokenizer.PreTokenizer, text, maxTokenCount),
                (tokenizer, ids, skipSpecialTokens) => DecodeWordPieceCore(model, RequireDecoder(tokenizer), ids, skipSpecialTokens))
            {
                Normalizer = normalizer,
                PreTokenizer = preTokenizer,
                PostProcessor = postProcessor,
                Decoder = decoder,
                FinalizeSingle = (tokenizer, encoding, addSpecialTokens, maxTokenCount) => FinalizeSingleWordPieceEncoding(model, tokenizer.PostProcessor, encoding, addSpecialTokens, maxTokenCount),
                FinalizePair = (tokenizer, first, second, addSpecialTokens, maxTokenCount) => FinalizePairWordPieceEncoding(model, tokenizer.PostProcessor, first, second, addSpecialTokens, maxTokenCount),
                Truncation = truncation,
                Padding = padding,
                AddedTokensResolver = (tokenizer, isPair) => GetWordPieceAddedTokensCount(tokenizer.PostProcessor, isPair),
            };
        }

        internal static TokenizerAssemblySpec CreateBpe(
            BpeModel model,
            INormalizer? normalizer,
            IPreTokenizer? preTokenizer,
            IPostProcessor? postProcessor,
            IDecoder? decoder,
            IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById,
            Truncation? truncation = null,
            Padding? padding = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var addedTokens = addedTokensById != null
                ? addedTokensById.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<int, BpeRuntimeAddedTokenInfo>();
            var addedTokensByContent = addedTokens.Values
                .OrderByDescending(token => token.MatchLength)
                .ThenBy(token => token.Content, StringComparer.Ordinal)
                .ToList();
            var specialTokenIds = new HashSet<int>(addedTokens.Values
                .Where(token => token.IsSpecial)
                .Select(token => token.Id));

            return new TokenizerAssemblySpec(
                model,
                (tokenizer, text, maxTokenCount) => CreateBpeEncoding(model, tokenizer.Normalizer, addedTokens, addedTokensByContent, text, maxTokenCount),
                (tokenizer, ids, skipSpecialTokens) => DecodeBpeCore(model, addedTokens, specialTokenIds, ids, skipSpecialTokens))
            {
                Normalizer = normalizer,
                PreTokenizer = preTokenizer,
                PostProcessor = postProcessor,
                Decoder = decoder,
                Truncation = truncation,
                Padding = padding,
            };
        }

        internal static TokenizerAssemblySpec CreateTiktoken(TiktokenModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return new TokenizerAssemblySpec(
                model,
                (tokenizer, text, maxTokenCount) =>
                {
                    if (text == null)
                    {
                        throw new ArgumentNullException(nameof(text));
                    }

                    var pieces = model.Encode(text, maxTokenCount);
                    return new EncodingResult
                    {
                        Ids = pieces.Select(piece => piece.Id).ToList(),
                        Tokens = pieces.Select(piece => piece.Token).ToList(),
                        TypeIds = Enumerable.Repeat(0, pieces.Count).ToList(),
                        AttentionMask = Enumerable.Repeat(1, pieces.Count).ToList(),
                        SpecialTokensMask = pieces.Select(piece => piece.IsSpecial ? 1 : 0).ToList(),
                        Words = pieces.Select(piece => piece.WordIndex).ToList(),
                        Offsets = pieces.Select(piece => (piece.Start, piece.End)).ToList(),
                    };
                },
                (tokenizer, ids, skipSpecialTokens) =>
                {
                    if (ids == null)
                    {
                        throw new ArgumentNullException(nameof(ids));
                    }

                    var effectiveIds = skipSpecialTokens ? ids.Where(id => !model.IsSpecialTokenId(id)) : ids;
                    return model.Decode(effectiveIds);
                });
        }

        internal static TokenizerAssemblySpec CreateSentencePieceUnigram(
            SentencePieceUnigramModel model,
            IPostProcessor? postProcessor = null,
            Truncation? truncation = null,
            Padding? padding = null,
            INormalizer? normalizer = null,
            IPreTokenizer? preTokenizer = null,
            IDecoder? decoder = null,
            IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var addedTokens = addedTokensById != null
                ? addedTokensById.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<int, BpeRuntimeAddedTokenInfo>();

            return new TokenizerAssemblySpec(
                model,
                (tokenizer, text, maxTokenCount) =>
                {
                    if (text == null)
                    {
                        throw new ArgumentNullException(nameof(text));
                    }

                    var normalizedInput = tokenizer.Normalizer?.Normalize(text) ?? text;
                    var normalized = model.NormalizeText(normalizedInput);
                    var pieces = model.Tokenize(normalized.Value, maxTokenCount);

                    return new EncodingResult
                    {
                        Ids = pieces.Select(piece => model.ToExternalId(piece.Id)).ToList(),
                        Tokens = pieces.Select(piece => piece.Token).ToList(),
                        TypeIds = Enumerable.Repeat(0, pieces.Count).ToList(),
                        AttentionMask = Enumerable.Repeat(1, pieces.Count).ToList(),
                        SpecialTokensMask = pieces.Select(piece => model.SpecialTokenIds.Contains(piece.Id) ? 1 : 0).ToList(),
                        Words = pieces.Select(_ => (int?)null).ToList(),
                        Offsets = pieces.Select(piece => normalized.GetOriginalOffsets(normalizedInput, piece.Start, piece.End)).ToList(),
                    };
                },
                (tokenizer, ids, skipSpecialTokens) => DecodeSentencePieceCore(
                    (chunkIds, chunkSkipSpecialTokens, trimLeadingBoundary, trimTrailingBoundary) =>
                        model.DecodeWithBoundaryOptions(chunkIds, chunkSkipSpecialTokens, trimLeadingBoundary, trimTrailingBoundary),
                    addedTokens,
                    ids,
                    skipSpecialTokens))
            {
                Normalizer = normalizer,
                PreTokenizer = preTokenizer,
                PostProcessor = postProcessor,
                Decoder = decoder,
                Truncation = truncation,
                Padding = padding,
            };
        }

        internal static TokenizerAssemblySpec CreateSentencePieceBpe(
            SentencePieceBpeModel model,
            IPostProcessor? postProcessor = null,
            Truncation? truncation = null,
            Padding? padding = null,
            INormalizer? normalizer = null,
            IPreTokenizer? preTokenizer = null,
            IDecoder? decoder = null,
            IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo>? addedTokensById = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var addedTokens = addedTokensById != null
                ? addedTokensById.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<int, BpeRuntimeAddedTokenInfo>();

            return new TokenizerAssemblySpec(
                model,
                (tokenizer, text, maxTokenCount) =>
                {
                    if (text == null)
                    {
                        throw new ArgumentNullException(nameof(text));
                    }

                    var normalizedInput = tokenizer.Normalizer?.Normalize(text) ?? text;
                    var normalized = model.NormalizeText(normalizedInput);
                    var pieces = model.EncodeNormalized(normalized, maxTokenCount);

                    return new EncodingResult
                    {
                        Ids = pieces.Select(piece => model.ToExternalId(piece.Id)).ToList(),
                        Tokens = pieces.Select(piece => piece.Value).ToList(),
                        TypeIds = Enumerable.Repeat(0, pieces.Count).ToList(),
                        AttentionMask = Enumerable.Repeat(1, pieces.Count).ToList(),
                        SpecialTokensMask = pieces.Select(piece => model.SpecialTokenIds.Contains(piece.Id) ? 1 : 0).ToList(),
                        Words = pieces.Select(_ => (int?)null).ToList(),
                        Offsets = pieces.Select(piece => normalized.GetOriginalOffsets(normalizedInput, piece.Start, piece.End)).ToList(),
                    };
                },
                (tokenizer, ids, skipSpecialTokens) => DecodeSentencePieceCore(
                    (chunkIds, chunkSkipSpecialTokens, trimLeadingBoundary, trimTrailingBoundary) =>
                        model.DecodeWithBoundaryOptions(chunkIds, chunkSkipSpecialTokens, trimLeadingBoundary),
                    addedTokens,
                    ids,
                    skipSpecialTokens))
            {
                Normalizer = normalizer,
                PreTokenizer = preTokenizer,
                PostProcessor = postProcessor,
                Decoder = decoder,
                Truncation = truncation,
                Padding = padding,
            };
        }

        internal static IPostProcessor? CreateBpePostProcessor(PostProcessorConfig? postProcessor, PreTokenizerConfig? preTokenizer, IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById)
        {
            if (postProcessor == null)
            {
                return null;
            }

            foreach (var specialToken in TokenizerJsonComponentFactory.EnumerateSpecialTokens(postProcessor))
            {
                AddOrUpdateBpeAddedToken(addedTokensById, specialToken.Id, specialToken.Token, isSpecial: true);
            }

            return TokenizerJsonComponentFactory.CreatePostProcessor(postProcessor, preTokenizer);
        }

        internal static Dictionary<int, BpeRuntimeAddedTokenInfo> ReadBpeAddedTokens(JObject root, JObject? tokenizerConfigRoot, BpeModel model)
        {
            var addedTokensById = new Dictionary<int, BpeRuntimeAddedTokenInfo>();
            AddBpeAddedTokens(root["added_tokens"] as JArray, addedTokensById);

            foreach (var content in ReadConfiguredSpecialTokenContents(root))
            {
                AddBpeSpecialTokenByContent(model, addedTokensById, content);
            }

            foreach (var content in ReadConfiguredSpecialTokenContents(tokenizerConfigRoot))
            {
                AddBpeSpecialTokenByContent(model, addedTokensById, content);
            }

            return addedTokensById;
        }

        internal static Dictionary<int, BpeRuntimeAddedTokenInfo> ReadBpeAddedTokens(IReadOnlyList<TokenizerJsonAddedToken> addedTokens, JObject root, JObject? tokenizerConfigRoot, BpeModel model)
        {
            var addedTokensById = new Dictionary<int, BpeRuntimeAddedTokenInfo>();
            AddBpeAddedTokens(addedTokens, addedTokensById);

            foreach (var content in ReadConfiguredSpecialTokenContents(root))
            {
                AddBpeSpecialTokenByContent(model, addedTokensById, content);
            }

            foreach (var content in ReadConfiguredSpecialTokenContents(tokenizerConfigRoot))
            {
                AddBpeSpecialTokenByContent(model, addedTokensById, content);
            }

            return addedTokensById;
        }

        internal static Dictionary<int, BpeRuntimeAddedTokenInfo> ReadSentencePieceAddedTokens(IReadOnlyList<TokenizerJsonAddedToken> addedTokens, PostProcessorConfig? postProcessor)
        {
            var addedTokensById = new Dictionary<int, BpeRuntimeAddedTokenInfo>();
            AddBpeAddedTokens(addedTokens, addedTokensById);

            if (postProcessor != null)
            {
                foreach (var specialToken in TokenizerJsonComponentFactory.EnumerateSpecialTokens(postProcessor))
                {
                    AddOrUpdateBpeAddedToken(addedTokensById, specialToken.Id, specialToken.Token, isSpecial: true);
                }
            }

            return addedTokensById;
        }

        internal static void ApplyBpeAddedTokenNormalization(IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, INormalizer? normalizer)
        {
            if (normalizer == null)
            {
                return;
            }

            foreach (var entry in addedTokensById.ToArray())
            {
                if (!entry.Value.Normalized)
                {
                    continue;
                }

                addedTokensById[entry.Key] = entry.Value.WithNormalizedContent(normalizer.Normalize(entry.Value.Content));
            }
        }

        private static EncodingResult CreateWordPieceEncodingCore(WordPieceModel model, INormalizer? normalizer, IPreTokenizer? preTokenizer, string text, int maxTokenCount)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var input = normalizer?.Normalize(text) ?? text;
            var segments = preTokenizer?.PreTokenize(input) ?? new[] { (0, input.Length) };

            var ids = new List<int>();
            var tokens = new List<string>();
            var offsets = new List<(int Start, int End)>();
            var words = new List<int?>();
            var specialTokensMask = new List<int>();

            var wordIndex = 0;
            foreach (var (offset, length) in segments)
            {
                if (length <= 0 || offset < 0 || offset + length > input.Length)
                {
                    continue;
                }

                var segment = input.Substring(offset, length);
                var pieces = model.TokenizeWord(segment);
                foreach (var piece in pieces)
                {
                    ids.Add(piece.Id);
                    tokens.Add(piece.Value);
                    offsets.Add((offset + piece.Start, offset + piece.End));
                    words.Add(wordIndex);
                    specialTokensMask.Add(0);
                }

                wordIndex++;
            }

            if (ids.Count > maxTokenCount)
            {
                ids = ids.Take(maxTokenCount).ToList();
                tokens = tokens.Take(maxTokenCount).ToList();
                offsets = offsets.Take(maxTokenCount).ToList();
                words = words.Take(maxTokenCount).ToList();
                specialTokensMask = specialTokensMask.Take(maxTokenCount).ToList();
            }

            return new EncodingResult
            {
                Ids = ids,
                Tokens = tokens,
                Offsets = offsets,
                Words = words,
                TypeIds = Enumerable.Repeat(0, ids.Count).ToList(),
                AttentionMask = Enumerable.Repeat(1, ids.Count).ToList(),
                SpecialTokensMask = specialTokensMask,
            };
        }

        private static EncodingResult FinalizeSingleWordPieceEncoding(WordPieceModel model, IPostProcessor? postProcessor, EncodingResult encoding, bool addSpecialTokens, int maxTokenCount)
        {
            encoding.SetSequenceId(0);
            if (postProcessor != null)
            {
                var processed = postProcessor.Process(new List<EncodingResult> { encoding }, addSpecialTokens);
                TruncateWordPieceEncoding(processed, maxTokenCount);
                return processed;
            }

            return AddWordPieceSpecialTokensToSingleEncoding(model, encoding, addSpecialTokens, maxTokenCount);
        }

        private static EncodingResult FinalizePairWordPieceEncoding(WordPieceModel model, IPostProcessor? postProcessor, EncodingResult first, EncodingResult second, bool addSpecialTokens, int maxTokenCount)
        {
            if (postProcessor != null)
            {
                var processed = postProcessor.Process(new List<EncodingResult> { first, second }, addSpecialTokens);
                TruncateWordPieceEncoding(processed, maxTokenCount);
                return processed;
            }

            if (addSpecialTokens)
            {
                var result = new EncodingResult();
                AppendWordPieceSpecialToken(model, model.Options.ClassificationToken, result, typeId: 0);
                AppendEncoding(first, result, typeId: 0, sequenceId: 0);
                AppendWordPieceSpecialToken(model, model.Options.SeparatorToken, result, typeId: 0);
                AppendEncoding(second, result, typeId: 1, sequenceId: 1);
                AppendWordPieceSpecialToken(model, model.Options.SeparatorToken, result, typeId: 1);
                TruncateWordPieceEncoding(result, maxTokenCount);
                return result;
            }

            first.TypeIds = Enumerable.Repeat(0, first.Length).ToList();
            second.TypeIds = Enumerable.Repeat(1, second.Length).ToList();
            first.SetSequenceId(0);
            second.SetSequenceId(1);
            var merged = EncodingResult.Merge(new[] { first, second }, false);
            TruncateWordPieceEncoding(merged, maxTokenCount);
            return merged;
        }

        private static EncodingResult AddWordPieceSpecialTokensToSingleEncoding(WordPieceModel model, EncodingResult encoding, bool addSpecialTokens, int maxTokenCount)
        {
            if (!addSpecialTokens)
            {
                TruncateWordPieceEncoding(encoding, maxTokenCount);
                return encoding;
            }

            var result = new EncodingResult();
            AppendWordPieceSpecialToken(model, model.Options.ClassificationToken, result, 0);
            AppendEncoding(encoding, result, 0, 0);
            AppendWordPieceSpecialToken(model, model.Options.SeparatorToken, result, 0);
            TruncateWordPieceEncoding(result, maxTokenCount);
            return result;
        }

        private static string? DecodeWordPieceCore(WordPieceModel model, IDecoder decoder, IEnumerable<int> ids, bool skipSpecialTokens)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var specialTokens = new HashSet<string>(StringComparer.Ordinal)
            {
                model.Options.ClassificationToken,
                model.Options.SeparatorToken,
                model.Options.PaddingToken,
                model.Options.MaskToken,
            };

            var tokens = ids
                .Select(model.IdToToken)
                .Where(token => token != null)
                .Select(token => token!)
                .Where(token => !skipSpecialTokens || !specialTokens.Contains(token))
                .ToList();

            return decoder.Decode(tokens);
        }

        private static void AppendWordPieceSpecialToken(WordPieceModel model, string token, EncodingResult result, int typeId)
        {
            var tokenId = model.TokenToId(token);
            if (!tokenId.HasValue)
            {
                return;
            }

            result.Ids.Add(tokenId.Value);
            result.Tokens.Add(token);
            result.TypeIds.Add(typeId);
            result.AttentionMask.Add(1);
            result.SpecialTokensMask.Add(1);
            result.Words.Add(null);
            result.Offsets.Add((0, 0));
        }

        private static void AppendEncoding(EncodingResult source, EncodingResult target, int typeId, int sequenceId)
        {
            var start = target.Length;
            target.Ids.AddRange(source.Ids);
            target.Tokens.AddRange(source.Tokens);
            target.TypeIds.AddRange(Enumerable.Repeat(typeId, source.Length));
            target.AttentionMask.AddRange(Enumerable.Repeat(1, source.Length));
            target.SpecialTokensMask.AddRange(source.SpecialTokensMask);
            target.Words.AddRange(source.Words);
            target.Offsets.AddRange(source.Offsets);
            target.SequenceRanges[sequenceId] = (start, start + source.Length);
        }

        private static void TruncateWordPieceEncoding(EncodingResult encoding, int maxTokenCount)
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
        }

        private static int GetWordPieceAddedTokensCount(IPostProcessor? postProcessor, bool isPair)
        {
            if (postProcessor != null)
            {
                return postProcessor.AddedTokens(isPair);
            }

            return isPair ? 3 : 2;
        }

        private static EncodingResult CreateBpeEncoding(BpeModel model, INormalizer? normalizer, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, IReadOnlyList<BpeRuntimeAddedTokenInfo> addedTokensByContent, string text, int maxTokenCount)
        {
            var pieces = EncodeBpeWithAddedTokens(model, normalizer, addedTokensByContent, text, maxTokenCount);
            return new EncodingResult
            {
                Ids = pieces.Select(piece => piece.Id).ToList(),
                Tokens = pieces.Select(piece => piece.Value).ToList(),
                TypeIds = Enumerable.Repeat(0, pieces.Count).ToList(),
                AttentionMask = Enumerable.Repeat(1, pieces.Count).ToList(),
                SpecialTokensMask = pieces.Select(piece => addedTokensById.TryGetValue(piece.Id, out var addedToken) && addedToken.IsSpecial ? 1 : 0).ToList(),
                Words = pieces.Select(piece => piece.WordIndex).ToList(),
                Offsets = pieces.Select(piece => (piece.Start, piece.End)).ToList(),
            };
        }

        private static IReadOnlyList<BpeModel.BpeTokenPiece> EncodeBpeWithAddedTokens(BpeModel model, INormalizer? normalizer, IReadOnlyList<BpeRuntimeAddedTokenInfo> addedTokensByContent, string text, int maxTokenCount)
        {
            if (addedTokensByContent.Count == 0)
            {
                return model.Encode(text, maxTokenCount);
            }

            var pieces = new List<BpeModel.BpeTokenPiece>();
            var nextWordIndex = 0;
            var currentIndex = 0;

            while (currentIndex < text.Length)
            {
                if (pieces.Count >= maxTokenCount)
                {
                    break;
                }

                var match = FindNextBpeAddedToken(normalizer, addedTokensByContent, text, currentIndex);
                if (match == null)
                {
                    AppendEncodedBpeChunk(model, text.Substring(currentIndex), currentIndex, ref nextWordIndex, pieces, maxTokenCount);
                    break;
                }

                if (match.ConsumedStart > currentIndex)
                {
                    AppendEncodedBpeChunk(model, text.Substring(currentIndex, match.ConsumedStart - currentIndex), currentIndex, ref nextWordIndex, pieces, maxTokenCount);
                    if (pieces.Count >= maxTokenCount)
                    {
                        break;
                    }
                }

                pieces.Add(new BpeModel.BpeTokenPiece(match.Token.Content, match.Token.Id, match.ConsumedStart, match.ConsumedEnd, null));
                currentIndex = match.ConsumedEnd;
            }

            return pieces;
        }

        private static void AppendEncodedBpeChunk(BpeModel model, string chunk, int absoluteOffset, ref int nextWordIndex, ICollection<BpeModel.BpeTokenPiece> pieces, int maxTokenCount)
        {
            if (string.IsNullOrEmpty(chunk) || pieces.Count >= maxTokenCount)
            {
                return;
            }

            var remainingTokenCount = maxTokenCount - pieces.Count;
            var chunkPieces = model.Encode(chunk, remainingTokenCount);
            foreach (var piece in chunkPieces)
            {
                pieces.Add(new BpeModel.BpeTokenPiece(
                    piece.Value,
                    piece.Id,
                    piece.Start + absoluteOffset,
                    piece.End + absoluteOffset,
                    piece.WordIndex.HasValue ? piece.WordIndex.Value + nextWordIndex : null));
            }

            var chunkWordCount = chunkPieces
                .Where(piece => piece.WordIndex.HasValue)
                .Select(piece => piece.WordIndex!.Value)
                .DefaultIfEmpty(-1)
                .Max() + 1;
            nextWordIndex += Math.Max(0, chunkWordCount);
        }

        private static BpeRuntimeAddedTokenMatch? FindNextBpeAddedToken(INormalizer? normalizer, IReadOnlyList<BpeRuntimeAddedTokenInfo> addedTokensByContent, string text, int startIndex)
        {
            BpeRuntimeAddedTokenMatch? bestMatch = null;
            string? normalizedText = null;
            var canUseNormalizedText = false;

            if (normalizer != null && addedTokensByContent.Any(token => token.Normalized))
            {
                normalizedText = normalizer.Normalize(text);
                canUseNormalizedText = normalizedText.Length == text.Length;
            }

            foreach (var token in addedTokensByContent)
            {
                if (string.IsNullOrEmpty(token.Content))
                {
                    continue;
                }

                var searchIndex = startIndex;
                var searchText = text;
                var searchContent = token.Content;
                var matchLength = token.Content.Length;

                if (token.Normalized && canUseNormalizedText && !string.IsNullOrEmpty(token.NormalizedContent))
                {
                    searchText = normalizedText!;
                    searchContent = token.NormalizedContent!;
                    matchLength = token.NormalizedContent!.Length;
                }

                while (searchIndex < text.Length)
                {
                    var matchIndex = searchText.IndexOf(searchContent, searchIndex, StringComparison.Ordinal);
                    if (matchIndex < 0)
                    {
                        break;
                    }

                    if (TryCreateBpeAddedTokenMatch(text, token, matchIndex, matchLength, out var match) &&
                        (bestMatch == null ||
                         match.Start < bestMatch.Start ||
                         (match.Start == bestMatch.Start && token.MatchLength > bestMatch.Token.MatchLength)))
                    {
                        bestMatch = match;
                    }

                    searchIndex = matchIndex + 1;
                }
            }

            return bestMatch;
        }

        private static bool TryCreateBpeAddedTokenMatch(string text, BpeRuntimeAddedTokenInfo token, int matchIndex, int matchLength, out BpeRuntimeAddedTokenMatch match)
        {
            match = null!;

            if (token.SingleWord && !HasSingleWordBoundaries(text, matchIndex, matchLength))
            {
                return false;
            }

            var consumedStart = matchIndex;
            var consumedEnd = matchIndex + matchLength;

            if (token.LStrip)
            {
                while (consumedStart > 0 && char.IsWhiteSpace(text[consumedStart - 1]))
                {
                    consumedStart--;
                }
            }

            if (token.RStrip)
            {
                while (consumedEnd < text.Length && char.IsWhiteSpace(text[consumedEnd]))
                {
                    consumedEnd++;
                }
            }

            match = new BpeRuntimeAddedTokenMatch(matchIndex, consumedStart, consumedEnd, token);
            return true;
        }

        private static bool HasSingleWordBoundaries(string text, int startIndex, int length)
        {
            var leftBoundary = startIndex == 0 || !IsWordCharacter(text[startIndex - 1]);
            var rightIndex = startIndex + length;
            var rightBoundary = rightIndex >= text.Length || !IsWordCharacter(text[rightIndex]);
            return leftBoundary && rightBoundary;
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string? DecodeBpeCore(BpeModel model, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, HashSet<int> specialTokenIds, IEnumerable<int> ids, bool skipSpecialTokens)
        {
            if (addedTokensById.Count == 0)
            {
                return model.Decode(skipSpecialTokens ? ids.Where(id => !specialTokenIds.Contains(id)) : ids);
            }

            var effectiveIds = skipSpecialTokens ? ids.Where(id => !specialTokenIds.Contains(id)) : ids;
            return DecodeBpeWithAddedTokens(model, addedTokensById, effectiveIds);
        }

        private static string? DecodeSentencePieceCore(
            Func<IEnumerable<int>, bool, bool, bool, string?> decodeCore,
            IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById,
            IEnumerable<int> ids,
            bool skipSpecialTokens)
        {
            if (addedTokensById.Count == 0)
            {
                return decodeCore(ids, skipSpecialTokens, true, true);
            }

            var idList = ids.ToList();
            var decodedParts = new List<string>();
            var regularIds = new List<int>();
            var emittedContentBefore = false;

            void FlushRegularIds(int nextIndex)
            {
                if (regularIds.Count == 0)
                {
                    return;
                }

                var trimLeadingBoundary = !emittedContentBefore;
                var trimTrailingBoundary = !HasEmittedContentAfter(idList, nextIndex, addedTokensById, skipSpecialTokens);
                var decoded = decodeCore(regularIds, skipSpecialTokens, trimLeadingBoundary, trimTrailingBoundary);
                if (!string.IsNullOrEmpty(decoded))
                {
                    decodedParts.Add(decoded);
                    emittedContentBefore = true;
                }

                regularIds.Clear();
            }

            for (var index = 0; index < idList.Count; index++)
            {
                var id = idList[index];
                if (addedTokensById.TryGetValue(id, out var addedToken))
                {
                    FlushRegularIds(index);
                    if (!skipSpecialTokens || !addedToken.IsSpecial)
                    {
                        decodedParts.Add(addedToken.Content);
                        emittedContentBefore = true;
                    }

                    continue;
                }

                regularIds.Add(id);
            }

            FlushRegularIds(idList.Count);
            return string.Concat(decodedParts);
        }

        private static bool HasEmittedContentAfter(
            IReadOnlyList<int> ids,
            int startIndex,
            IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById,
            bool skipSpecialTokens)
        {
            for (var index = startIndex; index < ids.Count; index++)
            {
                var id = ids[index];
                if (addedTokensById.TryGetValue(id, out var addedToken))
                {
                    if (!skipSpecialTokens || !addedToken.IsSpecial)
                    {
                        return true;
                    }

                    continue;
                }

                return true;
            }

            return false;
        }

        private static string? DecodeBpeWithAddedTokens(BpeModel model, IReadOnlyDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, IEnumerable<int> ids)
        {
            var decodedParts = new List<string>();
            var regularIds = new List<int>();

            void FlushRegularIds()
            {
                if (regularIds.Count == 0)
                {
                    return;
                }

                var decoded = model.Decode(regularIds);
                if (decoded != null && decoded.Length > 0)
                {
                    decodedParts.Add(decoded);
                }

                regularIds.Clear();
            }

            foreach (var id in ids)
            {
                if (addedTokensById.TryGetValue(id, out var addedToken))
                {
                    FlushRegularIds();
                    decodedParts.Add(addedToken.Content);
                }
                else
                {
                    regularIds.Add(id);
                }
            }

            FlushRegularIds();
            return string.Concat(decodedParts);
        }

        private static void AddBpeAddedTokens(JArray? addedTokens, IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById)
        {
            if (addedTokens == null)
            {
                return;
            }

            foreach (var addedToken in addedTokens.OfType<JObject>())
            {
                var id = addedToken["id"]?.Value<int?>();
                var content = addedToken["content"]?.Value<string>();
                if (id.HasValue && !string.IsNullOrEmpty(content))
                {
                    var isSpecial = addedToken["special"]?.Value<bool?>() ?? false;
                    AddOrUpdateBpeAddedToken(
                        addedTokensById,
                        id.Value,
                        content!,
                        isSpecial,
                        addedToken["single_word"]?.Value<bool?>() ?? false,
                        addedToken["lstrip"]?.Value<bool?>() ?? false,
                        addedToken["rstrip"]?.Value<bool?>() ?? false,
                        addedToken["normalized"]?.Value<bool?>() ?? !isSpecial);
                }
            }
        }

        private static IDecoder RequireDecoder(global::KitsuMate.Tokenizers.Tokenizer tokenizer)
        {
            if (tokenizer.Decoder != null)
            {
                return tokenizer.Decoder;
            }

            throw new InvalidOperationException("Tokenizer decoder is not configured.");
        }

        private static void AddBpeAddedTokens(IReadOnlyList<TokenizerJsonAddedToken> addedTokens, IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById)
        {
            foreach (var addedToken in addedTokens)
            {
                AddOrUpdateBpeAddedToken(
                    addedTokensById,
                    addedToken.Id,
                    addedToken.Content,
                    addedToken.Special,
                    addedToken.SingleWord,
                    addedToken.LStrip,
                    addedToken.RStrip,
                    addedToken.Normalized);
            }
        }

        private static IEnumerable<string> ReadConfiguredSpecialTokenContents(JObject? root)
        {
            if (root == null)
            {
                yield break;
            }

            var contents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var propertyName in new[]
                     {
                         "bos_token",
                         "eos_token",
                         "pad_token",
                         "cls_token",
                         "sep_token",
                         "mask_token",
                         "unk_token",
                         "additional_special_tokens",
                     })
            {
                AddSpecialTokenContent(root[propertyName], contents);
            }

            foreach (var content in contents)
            {
                yield return content;
            }
        }

        private static void AddSpecialTokenContent(JToken? token, ISet<string> contents)
        {
            switch (token)
            {
                case null:
                    return;
                case JValue value when value.Type == JTokenType.String:
                    var content = value.Value<string>();
                    if (!string.IsNullOrEmpty(content))
                    {
                        contents.Add(content!);
                    }

                    return;
                case JObject obj:
                    AddSpecialTokenContent(obj["content"], contents);
                    return;
                case JArray array:
                    foreach (var item in array)
                    {
                        AddSpecialTokenContent(item, contents);
                    }

                    return;
            }
        }

        private static void AddBpeSpecialTokenByContent(BpeModel model, IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, string content)
        {
            var id = model.TokenToId(content);
            if (id.HasValue)
            {
                AddOrUpdateBpeAddedToken(addedTokensById, id.Value, content, isSpecial: true);
            }
        }

        private static void AddOrUpdateBpeAddedToken(IDictionary<int, BpeRuntimeAddedTokenInfo> addedTokensById, int id, string content, bool isSpecial, bool singleWord = false, bool lStrip = false, bool rStrip = false, bool normalized = false)
        {
            if (addedTokensById.TryGetValue(id, out var existing))
            {
                addedTokensById[id] = new BpeRuntimeAddedTokenInfo(
                    id,
                    existing.Content,
                    existing.IsSpecial || isSpecial,
                    existing.SingleWord || singleWord,
                    existing.LStrip || lStrip,
                    existing.RStrip || rStrip,
                    existing.Normalized || normalized,
                    existing.NormalizedContent);
                return;
            }

            addedTokensById[id] = new BpeRuntimeAddedTokenInfo(id, content, isSpecial, singleWord, lStrip, rStrip, normalized, null);
        }
    }

    internal sealed class BpeRuntimeAddedTokenInfo
    {
        public BpeRuntimeAddedTokenInfo(int id, string content, bool isSpecial, bool singleWord, bool lStrip, bool rStrip, bool normalized, string? normalizedContent)
        {
            Id = id;
            Content = content;
            IsSpecial = isSpecial;
            SingleWord = singleWord;
            LStrip = lStrip;
            RStrip = rStrip;
            Normalized = normalized;
            NormalizedContent = normalizedContent;
        }

        public int Id { get; }

        public string Content { get; }

        public bool IsSpecial { get; }

        public bool SingleWord { get; }

        public bool LStrip { get; }

        public bool RStrip { get; }

        public bool Normalized { get; }

        public string? NormalizedContent { get; }

        public int MatchLength => NormalizedContent?.Length ?? Content.Length;

        public BpeRuntimeAddedTokenInfo WithNormalizedContent(string normalizedContent)
        {
            return new BpeRuntimeAddedTokenInfo(Id, Content, IsSpecial, SingleWord, LStrip, RStrip, Normalized, normalizedContent);
        }
    }

    internal sealed class BpeRuntimeAddedTokenMatch
    {
        public BpeRuntimeAddedTokenMatch(int start, int consumedStart, int consumedEnd, BpeRuntimeAddedTokenInfo token)
        {
            Start = start;
            ConsumedStart = consumedStart;
            ConsumedEnd = consumedEnd;
            Token = token;
        }

        public int Start { get; }

        public int ConsumedStart { get; }

        public int ConsumedEnd { get; }

        public BpeRuntimeAddedTokenInfo Token { get; }
    }
}