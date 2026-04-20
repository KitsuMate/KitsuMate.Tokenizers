using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Shared BPE model with the first native encode path for character-level and byte-level tokenizers.
    /// </summary>
    public sealed class BpeModel : ITokenizerModel
    {
        private static readonly Regex ByteLevelWordOrNonWordRegex = new Regex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", RegexOptions.Compiled);
        private readonly Dictionary<string, int> _vocabulary;
        private readonly Dictionary<int, string> _reverseVocabulary;
        private readonly Dictionary<(string Left, string Right), BpeMergeRule> _mergeRules;

        public BpeModel(Dictionary<string, int> vocabulary, IReadOnlyList<BpeMerge> merges, BpeTokenizerOptions? options = null, string? name = null)
        {
            _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
            _reverseVocabulary = _vocabulary.ToDictionary(pair => pair.Value, pair => pair.Key);
            Merges = merges ?? throw new ArgumentNullException(nameof(merges));
            Options = options ?? new BpeTokenizerOptions();
            _mergeRules = BuildMergeRules(_vocabulary, Merges, Options.ContinuingSubwordPrefix);
            Name = string.IsNullOrWhiteSpace(name) ? "bpe" : name;
        }

        public string Name { get; }

        public TokenizerBackendType BackendType => TokenizerBackendType.Bpe;

        public bool SupportsDecode => true;

        public IReadOnlyList<BpeMerge> Merges { get; }

        public BpeTokenizerOptions Options { get; }

        public static BpeModel FromFiles(string vocabPath, string mergesPath, BpeTokenizerOptions? options = null)
        {
            if (!File.Exists(vocabPath))
            {
                throw new FileNotFoundException($"Vocabulary file not found: {vocabPath}", vocabPath);
            }

            if (!File.Exists(mergesPath))
            {
                throw new FileNotFoundException($"Merges file not found: {mergesPath}", mergesPath);
            }

            var vocabulary = LoadVocabulary(JObject.Parse(File.ReadAllText(vocabPath)));
            var merges = LoadMerges(File.ReadLines(mergesPath));
            return new BpeModel(vocabulary, merges, options, Path.GetFileNameWithoutExtension(vocabPath));
        }

        public static BpeModel FromBytes(byte[] vocab, byte[] merges, BpeTokenizerOptions? options = null, string? name = null)
        {
            if (vocab == null)
            {
                throw new ArgumentNullException(nameof(vocab));
            }

            if (merges == null)
            {
                throw new ArgumentNullException(nameof(merges));
            }

            using var vocabStream = new MemoryStream(vocab, writable: false);
            using var mergesStream = new MemoryStream(merges, writable: false);
            return FromStreams(vocabStream, mergesStream, options, name);
        }

        public static BpeModel FromStreams(Stream vocabStream, Stream mergesStream, BpeTokenizerOptions? options = null, string? name = null)
        {
            if (vocabStream == null)
            {
                throw new ArgumentNullException(nameof(vocabStream));
            }

            if (mergesStream == null)
            {
                throw new ArgumentNullException(nameof(mergesStream));
            }

            using var vocabReader = new StreamReader(vocabStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            using var mergesReader = new StreamReader(mergesStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var vocabulary = LoadVocabulary(JObject.Parse(vocabReader.ReadToEnd()));
            var merges = LoadMerges(ReadLines(mergesReader));
            return new BpeModel(vocabulary, merges, options, string.IsNullOrWhiteSpace(name) ? "bpe" : name);
        }

        public static BpeModel FromTokenizerJson(JObject root, JObject? tokenizerConfigRoot = null, BpeTokenizerOptions? options = null)
        {
            var model = root["model"] as JObject ?? throw new InvalidOperationException("tokenizer.json is missing the model section.");
            var vocab = model["vocab"] as JObject ?? throw new InvalidOperationException("BPE tokenizer.json is missing model.vocab.");
            var mergesToken = model["merges"];

            var effectiveOptions = options ?? CreateOptions(root, tokenizerConfigRoot);
            var vocabulary = LoadVocabulary(vocab);
            var merges = LoadMergesFromJson(mergesToken);

            return new BpeModel(vocabulary, merges, effectiveOptions, "bpe-json");
        }

        public int? TokenToId(string token)
        {
            if (token == null)
            {
                return null;
            }

            return _vocabulary.TryGetValue(token, out var id) ? id : null;
        }

        public string? IdToToken(int id)
        {
            return _reverseVocabulary.TryGetValue(id, out var token) ? token : null;
        }

        public IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                return Array.Empty<int>();
            }

            return Encode(text, maxTokenCount)
                .Select(piece => piece.Id)
                .ToList();
        }

        internal IReadOnlyList<BpeTokenPiece> Encode(string text, int maxTokenCount = int.MaxValue)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                return Array.Empty<BpeTokenPiece>();
            }

            var pieces = new List<BpeTokenPiece>();
            foreach (var segment in GetSegments(text))
            {
                foreach (var piece in EncodeSegment(segment))
                {
                    pieces.Add(piece);
                    if (pieces.Count >= maxTokenCount)
                    {
                        return pieces;
                    }
                }
            }

            return pieces;
        }

        public string? Decode(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var tokens = ids.Select(id => IdToToken(id) ?? Options.UnknownToken ?? string.Empty).ToList();
            if (Options.UseByteLevel)
            {
                return new Decoders.ByteLevelDecoder().Decode(tokens);
            }

            if (!string.IsNullOrEmpty(Options.EndOfWordSuffix))
            {
                return new Decoders.BpeDecoder(Options.EndOfWordSuffix).Decode(tokens);
            }

            return string.Concat(tokens);
        }

        internal IReadOnlyList<BpeTokenPiece> EncodeSegment(SegmentInput segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            if (segment.Text.Length == 0)
            {
                return Array.Empty<BpeTokenPiece>();
            }

            var symbols = BuildInitialSymbols(segment);
            if (symbols.Count == 0)
            {
                return Array.Empty<BpeTokenPiece>();
            }

            while (TryFindBestMerge(symbols, out var mergeIndex, out var mergeRule))
            {
                MergeAt(symbols, mergeIndex, mergeRule!);
            }

            return symbols;
        }

        private static Dictionary<string, int> LoadVocabulary(JObject vocab)
        {
            var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in vocab.Properties())
            {
                vocabulary[property.Name] = property.Value.Value<int>();
            }

            return vocabulary;
        }

        private static IReadOnlyList<BpeMerge> LoadMerges(IEnumerable<string> lines)
        {
            var merges = new List<BpeMerge>();
            var rank = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    merges.Add(new BpeMerge(parts[0], parts[1], rank));
                    rank++;
                }
            }

            return merges;
        }

        private static IEnumerable<string> ReadLines(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private static IReadOnlyList<BpeMerge> LoadMergesFromJson(JToken? mergesToken)
        {
            if (mergesToken is not JArray mergesArray)
            {
                return Array.Empty<BpeMerge>();
            }

            var merges = new List<BpeMerge>();
            for (var index = 0; index < mergesArray.Count; index++)
            {
                var entry = mergesArray[index];
                if (entry.Type == JTokenType.String)
                {
                    var parts = entry.Value<string>()?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts != null && parts.Length >= 2)
                    {
                        merges.Add(new BpeMerge(parts[0], parts[1], index));
                    }
                }
                else if (entry is JArray pair && pair.Count >= 2)
                {
                    merges.Add(new BpeMerge(pair[0]!.Value<string>()!, pair[1]!.Value<string>()!, index));
                }
            }

            return merges;
        }

        private static BpeTokenizerOptions CreateOptions(JObject root, JObject? tokenizerConfigRoot)
        {
            var options = new BpeTokenizerOptions();
            var model = root["model"] as JObject;
            if (model != null)
            {
                options.UnknownToken = model["unk_token"]?.Value<string>();
                options.ContinuingSubwordPrefix = model["continuing_subword_prefix"]?.Value<string>();
                options.EndOfWordSuffix = model["end_of_word_suffix"]?.Value<string>();
            }

            var preTokenizer = TokenizerJsonComponentFactory.ParsePreTokenizerConfig(root["pre_tokenizer"] as JObject);
            var decoder = TokenizerJsonComponentFactory.ParseDecoderConfig(root["decoder"] as JObject);
            if (TokenizerJsonComponentFactory.UsesByteLevel(preTokenizer, decoder))
            {
                options.UseByteLevel = true;
                options.AddPrefixSpace = preTokenizer?.AddPrefixSpace ?? false;
                options.UseRegex = preTokenizer?.UseRegex ?? true;
            }

            options.CleanUpTokenizationSpaces = root["clean_up_tokenization_spaces"]?.Value<bool?>() ?? options.CleanUpTokenizationSpaces;
            if (tokenizerConfigRoot != null)
            {
                options.UnknownToken ??= tokenizerConfigRoot["unk_token"]?.Value<string>();
                options.CleanUpTokenizationSpaces = tokenizerConfigRoot["clean_up_tokenization_spaces"]?.Value<bool?>() ?? options.CleanUpTokenizationSpaces;
            }

            return options;
        }
        private static Dictionary<(string Left, string Right), BpeMergeRule> BuildMergeRules(
            IReadOnlyDictionary<string, int> vocabulary,
            IEnumerable<BpeMerge> merges,
            string? continuingSubwordPrefix)
        {
            var mergeRules = new Dictionary<(string Left, string Right), BpeMergeRule>();
            var prefixLength = string.IsNullOrEmpty(continuingSubwordPrefix) ? 0 : continuingSubwordPrefix.Length;

            foreach (var merge in merges)
            {
                var mergedToken = prefixLength > 0 && continuingSubwordPrefix != null && merge.Right.StartsWith(continuingSubwordPrefix, StringComparison.Ordinal)
                    ? merge.Left + merge.Right.Substring(prefixLength)
                    : merge.Left + merge.Right;

                if (vocabulary.TryGetValue(mergedToken, out var mergedId))
                {
                    mergeRules[(merge.Left, merge.Right)] = new BpeMergeRule(merge.Rank, mergedToken, mergedId);
                }
            }

            return mergeRules;
        }

        private List<BpeTokenPiece> BuildInitialSymbols(SegmentInput segment)
        {
            if (Options.UseByteLevel)
            {
                return BuildByteLevelSymbols(segment);
            }

            var textElementIndexes = StringInfo.ParseCombiningCharacters(segment.Text);
            var symbols = new List<BpeTokenPiece>(textElementIndexes.Length);

            for (var index = 0; index < textElementIndexes.Length; index++)
            {
                var start = textElementIndexes[index];
                var end = index + 1 < textElementIndexes.Length ? textElementIndexes[index + 1] : segment.Text.Length;
                var value = segment.Text.Substring(start, end - start);

                if (index > 0 && !string.IsNullOrEmpty(Options.ContinuingSubwordPrefix))
                {
                    value = Options.ContinuingSubwordPrefix + value;
                }

                if (index == textElementIndexes.Length - 1 && !string.IsNullOrEmpty(Options.EndOfWordSuffix))
                {
                    value += Options.EndOfWordSuffix;
                }

                if (_vocabulary.TryGetValue(value, out var id))
                {
                    symbols.Add(new BpeTokenPiece(value, id, segment.Offset + start, segment.Offset + end, segment.WordIndex));
                    continue;
                }

                if (!string.IsNullOrEmpty(Options.UnknownToken) && _vocabulary.TryGetValue(Options.UnknownToken, out var unknownId))
                {
                    symbols.Add(new BpeTokenPiece(Options.UnknownToken, unknownId, segment.Offset + start, segment.Offset + end, segment.WordIndex));
                    continue;
                }

                throw new InvalidOperationException($"BPE vocabulary is missing token '{value}' and no unknown token is configured.");
            }

            return symbols;
        }

        private List<BpeTokenPiece> BuildByteLevelSymbols(SegmentInput segment)
        {
            var rawSymbols = new List<(string Value, int Start, int End)>();
            var textElementIndexes = StringInfo.ParseCombiningCharacters(segment.Text);

            for (var index = 0; index < textElementIndexes.Length; index++)
            {
                var start = textElementIndexes[index];
                var end = index + 1 < textElementIndexes.Length ? textElementIndexes[index + 1] : segment.Text.Length;
                var textElement = segment.Text.Substring(start, end - start);
                var mapped = TokenizerUtils.ApplyByteLevelMapping(textElement);
                var absoluteStart = AdjustByteLevelOffset(segment, start);
                var absoluteEnd = AdjustByteLevelOffset(segment, end);

                foreach (var mappedChar in mapped)
                {
                    rawSymbols.Add((mappedChar.ToString(), absoluteStart, absoluteEnd));
                }
            }

            var symbols = new List<BpeTokenPiece>(rawSymbols.Count);
            for (var index = 0; index < rawSymbols.Count; index++)
            {
                var value = rawSymbols[index].Value;

                if (index > 0 && !string.IsNullOrEmpty(Options.ContinuingSubwordPrefix))
                {
                    value = Options.ContinuingSubwordPrefix + value;
                }

                if (index == rawSymbols.Count - 1 && !string.IsNullOrEmpty(Options.EndOfWordSuffix))
                {
                    value += Options.EndOfWordSuffix;
                }

                if (_vocabulary.TryGetValue(value, out var id))
                {
                    symbols.Add(new BpeTokenPiece(value, id, rawSymbols[index].Start, rawSymbols[index].End, segment.WordIndex));
                    continue;
                }

                if (!string.IsNullOrEmpty(Options.UnknownToken) && _vocabulary.TryGetValue(Options.UnknownToken, out var unknownId))
                {
                    symbols.Add(new BpeTokenPiece(Options.UnknownToken, unknownId, rawSymbols[index].Start, rawSymbols[index].End, segment.WordIndex));
                    continue;
                }

                throw new InvalidOperationException($"BPE vocabulary is missing token '{value}' and no unknown token is configured.");
            }

            return symbols;
        }

        private int AdjustByteLevelOffset(SegmentInput segment, int localIndex)
        {
            var currentIndex = segment.CurrentOffset + localIndex;
            if (segment.PrefixAdjustment == 0)
            {
                return currentIndex;
            }

            return Math.Max(0, currentIndex - segment.PrefixAdjustment);
        }

        private IEnumerable<SegmentInput> GetSegments(string text)
        {
            if (Options.UseByteLevel)
            {
                return GetByteLevelSegments(text);
            }

            var wordIndex = 0;
            return TokenizerUtils.SplitByWhitespace(text)
                .Select(segment => new SegmentInput(text.Substring(segment.Offset, segment.Length), segment.Offset, segment.Offset, 0, wordIndex++));
        }

        private IEnumerable<SegmentInput> GetByteLevelSegments(string text)
        {
            var currentText = text;
            var prefixAdjustment = 0;
            if (Options.AddPrefixSpace && !string.IsNullOrEmpty(currentText) && !char.IsWhiteSpace(currentText[0]))
            {
                currentText = " " + currentText;
                prefixAdjustment = 1;
            }

            if (!Options.UseRegex)
            {
                if (currentText.Length > 0)
                {
                    yield return new SegmentInput(currentText, 0, 0, prefixAdjustment, 0);
                }

                yield break;
            }

            var wordIndex = 0;
            foreach (var (offset, length) in TokenizerUtils.SplitByRegex(currentText, ByteLevelWordOrNonWordRegex, "isolated"))
            {
                var segment = currentText.Substring(offset, length);
                if (segment.Length > 0)
                {
                    yield return new SegmentInput(segment, Math.Max(0, offset - prefixAdjustment), offset, prefixAdjustment, wordIndex++);
                }
            }
        }

        private bool TryFindBestMerge(IReadOnlyList<BpeTokenPiece> symbols, out int mergeIndex, out BpeMergeRule? mergeRule)
        {
            mergeIndex = -1;
            mergeRule = null;

            for (var index = 0; index < symbols.Count - 1; index++)
            {
                if (!_mergeRules.TryGetValue((symbols[index].Value, symbols[index + 1].Value), out var candidate))
                {
                    continue;
                }

                if (mergeRule == null || candidate.Rank < mergeRule.Rank)
                {
                    mergeRule = candidate;
                    mergeIndex = index;
                }
            }

            return mergeRule != null;
        }

        private static void MergeAt(List<BpeTokenPiece> symbols, int mergeIndex, BpeMergeRule mergeRule)
        {
            var left = symbols[mergeIndex];
            var right = symbols[mergeIndex + 1];
            symbols[mergeIndex] = new BpeTokenPiece(mergeRule.Token, mergeRule.Id, left.Start, right.End, left.WordIndex);
            symbols.RemoveAt(mergeIndex + 1);
        }

        internal sealed class BpeTokenPiece
        {
            public BpeTokenPiece(string value, int id, int start, int end, int? wordIndex)
            {
                Value = value;
                Id = id;
                Start = start;
                End = end;
                WordIndex = wordIndex;
            }

            public string Value { get; }

            public int Id { get; }

            public int Start { get; }

            public int End { get; }

            public int? WordIndex { get; }
        }

        internal sealed class SegmentInput
        {
            public SegmentInput(string text, int offset, int currentOffset, int prefixAdjustment, int wordIndex)
            {
                Text = text;
                Offset = offset;
                CurrentOffset = currentOffset;
                PrefixAdjustment = prefixAdjustment;
                WordIndex = wordIndex;
            }

            public string Text { get; }

            public int Offset { get; }

            public int CurrentOffset { get; }

            public int PrefixAdjustment { get; }

            public int WordIndex { get; }
        }

        private sealed class BpeMergeRule
        {
            public BpeMergeRule(int rank, string token, int id)
            {
                Rank = rank;
                Token = token;
                Id = id;
            }

            public int Rank { get; }

            public string Token { get; }

            public int Id { get; }
        }
    }
}