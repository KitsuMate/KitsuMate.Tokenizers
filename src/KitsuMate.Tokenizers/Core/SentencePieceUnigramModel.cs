using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KitsuMate.Tokenizers.Normalizers;
using Google.Protobuf;
using Sentencepiece;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Native SentencePiece Unigram model loaded directly from the protobuf model file.
    /// </summary>
    // TODO(native-runtime): Revisit how much of the protobuf loading and normalization helper
    // surface should remain model-owned once Tokenizer owns full runtime assembly. Keep only the
    // model-specific algorithm/data surface here if this remains a public ITokenizerModel type.
    public sealed class SentencePieceUnigramModel : ITokenizerModel
    {
        private const char DummyWhitespace = '\u2581';

        private readonly Dictionary<char, List<PieceEntry>> _piecesByFirstChar;
        private readonly Dictionary<int, PieceEntry> _piecesById;
        private readonly HashSet<int> _specialTokenIds;
        private readonly Dictionary<string, int> _pieceIdsByValue;
        private readonly PrecompiledCharsMap? _precompiledCharsMap;
        private readonly string _unknownToken;
        private readonly int _unknownId;
        private readonly bool _addDummyPrefix;
        private readonly bool _removeExtraWhitespaces;
        private readonly bool _escapeWhitespaces;
        private readonly bool _treatWhitespaceAsSuffix;
        private readonly bool _applyIdOffset;
        private readonly float _unknownPenalty;
        private readonly HashSet<int> _externalSpecialTokenIds;

        internal SentencePieceUnigramModel(
            string name,
            Dictionary<char, List<PieceEntry>> piecesByFirstChar,
            Dictionary<int, PieceEntry> piecesById,
            HashSet<int> specialTokenIds,
            PrecompiledCharsMap? precompiledCharsMap,
            string unknownToken,
            int unknownId,
            bool addDummyPrefix,
            bool removeExtraWhitespaces,
            bool escapeWhitespaces,
            bool treatWhitespaceAsSuffix,
            bool applyIdOffset,
            float unknownPenalty)
        {
            Name = name;
            _piecesByFirstChar = piecesByFirstChar;
            _piecesById = piecesById;
            _specialTokenIds = specialTokenIds;
            _precompiledCharsMap = precompiledCharsMap;
            _unknownToken = unknownToken;
            _unknownId = unknownId;
            _addDummyPrefix = addDummyPrefix;
            _removeExtraWhitespaces = removeExtraWhitespaces;
            _escapeWhitespaces = escapeWhitespaces;
            _treatWhitespaceAsSuffix = treatWhitespaceAsSuffix;
            _applyIdOffset = applyIdOffset;
            _unknownPenalty = unknownPenalty;
            _externalSpecialTokenIds = new HashSet<int>(_specialTokenIds.Select(ToExternalId));
            _pieceIdsByValue = _piecesById.Values
                .GroupBy(piece => piece.Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => ToExternalId(group.First().Id), StringComparer.Ordinal);
        }

        public string Name { get; }

        public TokenizerBackendType BackendType => TokenizerBackendType.SentencePieceUnigram;

        public bool SupportsDecode => true;

        internal Dictionary<char, List<PieceEntry>> PiecesByFirstChar => _piecesByFirstChar;

        internal Dictionary<int, PieceEntry> PiecesById => _piecesById;

        internal HashSet<int> SpecialTokenIds => _specialTokenIds;

        internal PrecompiledCharsMap? PrecompiledCharsMap => _precompiledCharsMap;

        internal string UnknownToken => _unknownToken;

        internal int UnknownId => _unknownId;

        internal bool AddDummyPrefix => _addDummyPrefix;

        internal bool RemoveExtraWhitespaces => _removeExtraWhitespaces;

        internal bool EscapeWhitespaces => _escapeWhitespaces;

        internal bool TreatWhitespaceAsSuffix => _treatWhitespaceAsSuffix;

        internal bool ApplyIdOffset => _applyIdOffset;

        internal float UnknownPenalty => _unknownPenalty;

        internal HashSet<int> ExternalSpecialTokenIds => _externalSpecialTokenIds;

        public static SentencePieceUnigramModel FromFile(string modelPath, bool applyIdOffset = false)
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}", modelPath);
            }

            using var modelStream = File.OpenRead(modelPath);
            return FromStream(modelStream, Path.GetFileNameWithoutExtension(modelPath), applyIdOffset);
        }

        public static SentencePieceUnigramModel FromBytes(byte[] modelBytes, string name = "sentencepiece", bool applyIdOffset = false)
        {
            if (modelBytes == null)
            {
                throw new ArgumentNullException(nameof(modelBytes));
            }

            using var modelStream = new MemoryStream(modelBytes, writable: false);
            return FromStream(modelStream, name, applyIdOffset);
        }

        public static SentencePieceUnigramModel FromStream(Stream modelStream, string name = "sentencepiece", bool applyIdOffset = false)
        {
            if (modelStream == null)
            {
                throw new ArgumentNullException(nameof(modelStream));
            }

            var model = ModelProto.Parser.ParseFrom(modelStream);
            if (model.TrainerSpec.ModelType != TrainerSpec.Types.ModelType.Unigram)
            {
                throw new NotSupportedException($"SentencePiece tokenizer currently supports only Unigram models. Model type was '{model.TrainerSpec.ModelType}'.");
            }

            var piecesByFirstChar = new Dictionary<char, List<PieceEntry>>();
            var piecesById = new Dictionary<int, PieceEntry>();
            var specialTokenIds = new HashSet<int>();
            var unknownId = Math.Max(0, model.TrainerSpec.UnkId);
            var unknownToken = string.IsNullOrEmpty(model.TrainerSpec.UnkPiece) ? "<unk>" : model.TrainerSpec.UnkPiece;
            var minScore = float.MaxValue;
            var normalizerSpec = model.NormalizerSpec ?? new NormalizerSpec();
            var precompiledCharsMap = PrecompiledCharsMap.FromBlob(normalizerSpec.PrecompiledCharsmap.Span);

            for (var index = 0; index < model.Pieces.Count; index++)
            {
                var piece = model.Pieces[index];
                var entry = new PieceEntry(index, piece.Piece ?? string.Empty, piece.Score, piece.Type);
                piecesById[index] = entry;

                if (piece.Type == ModelProto.Types.SentencePiece.Types.Type.Control ||
                    piece.Type == ModelProto.Types.SentencePiece.Types.Type.Unknown)
                {
                    specialTokenIds.Add(index);
                }

                if (!IsTokenizablePiece(piece.Type) || string.IsNullOrEmpty(entry.Value))
                {
                    continue;
                }

                minScore = Math.Min(minScore, entry.Score);

                var firstChar = entry.Value[0];
                if (!piecesByFirstChar.TryGetValue(firstChar, out var list))
                {
                    list = new List<PieceEntry>();
                    piecesByFirstChar[firstChar] = list;
                }

                list.Add(entry);
            }

            foreach (var list in piecesByFirstChar.Values)
            {
                list.Sort(static (left, right) =>
                {
                    var lengthCompare = right.Value.Length.CompareTo(left.Value.Length);
                    return lengthCompare != 0 ? lengthCompare : right.Score.CompareTo(left.Score);
                });
            }

            if (minScore == float.MaxValue)
            {
                minScore = -100f;
            }

            return new SentencePieceUnigramModel(
                name,
                piecesByFirstChar,
                piecesById,
                specialTokenIds,
                precompiledCharsMap,
                unknownToken,
                unknownId,
                normalizerSpec.AddDummyPrefix,
                normalizerSpec.RemoveExtraWhitespaces,
                normalizerSpec.EscapeWhitespaces,
                model.TrainerSpec?.TreatWhitespaceAsSuffix ?? false,
                applyIdOffset,
                minScore - 10f);
        }

        public int? TokenToId(string token)
        {
            if (token == null)
            {
                return null;
            }

            return _pieceIdsByValue.TryGetValue(token, out var id) ? id : null;
        }

        public string? IdToToken(int id)
        {
            return _piecesById.TryGetValue(ToModelId(id), out var piece) ? piece.Value : null;
        }

        public IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
        {
            var normalized = NormalizeText(text);
            return Tokenize(normalized.Value, maxTokenCount)
                .Select(piece => ToExternalId(piece.Id))
                .ToList();
        }

        public string? Decode(IEnumerable<int> ids)
        {
            return DecodeWithOptions(ids, skipSpecialTokens: false);
        }

        internal string? DecodeWithOptions(IEnumerable<int> ids, bool skipSpecialTokens)
        {
            return DecodeWithBoundaryOptions(ids, skipSpecialTokens, trimLeadingBoundary: true, trimTrailingBoundary: true);
        }

        internal string? DecodeWithBoundaryOptions(IEnumerable<int> ids, bool skipSpecialTokens, bool trimLeadingBoundary, bool trimTrailingBoundary)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var builder = new StringBuilder();
            foreach (var id in ids)
            {
                var modelId = ToModelId(id);
                if (!_piecesById.TryGetValue(modelId, out var piece))
                {
                    continue;
                }

                if (skipSpecialTokens && _externalSpecialTokenIds.Contains(id))
                {
                    continue;
                }

                builder.Append(piece.Value);
            }

            var decoded = builder.ToString();
            if (_escapeWhitespaces)
            {
                decoded = decoded.Replace(DummyWhitespace, ' ');
            }

            if (trimLeadingBoundary && _addDummyPrefix && !_treatWhitespaceAsSuffix && decoded.Length > 0 && decoded[0] == ' ')
            {
                decoded = decoded.Substring(1);
            }

            if (trimTrailingBoundary && _treatWhitespaceAsSuffix && decoded.Length > 0 && decoded[decoded.Length - 1] == ' ')
            {
                decoded = decoded.Substring(0, decoded.Length - 1);
            }

            return decoded;
        }

        internal NormalizedText NormalizeText(string text)
        {
            var precompiledNormalized = _precompiledCharsMap?.Apply(text) ?? text;
            var normalized = new NormalizedText(precompiledNormalized, BuildSourceMap(text, precompiledNormalized));

            if (_removeExtraWhitespaces)
            {
                normalized = CollapseWhitespaces(normalized);
            }

            if (normalized.Value.Length == 0)
            {
                return normalized;
            }

            if (_addDummyPrefix && !_treatWhitespaceAsSuffix)
            {
                normalized = normalized.Prepend(' ', -1);
            }

            if (_treatWhitespaceAsSuffix)
            {
                normalized = normalized.Append(' ', -1);
            }

            if (_escapeWhitespaces)
            {
                normalized = normalized.Replace(' ', DummyWhitespace);
            }

            return normalized;
        }

        internal List<TokenPiece> Tokenize(string normalized, int maxTokenCount)
        {
            var pieces = new List<TokenPiece>();
            if (string.IsNullOrEmpty(normalized) || maxTokenCount <= 0)
            {
                return pieces;
            }

            var length = normalized.Length;
            var bestScores = Enumerable.Repeat(float.NegativeInfinity, length + 1).ToArray();
            var previousIndex = Enumerable.Repeat(-1, length + 1).ToArray();
            var previousPiece = new PieceEntry?[length + 1];

            bestScores[0] = 0f;

            for (var index = 0; index < length; index++)
            {
                if (float.IsNegativeInfinity(bestScores[index]))
                {
                    continue;
                }

                if (_piecesByFirstChar.TryGetValue(normalized[index], out var candidates))
                {
                    foreach (var candidate in candidates)
                    {
                        if (index + candidate.Value.Length > length)
                        {
                            continue;
                        }

                        if (!normalized.AsSpan(index, candidate.Value.Length).SequenceEqual(candidate.Value.AsSpan()))
                        {
                            continue;
                        }

                        var nextIndex = index + candidate.Value.Length;
                        var score = bestScores[index] + candidate.Score;
                        if (score > bestScores[nextIndex])
                        {
                            bestScores[nextIndex] = score;
                            previousIndex[nextIndex] = index;
                            previousPiece[nextIndex] = candidate;
                        }
                    }
                }

                var unknownNextIndex = index + 1;
                var unknownScore = bestScores[index] + _unknownPenalty;
                if (unknownScore > bestScores[unknownNextIndex])
                {
                    bestScores[unknownNextIndex] = unknownScore;
                    previousIndex[unknownNextIndex] = index;
                    previousPiece[unknownNextIndex] = new PieceEntry(_unknownId, _unknownToken, _unknownPenalty, ModelProto.Types.SentencePiece.Types.Type.Unknown);
                }
            }

            var cursor = length;
            while (cursor > 0)
            {
                var piece = previousPiece[cursor] ?? new PieceEntry(_unknownId, _unknownToken, _unknownPenalty, ModelProto.Types.SentencePiece.Types.Type.Unknown);
                var start = Math.Max(0, previousIndex[cursor]);
                pieces.Add(new TokenPiece(piece.Id, piece.Value, start, cursor));
                cursor = start;
            }

            pieces.Reverse();
            if (pieces.Count > maxTokenCount)
            {
                pieces = pieces.Take(maxTokenCount).ToList();
            }

            return pieces;
        }

        internal int ToExternalId(int id)
        {
            return _applyIdOffset && id >= 4 ? id + 1 : id;
        }

        internal int ToModelId(int id)
        {
            return _applyIdOffset && id >= 4 ? id - 1 : id;
        }

        private static List<int> BuildSourceMap(string original, string normalized)
        {
            var map = new List<int>(normalized.Length);
            if (normalized.Length == 0)
            {
                return map;
            }

            if (original.Length == 0)
            {
                for (var index = 0; index < normalized.Length; index++)
                {
                    map.Add(-1);
                }

                return map;
            }

            if (original.Length == normalized.Length)
            {
                for (var index = 0; index < normalized.Length; index++)
                {
                    map.Add(index);
                }

                return map;
            }

            for (var index = 0; index < normalized.Length; index++)
            {
                map.Add(Math.Min(index, original.Length - 1));
            }

            return map;
        }

        private static NormalizedText CollapseWhitespaces(NormalizedText text)
        {
            if (string.IsNullOrEmpty(text.Value))
            {
                return new NormalizedText(string.Empty, new List<int>());
            }

            var builder = new StringBuilder(text.Value.Length);
            var sourceMap = new List<int>(text.Value.Length);
            var previousWasWhitespace = true;

            for (var index = 0; index < text.Value.Length; index++)
            {
                var character = text.Value[index];
                if (char.IsWhiteSpace(character))
                {
                    if (previousWasWhitespace)
                    {
                        continue;
                    }

                    builder.Append(' ');
                    sourceMap.Add(text.SourceMap[index]);
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(character);
                sourceMap.Add(text.SourceMap[index]);
                previousWasWhitespace = false;
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            {
                builder.Length--;
                sourceMap.RemoveAt(sourceMap.Count - 1);
            }

            return new NormalizedText(builder.ToString(), sourceMap);
        }

        private static bool IsTokenizablePiece(ModelProto.Types.SentencePiece.Types.Type type)
        {
            return type == ModelProto.Types.SentencePiece.Types.Type.Normal ||
                   type == ModelProto.Types.SentencePiece.Types.Type.UserDefined ||
                   type == ModelProto.Types.SentencePiece.Types.Type.Unused;
        }

        internal readonly struct PieceEntry
        {
            public PieceEntry(int id, string value, float score, ModelProto.Types.SentencePiece.Types.Type type)
            {
                Id = id;
                Value = value;
                Score = score;
                Type = type;
            }

            public int Id { get; }

            public string Value { get; }

            public float Score { get; }

            public ModelProto.Types.SentencePiece.Types.Type Type { get; }
        }

        internal readonly struct TokenPiece
        {
            public TokenPiece(int id, string token, int start, int end)
            {
                Id = id;
                Token = token;
                Start = start;
                End = end;
            }

            public int Id { get; }

            public string Token { get; }

            public int Start { get; }

            public int End { get; }
        }

        internal readonly struct NormalizedText
        {
            public NormalizedText(string value, List<int> sourceMap)
            {
                Value = value;
                SourceMap = sourceMap;
            }

            public string Value { get; }

            public List<int> SourceMap { get; }

            public NormalizedText Prepend(char value, int sourceIndex)
            {
                var sourceMap = new List<int>(SourceMap.Count + 1) { sourceIndex };
                sourceMap.AddRange(SourceMap);
                return new NormalizedText(value + Value, sourceMap);
            }

            public NormalizedText Append(char value, int sourceIndex)
            {
                var sourceMap = new List<int>(SourceMap.Count + 1);
                sourceMap.AddRange(SourceMap);
                sourceMap.Add(sourceIndex);
                return new NormalizedText(Value + value, sourceMap);
            }

            public NormalizedText Replace(char oldValue, char newValue)
            {
                return new NormalizedText(Value.Replace(oldValue, newValue), new List<int>(SourceMap));
            }

            public (int Start, int End) GetOriginalOffsets(string originalText, int start, int end)
            {
                if (string.IsNullOrEmpty(originalText) || start >= end || start >= SourceMap.Count)
                {
                    return (0, 0);
                }

                var mappedIndices = new List<int>(Math.Max(0, end - start));
                for (var index = start; index < end && index < SourceMap.Count; index++)
                {
                    var sourceIndex = SourceMap[index];
                    if (sourceIndex >= 0 && sourceIndex < originalText.Length)
                    {
                        mappedIndices.Add(sourceIndex);
                    }
                }

                if (mappedIndices.Count == 0)
                {
                    return (0, 0);
                }

                var trimmedStart = mappedIndices.FirstOrDefault(index => !char.IsWhiteSpace(originalText[index]));
                var trimmedEnd = mappedIndices.LastOrDefault(index => !char.IsWhiteSpace(originalText[index]));
                if (trimmedEnd >= trimmedStart && trimmedEnd > 0)
                {
                    return (trimmedStart, trimmedEnd + 1);
                }

                var nextVisibleIndex = mappedIndices[mappedIndices.Count - 1] + 1;
                if (nextVisibleIndex < originalText.Length && !char.IsWhiteSpace(originalText[nextVisibleIndex]))
                {
                    return (nextVisibleIndex, nextVisibleIndex + 1);
                }

                return (mappedIndices[0], mappedIndices[mappedIndices.Count - 1] + 1);
            }
        }
    }
}