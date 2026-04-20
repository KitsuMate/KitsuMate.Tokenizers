using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Native WordPiece model implementation following the Hugging Face and Rust reference behavior.
    /// </summary>
    public sealed class WordPieceModel : ITokenizerModel
    {
        private readonly Dictionary<string, int> _vocabulary;
        private readonly Dictionary<int, string> _reverseVocabulary;

        public WordPieceModel(Dictionary<string, int> vocabulary, WordPieceTokenizerOptions? options = null, string? name = null)
        {
            _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
            if (_vocabulary.Count == 0)
            {
                throw new ArgumentException("WordPiece vocabulary cannot be empty.", nameof(vocabulary));
            }

            Options = options ?? new WordPieceTokenizerOptions();
            _reverseVocabulary = _vocabulary.ToDictionary(pair => pair.Value, pair => pair.Key);

            if (!_vocabulary.ContainsKey(Options.UnknownToken))
            {
                throw new InvalidOperationException($"WordPiece vocabulary is missing the unknown token '{Options.UnknownToken}'.");
            }

            Name = string.IsNullOrWhiteSpace(name) ? "wordpiece" : name;
        }

        public string Name { get; }

        public TokenizerBackendType BackendType => TokenizerBackendType.WordPiece;

        public bool SupportsDecode => true;

        public WordPieceTokenizerOptions Options { get; }

        public static WordPieceModel FromVocab(string vocabPath, WordPieceTokenizerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(vocabPath))
            {
                throw new ArgumentException("Vocabulary path cannot be null or empty.", nameof(vocabPath));
            }

            if (!File.Exists(vocabPath))
            {
                throw new FileNotFoundException($"Vocabulary file not found: {vocabPath}", vocabPath);
            }

            var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
            var index = 0;

            foreach (var line in File.ReadLines(vocabPath))
            {
                vocabulary[line.TrimEnd()] = index;
                index++;
            }

            return new WordPieceModel(vocabulary, options, Path.GetFileNameWithoutExtension(vocabPath));
        }

        public static WordPieceModel FromBytes(byte[] vocab, WordPieceTokenizerOptions? options = null, string? name = null)
        {
            if (vocab == null)
            {
                throw new ArgumentNullException(nameof(vocab));
            }

            using var stream = new MemoryStream(vocab, writable: false);
            return FromStream(stream, options, name);
        }

        public static WordPieceModel FromStream(Stream vocabStream, WordPieceTokenizerOptions? options = null, string? name = null)
        {
            if (vocabStream == null)
            {
                throw new ArgumentNullException(nameof(vocabStream));
            }

            var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
            using var reader = new StreamReader(vocabStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            var index = 0;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                vocabulary[line.TrimEnd()] = index;
                index++;
            }

            return new WordPieceModel(vocabulary, options, string.IsNullOrWhiteSpace(name) ? "wordpiece" : name);
        }

        public static WordPieceModel FromTokenizerJson(JObject root, WordPieceTokenizerOptions? options = null)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var model = root["model"] as JObject ?? throw new InvalidOperationException("tokenizer.json is missing the model section.");
            var vocab = model["vocab"] as JObject ?? throw new InvalidOperationException("WordPiece tokenizer.json is missing model.vocab.");

            var effectiveOptions = options ?? WordPieceTokenizerConfigLoader.CreateOptions(root);

            var vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in vocab.Properties())
            {
                vocabulary[property.Name] = property.Value.Value<int>();
            }

            return new WordPieceModel(vocabulary, effectiveOptions, "wordpiece-json");
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
            return TokenizeWord(text).Select(piece => piece.Id).Take(maxTokenCount).ToList();
        }

        public string? Decode(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            return string.Join(" ", ids.Select(id => IdToToken(id) ?? Options.UnknownToken));
        }

        internal IReadOnlyList<WordPieceTokenPiece> TokenizeWord(string sequence)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            if (sequence.Length == 0)
            {
                return Array.Empty<WordPieceTokenPiece>();
            }

            if (CountUnicodeScalars(sequence) > Options.MaxInputCharsPerWord)
            {
                return new[] { CreateUnknownPiece(sequence) };
            }

            var start = 0;
            var pieces = new List<WordPieceTokenPiece>();

            while (start < sequence.Length)
            {
                var end = sequence.Length;
                WordPieceTokenPiece? matchedPiece = null;

                while (start < end)
                {
                    var candidate = sequence.Substring(start, end - start);
                    var tokenValue = start > 0 ? Options.ContinuingSubwordPrefix + candidate : candidate;
                    if (_vocabulary.TryGetValue(tokenValue, out var tokenId))
                    {
                        matchedPiece = new WordPieceTokenPiece(tokenValue, tokenId, start, end);
                        break;
                    }

                    end = MoveLeftByOneTextElement(sequence, end);
                }

                if (matchedPiece == null)
                {
                    return new[] { CreateUnknownPiece(sequence) };
                }

                pieces.Add(matchedPiece);
                start = matchedPiece.End;
            }

            return pieces;
        }

        private WordPieceTokenPiece CreateUnknownPiece(string sequence)
        {
            return new WordPieceTokenPiece(Options.UnknownToken, _vocabulary[Options.UnknownToken], 0, sequence.Length);
        }

        private static int MoveLeftByOneTextElement(string text, int endExclusive)
        {
            if (endExclusive <= 0)
            {
                return 0;
            }

            var newEnd = endExclusive - 1;
            if (newEnd > 0 && char.IsLowSurrogate(text[newEnd]) && char.IsHighSurrogate(text[newEnd - 1]))
            {
                newEnd--;
            }

            return newEnd;
        }

        private static int CountUnicodeScalars(string value)
        {
            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                }

                count++;
            }

            return count;
        }

        internal sealed class WordPieceTokenPiece
        {
            public WordPieceTokenPiece(string value, int id, int start, int end)
            {
                Value = value;
                Id = id;
                Start = start;
                End = end;
            }

            public string Value { get; }

            public int Id { get; }

            public int Start { get; }

            public int End { get; }
        }
    }
}