using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Model for OpenAI-style .tiktoken mergeable-ranks vocabularies.
    /// </summary>
    public sealed class TiktokenModel : ITokenizerModel
    {
        private const string Cl100kBaseRegexPattern = @"'(?i:[sdmt]|ll|ve|re)|(?>[^\r\n\p{L}\p{N}]?)(?>\p{L}+)|(?>\p{N}{1,3})| ?(?>[^\s\p{L}\p{N}]+)(?>[\r\n]*)|(?>\s+)$|\s*[\r\n]|\s+(?!\S)|\s";
        private const string P50kBaseRegexPattern = @"'(?:[sdmt]|ll|ve|re)| ?(?>\p{L}+)| ?(?>\p{N}+)| ?(?>[^\s\p{L}\p{N}]+)|(?>\s+)$|\s+(?!\S)|\s";
        private const string O200kBaseRegexPattern = @"[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]*[\p{Ll}\p{Lm}\p{Lo}\p{M}]+(?i:'s|'t|'re|'ve|'m|'ll|'d)?|[^\r\n\p{L}\p{N}]?[\p{Lu}\p{Lt}\p{Lm}\p{Lo}\p{M}]+[\p{Ll}\p{Lm}\p{Lo}\p{M}]*(?i:'s|'t|'re|'ve|'m|'ll|'d)?|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n/]*|\s*[\r\n]+|\s+(?!\S)|\s+";
        private const string EndOfText = "<|endoftext|>";
        private const string EndOfPrompt = "<|endofprompt|>";
        private const string StartOfText = "<|startoftext|>";
        private const string Return = "<|return|>";
        private const string Constrain = "<|constrain|>";
        private const string Channel = "<|channel|>";
        private const string Start = "<|start|>";
        private const string End = "<|end|>";
        private const string Message = "<|message|>";
        private const string Call = "<|call|>";
        private const string ReservedPrefix = "<|reserved_";

        private static readonly Dictionary<string, string> ModelToEncoding = new(StringComparer.OrdinalIgnoreCase)
        {
            ["o1"] = "o200k_base",
            ["o3"] = "o200k_base",
            ["o4-mini"] = "o200k_base",
            ["gpt-5"] = "o200k_base",
            ["gpt-4.1"] = "o200k_base",
            ["gpt-4o"] = "o200k_base",
            ["gpt-4"] = "cl100k_base",
            ["gpt-3.5-turbo"] = "cl100k_base",
            ["gpt-3.5"] = "cl100k_base",
            ["gpt-3.5-turbo-16k"] = "cl100k_base",
            ["gpt-35"] = "cl100k_base",
            ["gpt-35-turbo"] = "cl100k_base",
            ["gpt-35-turbo-16k"] = "cl100k_base",
            ["davinci-002"] = "cl100k_base",
            ["babbage-002"] = "cl100k_base",
            ["text-embedding-ada-002"] = "cl100k_base",
            ["text-embedding-3-small"] = "cl100k_base",
            ["text-embedding-3-large"] = "cl100k_base",
            ["text-davinci-003"] = "p50k_base",
            ["text-davinci-002"] = "p50k_base",
            ["text-davinci-001"] = "r50k_base",
            ["text-curie-001"] = "r50k_base",
            ["text-babbage-001"] = "r50k_base",
            ["text-ada-001"] = "r50k_base",
            ["davinci"] = "r50k_base",
            ["curie"] = "r50k_base",
            ["babbage"] = "r50k_base",
            ["ada"] = "r50k_base",
            ["code-davinci-002"] = "p50k_base",
            ["code-davinci-001"] = "p50k_base",
            ["code-cushman-002"] = "p50k_base",
            ["code-cushman-001"] = "p50k_base",
            ["davinci-codex"] = "p50k_base",
            ["cushman-codex"] = "p50k_base",
            ["text-davinci-edit-001"] = "p50k_edit",
            ["code-davinci-edit-001"] = "p50k_edit",
            ["text-similarity-davinci-001"] = "r50k_base",
            ["text-similarity-curie-001"] = "r50k_base",
            ["text-similarity-babbage-001"] = "r50k_base",
            ["text-similarity-ada-001"] = "r50k_base",
            ["text-search-davinci-doc-001"] = "r50k_base",
            ["text-search-curie-doc-001"] = "r50k_base",
            ["text-search-babbage-doc-001"] = "r50k_base",
            ["text-search-ada-doc-001"] = "r50k_base",
            ["code-search-babbage-code-001"] = "r50k_base",
            ["code-search-ada-code-001"] = "r50k_base",
            ["gpt2"] = "gpt2",
            ["gpt-2"] = "gpt2",
            ["phi-4"] = "cl100k_base",
        };

        private static readonly (string Prefix, string Encoding)[] ModelPrefixToEncoding =
        {
            ("o1-", "o200k_base"),
            ("o3-", "o200k_base"),
            ("o4-mini-", "o200k_base"),
            ("gpt-5-", "o200k_base"),
            ("gpt-4.1-", "o200k_base"),
            ("gpt-4.5-", "o200k_base"),
            ("gpt-4o-", "o200k_base"),
            ("chatgpt-4o-", "o200k_base"),
            ("gpt-4-", "cl100k_base"),
            ("gpt-3.5-", "cl100k_base"),
            ("gpt-35-", "cl100k_base"),
            ("gpt-oss-", "o200k_harmony"),
            ("ft:gpt-4o", "o200k_base"),
            ("ft:gpt-4", "cl100k_base"),
            ("ft:gpt-3.5-turbo", "cl100k_base"),
            ("ft:davinci-002", "cl100k_base"),
            ("ft:babbage-002", "cl100k_base"),
        };

        private readonly Dictionary<byte[], int> _mergeableRanks;
        private readonly Dictionary<int, byte[]> _decoder;
        private readonly Dictionary<int, string> _tokenStringsById;
        private readonly Dictionary<string, int> _specialTokens;
        private readonly Dictionary<int, string> _specialTokensById;
        private readonly HashSet<int> _specialTokenIds;
        private readonly Dictionary<string, int> _tokenIdsByString;
        private readonly Regex _splitRegex;
        private readonly Regex? _specialTokensRegex;

        internal TiktokenModel(
            string name,
            Dictionary<byte[], int> mergeableRanks,
            Dictionary<int, byte[]> decoder,
            Dictionary<int, string> tokenStringsById,
            Regex splitRegex,
            Dictionary<string, int> specialTokens)
        {
            Name = name;
            _mergeableRanks = mergeableRanks;
            _decoder = decoder;
            _tokenStringsById = tokenStringsById;
            _splitRegex = splitRegex;
            _specialTokens = specialTokens;
            _specialTokensById = specialTokens.ToDictionary(pair => pair.Value, pair => pair.Key);
            _specialTokenIds = new HashSet<int>(specialTokens.Values);
            _specialTokensRegex = specialTokens.Count == 0
                ? null
                : new Regex(string.Join("|", specialTokens.Keys.OrderByDescending(token => token.Length).Select(Regex.Escape)), RegexOptions.Compiled);
            _tokenIdsByString = tokenStringsById.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
            foreach (var specialToken in specialTokens)
            {
                _tokenIdsByString[specialToken.Key] = specialToken.Value;
            }
        }

        public string Name { get; }

        public TokenizerBackendType BackendType => TokenizerBackendType.Tiktoken;

        public bool SupportsDecode => true;

        internal Dictionary<byte[], int> MergeableRanks => _mergeableRanks;

        internal Dictionary<int, byte[]> DecoderMap => _decoder;

        internal Dictionary<int, string> TokenStringsById => _tokenStringsById;

        internal Dictionary<string, int> SpecialTokens => _specialTokens;

        internal Regex SplitRegex => _splitRegex;

        internal bool IsSpecialTokenId(int id) => _specialTokenIds.Contains(id);

        public static TiktokenModel FromFile(string vocabPath, string? encodingName = null)
        {
            if (string.IsNullOrWhiteSpace(vocabPath))
            {
                throw new ArgumentException("Vocab path cannot be null or empty.", nameof(vocabPath));
            }

            if (!File.Exists(vocabPath))
            {
                throw new FileNotFoundException($"Tiktoken vocab file not found: {vocabPath}", vocabPath);
            }

            var config = ResolveConfiguration(Path.GetFileNameWithoutExtension(vocabPath), encodingName);
            using var stream = File.OpenRead(vocabPath);
            var (mergeableRanks, decoder, tokenStringsById) = LoadMergeableRanks(stream);
            return new TiktokenModel(config.Name, mergeableRanks, decoder, tokenStringsById, config.Regex, config.SpecialTokens);
        }

        public static TiktokenModel FromBytes(byte[] vocab, string? encodingName = null)
        {
            if (vocab == null)
            {
                throw new ArgumentNullException(nameof(vocab));
            }

            using var stream = new MemoryStream(vocab, writable: false);
            return FromStream(stream, encodingName);
        }

        public static TiktokenModel FromStream(Stream vocabStream, string? encodingName = null)
        {
            if (vocabStream == null)
            {
                throw new ArgumentNullException(nameof(vocabStream));
            }

            var config = ResolveConfiguration(sourceName: null, encodingName);
            var (mergeableRanks, decoder, tokenStringsById) = LoadMergeableRanks(vocabStream);
            return new TiktokenModel(config.Name, mergeableRanks, decoder, tokenStringsById, config.Regex, config.SpecialTokens);
        }

        public int? TokenToId(string token)
        {
            if (token == null)
            {
                return null;
            }

            return _tokenIdsByString.TryGetValue(token, out var id) ? id : null;
        }

        public string? IdToToken(int id)
        {
            if (_specialTokensById.TryGetValue(id, out var specialToken))
            {
                return specialToken;
            }

            return _tokenStringsById.TryGetValue(id, out var token) ? token : null;
        }

        public IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
        {
            return Encode(text, maxTokenCount).Select(piece => piece.Id).ToList();
        }

        public string? Decode(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var builder = new StringBuilder();
            var utf8Bytes = new List<byte>();

            foreach (var id in ids)
            {
                if (_specialTokensById.TryGetValue(id, out var specialToken))
                {
                    FlushPendingBytes();
                    builder.Append(specialToken);
                    continue;
                }

                if (_decoder.TryGetValue(id, out var bytes))
                {
                    utf8Bytes.AddRange(bytes);
                }
            }

            FlushPendingBytes();
            return builder.ToString();

            void FlushPendingBytes()
            {
                if (utf8Bytes.Count == 0)
                {
                    return;
                }

                builder.Append(Encoding.UTF8.GetString(utf8Bytes.ToArray()));
                utf8Bytes.Clear();
            }
        }

        internal IReadOnlyList<TokenPiece> Encode(string text, int maxTokenCount)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var pieces = new List<TokenPiece>();
            var wordIndex = 0;

            foreach (var segment in SplitSegments(text))
            {
                if (pieces.Count >= maxTokenCount)
                {
                    break;
                }

                if (segment.IsSpecial)
                {
                    if (_specialTokens.TryGetValue(segment.Text, out var specialId))
                    {
                        pieces.Add(new TokenPiece(specialId, segment.Text, segment.Offset, segment.Offset + segment.Length, null, isSpecial: true));
                    }

                    continue;
                }

                foreach (var piece in EncodeOrdinarySegment(segment, wordIndex))
                {
                    pieces.Add(piece);
                    if (pieces.Count >= maxTokenCount)
                    {
                        return pieces;
                    }
                }

                wordIndex++;
            }

            return pieces;
        }

        private IEnumerable<TokenPiece> EncodeOrdinarySegment(TextSegment segment, int wordIndex)
        {
            var symbols = BuildInitialSymbols(segment, wordIndex);
            if (symbols.Count == 0)
            {
                yield break;
            }

            while (TryFindBestMerge(symbols, out var mergeIndex, out var mergedBytes))
            {
                var rank = _mergeableRanks[mergedBytes];
                var left = symbols[mergeIndex];
                var right = symbols[mergeIndex + 1];
                symbols[mergeIndex] = new ByteSymbol(mergedBytes, rank, left.Start, right.End, left.WordIndex);
                symbols.RemoveAt(mergeIndex + 1);
            }

            foreach (var symbol in symbols)
            {
                yield return new TokenPiece(symbol.Id, _tokenStringsById[symbol.Id], symbol.Start, symbol.End, symbol.WordIndex, isSpecial: false);
            }
        }

        private List<ByteSymbol> BuildInitialSymbols(TextSegment segment, int wordIndex)
        {
            var textElementIndexes = StringInfo.ParseCombiningCharacters(segment.Text);
            var symbols = new List<ByteSymbol>();

            for (var index = 0; index < textElementIndexes.Length; index++)
            {
                var start = textElementIndexes[index];
                var end = index + 1 < textElementIndexes.Length ? textElementIndexes[index + 1] : segment.Text.Length;
                var textElement = segment.Text.Substring(start, end - start);
                var bytes = Encoding.UTF8.GetBytes(textElement);

                foreach (var value in bytes)
                {
                    var tokenBytes = new[] { value };
                    if (!_mergeableRanks.TryGetValue(tokenBytes, out var id))
                    {
                        throw new InvalidOperationException($"Tiktoken ranks are missing base byte token 0x{value:X2}.");
                    }

                    symbols.Add(new ByteSymbol(tokenBytes, id, segment.Offset + start, segment.Offset + end, wordIndex));
                }
            }

            return symbols;
        }

        private bool TryFindBestMerge(IReadOnlyList<ByteSymbol> symbols, out int mergeIndex, out byte[] mergedBytes)
        {
            mergeIndex = -1;
            mergedBytes = Array.Empty<byte>();
            var bestRank = int.MaxValue;

            for (var index = 0; index < symbols.Count - 1; index++)
            {
                var candidate = Concatenate(symbols[index].Bytes, symbols[index + 1].Bytes);
                if (!_mergeableRanks.TryGetValue(candidate, out var rank))
                {
                    continue;
                }

                if (rank < bestRank)
                {
                    bestRank = rank;
                    mergeIndex = index;
                    mergedBytes = candidate;
                }
            }

            return mergeIndex >= 0;
        }

        private IEnumerable<TextSegment> SplitSegments(string text)
        {
            var beginning = 0;

            if (_specialTokensRegex != null)
            {
                while (TryGetMatch(_specialTokensRegex, text, beginning, text.Length - beginning, out var specialMatch))
                {
                    while (TryGetMatch(_splitRegex, text, beginning, specialMatch.Offset - beginning, out var match))
                    {
                        yield return new TextSegment(text.Substring(match.Offset, match.Length), match.Offset, match.Length, isSpecial: false);
                        beginning = match.Offset + match.Length;
                    }

                    yield return new TextSegment(text.Substring(specialMatch.Offset, specialMatch.Length), specialMatch.Offset, specialMatch.Length, isSpecial: true);
                    beginning = specialMatch.Offset + specialMatch.Length;
                }
            }

            while (TryGetMatch(_splitRegex, text, beginning, text.Length - beginning, out var remainingMatch))
            {
                yield return new TextSegment(text.Substring(remainingMatch.Offset, remainingMatch.Length), remainingMatch.Offset, remainingMatch.Length, isSpecial: false);
                beginning = remainingMatch.Offset + remainingMatch.Length;
            }
        }

        private static bool TryGetMatch(Regex regex, string text, int startAt, int length, out (int Offset, int Length) match)
        {
            match = default;
            if (length <= 0 || startAt >= text.Length)
            {
                return false;
            }

            var result = regex.Match(text, startAt);
            if (!result.Success || result.Index >= startAt + length)
            {
                return false;
            }

            match = (result.Index, result.Length);
            return true;
        }

        private static (Dictionary<byte[], int> MergeableRanks, Dictionary<int, byte[]> Decoder, Dictionary<int, string> TokenStringsById) LoadMergeableRanks(Stream vocabStream)
        {
            var mergeableRanks = new Dictionary<byte[], int>(ByteArrayComparer.Instance);
            var decoder = new Dictionary<int, byte[]>();
            var tokenStringsById = new Dictionary<int, string>();

            using var reader = new StreamReader(vocabStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var line = reader.ReadLine();
            const string capacityPrefix = "Capacity: ";
            if (line != null && line.StartsWith(capacityPrefix, StringComparison.Ordinal))
            {
                line = reader.ReadLine();
            }

            while (line != null && line.Length == 0)
            {
                line = reader.ReadLine();
            }

            if (line != null && line.IndexOf(' ') < 0)
            {
                var lineNumber = 0;
                do
                {
                    if (line.Length > 0)
                    {
                        AddData(Convert.FromBase64String(line), lineNumber);
                    }

                    lineNumber++;
                    line = reader.ReadLine();
                }
                while (line != null);

                return (mergeableRanks, decoder, tokenStringsById);
            }

            while (line != null)
            {
                if (line.Length > 0)
                {
                    var spaceIndex = line.IndexOf(' ');
                    if (spaceIndex <= 0 || spaceIndex >= line.Length - 1 || line.IndexOf(' ', spaceIndex + 1) >= 0)
                    {
                        throw new InvalidOperationException("Invalid format in the .tiktoken vocab file.");
                    }

                    var tokenBytes = Convert.FromBase64String(line.Substring(0, spaceIndex));
                    if (!int.TryParse(line.Substring(spaceIndex + 1), out var rank))
                    {
                        throw new InvalidOperationException("Invalid rank in the .tiktoken vocab file.");
                    }

                    AddData(tokenBytes, rank);
                }

                line = reader.ReadLine();
            }

            return (mergeableRanks, decoder, tokenStringsById);

            void AddData(byte[] tokenBytes, int rank)
            {
                mergeableRanks[tokenBytes] = rank;
                decoder[rank] = tokenBytes;
                tokenStringsById[rank] = TokenizerUtils.ApplyByteLevelMapping(tokenBytes);
            }
        }

        private static byte[] Concatenate(byte[] left, byte[] right)
        {
            var merged = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, merged, 0, left.Length);
            Buffer.BlockCopy(right, 0, merged, left.Length, right.Length);
            return merged;
        }

        private static TiktokenConfiguration ResolveConfiguration(string? sourceName, string? encodingName)
        {
            var requestedName = encodingName ?? sourceName;
            var name = ResolveEncodingName(requestedName);
            switch (name)
            {
                case "gpt2":
                case "r50k_base":
                case "p50k_base":
                    return new TiktokenConfiguration(name!, CreateCompiledRegex(P50kBaseRegexPattern), new Dictionary<string, int>
                    {
                        [EndOfText] = 50256,
                    });
                case "p50k_edit":
                    return new TiktokenConfiguration(name!, CreateCompiledRegex(P50kBaseRegexPattern), new Dictionary<string, int>
                    {
                        [EndOfText] = 50256,
                        ["<|fim_prefix|>"] = 50281,
                        ["<|fim_middle|>"] = 50282,
                        ["<|fim_suffix|>"] = 50283,
                    });
                case "cl100k_base":
                    return new TiktokenConfiguration(name!, CreateCompiledRegex(Cl100kBaseRegexPattern), new Dictionary<string, int>
                    {
                        [EndOfText] = 100257,
                        ["<|fim_prefix|>"] = 100258,
                        ["<|fim_middle|>"] = 100259,
                        ["<|fim_suffix|>"] = 100260,
                        [EndOfPrompt] = 100276,
                    });
                case "o200k_base":
                    return new TiktokenConfiguration(name!, CreateCompiledRegex(O200kBaseRegexPattern), new Dictionary<string, int>
                    {
                        [EndOfText] = 199999,
                        [EndOfPrompt] = 200018,
                    });
                case "o200k_harmony":
                    return new TiktokenConfiguration(name!, CreateCompiledRegex(O200kBaseRegexPattern), CreateO200kHarmonySpecialTokens());
                default:
                    throw new NotSupportedException($"Unsupported .tiktoken encoding or model '{requestedName}'. Supported encodings: gpt2, r50k_base, p50k_base, p50k_edit, cl100k_base, o200k_base, o200k_harmony.");
            }
        }

        private static string ResolveEncodingName(string? requestedName)
        {
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                throw new NotSupportedException("A .tiktoken encoding or model name is required.");
            }

            var name = requestedName.ToLowerInvariant();
            switch (name)
            {
                case "gpt2":
                case "r50k_base":
                case "p50k_base":
                case "p50k_edit":
                case "cl100k_base":
                case "o200k_base":
                case "o200k_harmony":
                    return name;
            }

            if (ModelToEncoding.TryGetValue(requestedName, out var exactEncoding))
            {
                return exactEncoding;
            }

            foreach (var (prefix, encoding) in ModelPrefixToEncoding)
            {
                if (requestedName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return encoding;
                }
            }

            return name;
        }

        private static Dictionary<string, int> CreateO200kHarmonySpecialTokens()
        {
            return new Dictionary<string, int>
            {
                [StartOfText] = 199998,
                [EndOfText] = 199999,
                [$"{ReservedPrefix}200000|>"] = 200000,
                [$"{ReservedPrefix}200001|>"] = 200001,
                [Return] = 200002,
                [Constrain] = 200003,
                [$"{ReservedPrefix}200004|>"] = 200004,
                [Channel] = 200005,
                [Start] = 200006,
                [End] = 200007,
                [Message] = 200008,
                [$"{ReservedPrefix}200009|>"] = 200009,
                [$"{ReservedPrefix}200010|>"] = 200010,
                [$"{ReservedPrefix}200011|>"] = 200011,
                [Call] = 200012,
                [$"{ReservedPrefix}200013|>"] = 200013,
                [$"{ReservedPrefix}200014|>"] = 200014,
                [$"{ReservedPrefix}200015|>"] = 200015,
                [$"{ReservedPrefix}200016|>"] = 200016,
                [$"{ReservedPrefix}200017|>"] = 200017,
                [EndOfPrompt] = 200018,
            };
        }

        private static Regex CreateCompiledRegex(string pattern)
        {
            return new Regex(pattern, RegexOptions.Compiled);
        }

        private readonly struct TiktokenConfiguration
        {
            public TiktokenConfiguration(string name, Regex regex, Dictionary<string, int> specialTokens)
            {
                Name = name;
                Regex = regex;
                SpecialTokens = specialTokens;
            }

            public string Name { get; }

            public Regex Regex { get; }

            public Dictionary<string, int> SpecialTokens { get; }
        }

        private readonly struct TextSegment
        {
            public TextSegment(string text, int offset, int length, bool isSpecial)
            {
                Text = text;
                Offset = offset;
                Length = length;
                IsSpecial = isSpecial;
            }

            public string Text { get; }

            public int Offset { get; }

            public int Length { get; }

            public bool IsSpecial { get; }
        }

        internal readonly struct TokenPiece
        {
            public TokenPiece(int id, string token, int start, int end, int? wordIndex, bool isSpecial)
            {
                Id = id;
                Token = token;
                Start = start;
                End = end;
                WordIndex = wordIndex;
                IsSpecial = isSpecial;
            }

            public int Id { get; }

            public string Token { get; }

            public int Start { get; }

            public int End { get; }

            public int? WordIndex { get; }

            public bool IsSpecial { get; }
        }

        private sealed class ByteSymbol
        {
            public ByteSymbol(byte[] bytes, int id, int start, int end, int? wordIndex)
            {
                Bytes = bytes;
                Id = id;
                Start = start;
                End = end;
                WordIndex = wordIndex;
            }

            public byte[] Bytes { get; }

            public int Id { get; }

            public int Start { get; }

            public int End { get; }

            public int? WordIndex { get; }
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static ByteArrayComparer Instance { get; } = new ByteArrayComparer();

            public bool Equals(byte[] x, byte[] y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null || x.Length != y.Length)
                {
                    return false;
                }

                for (var index = 0; index < x.Length; index++)
                {
                    if (x[index] != y[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(byte[] obj)
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var value in obj)
                    {
                        hash = hash * 31 + value;
                    }

                    return hash;
                }
            }
        }
    }
}