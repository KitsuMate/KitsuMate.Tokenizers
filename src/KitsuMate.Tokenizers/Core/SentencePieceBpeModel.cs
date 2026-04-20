using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using KitsuMate.Tokenizers.Normalizers;
using Google.Protobuf;
using Sentencepiece;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Native SentencePiece BPE model loaded directly from the protobuf model file.
    /// </summary>
    public sealed class SentencePieceBpeModel : ITokenizerModel
    {
        private const char DummyWhitespace = '\u2581';

        private readonly BpeModel _model;
        private readonly HashSet<int> _specialTokenIds;
        private readonly PrecompiledCharsMap? _precompiledCharsMap;
        private readonly bool _applyIdOffset;
        private readonly bool _addDummyPrefix;
        private readonly HashSet<int> _externalSpecialTokenIds;

        internal SentencePieceBpeModel(
            string name,
            BpeModel model,
            HashSet<int> specialTokenIds,
            PrecompiledCharsMap? precompiledCharsMap,
            bool applyIdOffset,
            bool addDummyPrefix)
        {
            Name = name;
            _model = model;
            _specialTokenIds = specialTokenIds;
            _precompiledCharsMap = precompiledCharsMap;
            _applyIdOffset = applyIdOffset;
            _addDummyPrefix = addDummyPrefix;
            _externalSpecialTokenIds = new HashSet<int>(_specialTokenIds.Select(ToExternalId));
        }

        public string Name { get; }

        public TokenizerBackendType BackendType => TokenizerBackendType.SentencePieceBpe;

        public bool SupportsDecode => true;

        internal BpeModel InnerModel => _model;

        internal HashSet<int> SpecialTokenIds => _specialTokenIds;

        internal PrecompiledCharsMap? PrecompiledCharsMap => _precompiledCharsMap;

        internal bool ApplyIdOffset => _applyIdOffset;

        internal bool AddDummyPrefix => _addDummyPrefix;

        internal HashSet<int> ExternalSpecialTokenIds => _externalSpecialTokenIds;

        public static SentencePieceBpeModel FromFile(string modelPath, bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}", modelPath);
            }

            using var modelStream = File.OpenRead(modelPath);
            return FromStream(modelStream, Path.GetFileNameWithoutExtension(modelPath), applyIdOffset, addDummyPrefix);
        }

        public static SentencePieceBpeModel FromBytes(byte[] modelBytes, string name = "sentencepiece", bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            if (modelBytes == null)
            {
                throw new ArgumentNullException(nameof(modelBytes));
            }

            using var modelStream = new MemoryStream(modelBytes, writable: false);
            return FromStream(modelStream, name, applyIdOffset, addDummyPrefix);
        }

        public static SentencePieceBpeModel FromStream(Stream modelStream, string name = "sentencepiece", bool applyIdOffset = false, bool addDummyPrefix = true)
        {
            if (modelStream == null)
            {
                throw new ArgumentNullException(nameof(modelStream));
            }

            var model = ModelProto.Parser.ParseFrom(modelStream);
            if (model.TrainerSpec.ModelType != TrainerSpec.Types.ModelType.Bpe)
            {
                throw new NotSupportedException($"SentencePiece BPE tokenizer requires a BPE model. Model type was '{model.TrainerSpec.ModelType}'.");
            }

            var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
            var specialTokenIds = new HashSet<int>();
            for (var index = 0; index < model.Pieces.Count; index++)
            {
                var piece = model.Pieces[index];
                vocabulary[piece.Piece ?? string.Empty] = index;
                if (piece.Type == ModelProto.Types.SentencePiece.Types.Type.Control ||
                    piece.Type == ModelProto.Types.SentencePiece.Types.Type.Unknown)
                {
                    specialTokenIds.Add(index);
                }
            }

            var merges = ExtractMerges(vocabulary)
                .Select((merge, index) => new BpeMerge(merge.Left, merge.Right, index))
                .ToList();

            var options = new BpeTokenizerOptions
            {
                UnknownToken = string.IsNullOrWhiteSpace(model.TrainerSpec.UnkPiece) ? "<unk>" : model.TrainerSpec.UnkPiece,
            };

            var normalizerSpec = model.NormalizerSpec ?? new NormalizerSpec();
            var precompiledCharsMap = PrecompiledCharsMap.FromBlob(normalizerSpec.PrecompiledCharsmap.Span);

            return new SentencePieceBpeModel(
                name,
                new BpeModel(vocabulary, merges, options, name),
                specialTokenIds,
                precompiledCharsMap,
                applyIdOffset,
                addDummyPrefix);
        }

        public int? TokenToId(string token)
        {
            if (token == null)
            {
                return null;
            }

            var id = _model.TokenToId(token);
            return id.HasValue ? ToExternalId(id.Value) : null;
        }

        public string? IdToToken(int id)
        {
            return _model.IdToToken(ToModelId(id));
        }

        public IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
        {
            var normalized = NormalizeText(text);
            return EncodeNormalized(normalized, maxTokenCount)
                .Select(piece => ToExternalId(piece.Id))
                .ToList();
        }

        public string? Decode(IEnumerable<int> ids)
        {
            return DecodeWithOptions(ids, skipSpecialTokens: false);
        }

        internal string? DecodeWithOptions(IEnumerable<int> ids, bool skipSpecialTokens)
        {
            return DecodeWithBoundaryOptions(ids, skipSpecialTokens, trimLeadingBoundary: true);
        }

        internal string? DecodeWithBoundaryOptions(IEnumerable<int> ids, bool skipSpecialTokens, bool trimLeadingBoundary)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var effectiveIds = skipSpecialTokens ? ids.Where(id => !_externalSpecialTokenIds.Contains(id)) : ids;
            var decoded = _model.Decode(effectiveIds.Select(ToModelId)) ?? string.Empty;
            decoded = decoded.Replace(DummyWhitespace, ' ');
            if (trimLeadingBoundary && decoded.Length > 0 && decoded[0] == ' ')
            {
                decoded = decoded.Substring(1);
            }

            return decoded;
        }

        internal NormalizedText NormalizeText(string text)
        {
            var precompiledNormalized = _precompiledCharsMap?.Apply(text) ?? text;
            var normalized = new NormalizedText(precompiledNormalized, BuildSourceMap(text, precompiledNormalized));
            normalized = CollapseRepeatedSpaces(normalized);
            if (_addDummyPrefix)
            {
                normalized = normalized.Prepend(' ', -1);
            }

            normalized = normalized.Replace(' ', DummyWhitespace);
            return normalized;
        }

        internal IReadOnlyList<BpeModel.BpeTokenPiece> EncodeNormalized(NormalizedText normalized, int maxTokenCount)
        {
            var pieces = _model.EncodeSegment(new BpeModel.SegmentInput(normalized.Value, 0, 0, 0, 0));
            return pieces.Count > maxTokenCount ? pieces.Take(maxTokenCount).ToList() : pieces;
        }

        internal int ToExternalId(int id)
        {
            return _applyIdOffset && id >= 4 ? id + 1 : id;
        }

        internal int ToModelId(int id)
        {
            return _applyIdOffset && id >= 4 ? id - 1 : id;
        }

        private static IEnumerable<(string Left, string Right)> ExtractMerges(IReadOnlyDictionary<string, int> vocabulary)
        {
            foreach (var entry in vocabulary.OrderBy(pair => pair.Value))
            {
                var token = entry.Key;
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                var splitIndexes = StringInfo.ParseCombiningCharacters(token);
                for (var index = 1; index < splitIndexes.Length; index++)
                {
                    var split = splitIndexes[index];
                    var left = token.Substring(0, split);
                    var right = token.Substring(split);
                    if (vocabulary.ContainsKey(left) && vocabulary.ContainsKey(right))
                    {
                        yield return (left, right);
                    }
                }
            }
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

        private static NormalizedText CollapseRepeatedSpaces(NormalizedText text)
        {
            if (string.IsNullOrEmpty(text.Value))
            {
                return new NormalizedText(string.Empty, new List<int>());
            }

            var value = new List<char>(text.Value.Length);
            var sourceMap = new List<int>(text.Value.Length);
            var previousWasSpace = false;

            for (var index = 0; index < text.Value.Length; index++)
            {
                var character = text.Value[index];
                if (character == ' ')
                {
                    if (previousWasSpace)
                    {
                        continue;
                    }

                    previousWasSpace = true;
                    value.Add(' ');
                    sourceMap.Add(text.SourceMap[index]);
                    continue;
                }

                previousWasSpace = false;
                value.Add(character);
                sourceMap.Add(text.SourceMap[index]);
            }

            return new NormalizedText(new string(value.ToArray()), sourceMap);
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