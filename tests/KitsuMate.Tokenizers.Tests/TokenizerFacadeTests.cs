using System;
using System.IO;
using System.Collections.Generic;
using KitsuMate.Tokenizers.Core;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerFacadeTests
    {
        [Fact]
        public void FromLocal_ReturnsTokenizerFacadeForWordPieceArtifacts()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var tokenizer = Tokenizer.FromLocal(directory);

                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.True(tokenizer.SupportsDecode);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void CreateBpe_ReturnsTokenizerFacadeAndDelegatesEncoding()
        {
            var directory = CreateTempDirectory();

            try
            {
                var vocabPath = Path.Combine(directory, "vocab.json");
                var mergesPath = Path.Combine(directory, "merges.txt");
                File.WriteAllText(vocabPath, "{\"hello\":0,\"world\":1}");
                File.WriteAllText(mergesPath, "#version: 0.2\nh e\n");

                var tokenizer = Tokenizer.CreateBpe(vocabPath, mergesPath);

                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void FromTokenizerJson_LoadsLocalGpt2FixtureThroughTokenizerFacade()
        {
            var tokenizerJsonPath = Path.Combine(GetGpt2FixtureDirectory(), "tokenizer.json");

            var tokenizer = Tokenizer.FromTokenizerJson(tokenizerJsonPath);
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.IsType<Tokenizer>(tokenizer);
            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.IsType<BpeModel>(tokenizer.Model);
            Assert.NotNull(tokenizer.PreTokenizer);
            Assert.NotNull(tokenizer.PostProcessor);
            Assert.NotNull(tokenizer.Decoder);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromWordPieceModel_UsesNativeWordPieceTokenizerBehavior()
        {
            var model = new WordPieceModel(
                new System.Collections.Generic.Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                });

            var tokenizer = Tokenizer.Create(model);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            Assert.Same(model, tokenizer.Model);
            Assert.NotNull(tokenizer.Normalizer);
            Assert.NotNull(tokenizer.PreTokenizer);
            Assert.NotNull(tokenizer.Decoder);
            Assert.Null(tokenizer.PostProcessor);
            Assert.Equal(new[] { 1, 3, 2 }, encoding.Ids);
            Assert.Equal(new[] { "[CLS]", "hello", "[SEP]" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromBpeModel_UsesTokenizerRuntimePath()
        {
            var model = new BpeModel(
                new System.Collections.Generic.Dictionary<string, int>
                {
                    ["h"] = 0,
                    ["e"] = 1,
                    ["l"] = 2,
                    ["o"] = 3,
                    ["he"] = 4,
                    ["hel"] = 5,
                    ["hell"] = 6,
                    ["hello"] = 7,
                },
                new[]
                {
                    new BpeMerge("h", "e", 0),
                    new BpeMerge("he", "l", 1),
                    new BpeMerge("hel", "l", 2),
                    new BpeMerge("hell", "o", 3),
                });

            var tokenizer = Tokenizer.Create(model);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Same(model, tokenizer.Model);
            Assert.Null(tokenizer.Normalizer);
            Assert.Null(tokenizer.PreTokenizer);
            Assert.Null(tokenizer.PostProcessor);
            Assert.Null(tokenizer.Decoder);
            Assert.Equal(new[] { 7 }, encoding.Ids);
            Assert.Equal(new[] { "hello" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromTiktokenModel_UsesNativeTiktokenBehavior()
        {
            var model = TiktokenModel.FromFile(GetGpt2TiktokenPath());
            var tokenizer = Tokenizer.Create(model);
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Same(model, tokenizer.Model);
            Assert.Null(tokenizer.Normalizer);
            Assert.Null(tokenizer.PreTokenizer);
            Assert.Null(tokenizer.PostProcessor);
            Assert.Null(tokenizer.Decoder);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromSentencePieceUnigramModel_UsesNativeSentencePieceBehavior()
        {
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleUnigramModel());
            var model = SentencePieceUnigramModel.FromStream(stream, name: "synthetic-sp-unigram");
            var tokenizer = Tokenizer.Create(model);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
            Assert.Same(model, tokenizer.Model);
            Assert.Null(tokenizer.Normalizer);
            Assert.Null(tokenizer.PreTokenizer);
            Assert.Null(tokenizer.PostProcessor);
            Assert.Null(tokenizer.Decoder);
            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromSentencePieceBpeModel_UsesNativeSentencePieceBehavior()
        {
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleBpeModel());
            var model = SentencePieceBpeModel.FromStream(stream, name: "synthetic-sp-bpe");
            var tokenizer = Tokenizer.Create(model);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
            Assert.Same(model, tokenizer.Model);
            Assert.Null(tokenizer.Normalizer);
            Assert.Null(tokenizer.PreTokenizer);
            Assert.Null(tokenizer.PostProcessor);
            Assert.Null(tokenizer.Decoder);
            Assert.Equal(new[] { 13 }, encoding.Ids);
            Assert.Equal(new[] { "▁hello" }, encoding.Tokens);
        }

        [Fact]
        public void Create_FromCustomModel_ThrowsNotSupported()
        {
            var model = new TestTokenizerModel();
            var exception = Assert.Throws<TokenizerNotSupportedException>(() => Tokenizer.Create(model));

            Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
            Assert.Contains("test-model", exception.Message);
            Assert.Contains("not implemented", exception.Message);
        }

        [Fact]
        public void Normalizer_Setter_UpdatesLiveEncodingBehavior()
        {
            var tokenizer = Tokenizer.Create(new WordPieceModel(
                new Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                    LowerCaseBeforeTokenization = true,
                }));

            Assert.Equal(new[] { 1, 3, 2 }, tokenizer.Encode("Hello").Ids);

            tokenizer.Normalizer = null;

            Assert.Equal(new[] { 1, 0, 2 }, tokenizer.Encode("Hello").Ids);
        }

        [Fact]
        public void PreTokenizer_Setter_UpdatesLiveEncodingBehavior()
        {
            var tokenizer = Tokenizer.Create(new WordPieceModel(
                new Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                    ["world"] = 4,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                    ApplyBasicTokenization = false,
                    LowerCaseBeforeTokenization = false,
                }));

            Assert.Equal(new[] { 1, 0, 2 }, tokenizer.Encode("hello world").Ids);

            tokenizer.PreTokenizer = new FixedSegmentsPreTokenizer((0, 5), (6, 5));

            Assert.Equal(new[] { 1, 3, 4, 2 }, tokenizer.Encode("hello world").Ids);
        }

        [Fact]
        public void PostProcessor_Setter_UpdatesLiveEncodingBehavior()
        {
            var tokenizer = Tokenizer.Create(new WordPieceModel(
                new Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                }));

            Assert.Equal(new[] { 3 }, tokenizer.Encode("hello", addSpecialTokens: false).Ids);

            tokenizer.PostProcessor = new AppendingPostProcessor();

            Assert.Equal(new[] { 3, 99 }, tokenizer.Encode("hello", addSpecialTokens: false).Ids);
        }

        [Fact]
        public void Decoder_Setter_UpdatesLiveDecodeBehavior()
        {
            var tokenizer = Tokenizer.Create(new WordPieceModel(
                new Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                }));

            tokenizer.Decoder = new ConstantDecoder("mutated");

            Assert.Equal("mutated", tokenizer.Decode(new[] { 3 }));
        }

        [Fact]
        public void Encode_WithOptions_UsesConfiguredPaddingMetadataFromTokenizer()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "padding": {
                        "direction": "Left",
                        "pad_id": 99,
                        "pad_type_id": 3,
                        "pad_token": "<pad>"
                    },
                    "model": {
                        "type": "WordPiece",
                        "unk_token": "[UNK]",
                        "continuing_subword_prefix": "##",
                        "vocab": {
                            "[UNK]": 0,
                            "hello": 1,
                            "<pad>": 99
                        }
                    }
                }
                """);

                var tokenizer = Tokenizer.FromTokenizerJson(path);
                var result = tokenizer.Encode("hello", new TokenizerEncodeOptions
                {
                    AddSpecialTokens = false,
                    MaxLength = 4,
                    Padding = TokenizerPaddingMode.MaxLength,
                });

                Assert.Equal(new[] { 99, 99, 99, 1 }, result.Ids);
                Assert.Equal(new[] { "<pad>", "<pad>", "<pad>", "hello" }, result.Tokens);
                Assert.Equal(new[] { 3, 3, 3, 0 }, result.TypeIds);
                Assert.Equal(new[] { 0, 0, 0, 1 }, result.AttentionMask);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void EncodeBatch_WithOptions_UsesConfiguredPaddingMetadataFromTokenizer()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "padding": {
                        "direction": "Left",
                        "pad_id": 99,
                        "pad_type_id": 3,
                        "pad_token": "<pad>"
                    },
                    "model": {
                        "type": "WordPiece",
                        "unk_token": "[UNK]",
                        "continuing_subword_prefix": "##",
                        "vocab": {
                            "[UNK]": 0,
                            "hello": 1,
                            "world": 2,
                            "<pad>": 99
                        }
                    }
                }
                """);

                var tokenizer = Tokenizer.FromTokenizerJson(path);
                var results = tokenizer.EncodeBatch(new[] { "hello", "hello world" }, new TokenizerEncodeOptions
                {
                    AddSpecialTokens = false,
                    Padding = TokenizerPaddingMode.Longest,
                });

                Assert.Equal(2, results.Count);
                Assert.Equal(new[] { 99, 1 }, results[0].Ids);
                Assert.Equal(new[] { "<pad>", "hello" }, results[0].Tokens);
                Assert.Equal(new[] { 3, 0 }, results[0].TypeIds);
                Assert.Equal(new[] { 0, 1 }, results[0].AttentionMask);
                Assert.Equal(new[] { 1, 2 }, results[1].Ids);
                Assert.Equal(new[] { "hello", "world" }, results[1].Tokens);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void CountTokens_Method_MatchesEncodedLength()
        {
            var tokenizer = Tokenizer.Create(new WordPieceModel(
                new Dictionary<string, int>
                {
                    ["[UNK]"] = 0,
                    ["[CLS]"] = 1,
                    ["[SEP]"] = 2,
                    ["hello"] = 3,
                    ["world"] = 4,
                },
                new WordPieceTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    ClassificationToken = "[CLS]",
                    SeparatorToken = "[SEP]",
                }));

            var encoding = tokenizer.Encode("hello world");

            Assert.Equal(encoding.Ids.Count, tokenizer.CountTokens("hello world"));
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetGpt2FixtureDirectory()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "IntegrationTests", "GPT2"));
        }

        private static string GetGpt2TiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "gpt2.tiktoken"));
        }

        private sealed class TestTokenizerModel : ITokenizerModel
        {
            public string Name => "test-model";

            public TokenizerBackendType BackendType => TokenizerBackendType.Unknown;

            public bool SupportsDecode => true;

            public int? TokenToId(string token)
            {
                return token == "hello" ? 42 : null;
            }

            public string? IdToToken(int id)
            {
                return id == 42 ? "hello" : null;
            }

            public System.Collections.Generic.IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
            {
                return new[] { 42 };
            }

            public string? Decode(System.Collections.Generic.IEnumerable<int> ids)
            {
                return "decoded";
            }
        }

        private sealed class FixedSegmentsPreTokenizer : IPreTokenizer
        {
            private readonly (int Offset, int Length)[] _segments;

            public FixedSegmentsPreTokenizer(params (int Offset, int Length)[] segments)
            {
                _segments = segments;
            }

            public IEnumerable<(int Offset, int Length)> PreTokenize(string text)
            {
                return _segments;
            }
        }

        private sealed class AppendingPostProcessor : IPostProcessor
        {
            public int AddedTokens(bool isPair)
            {
                return 1;
            }

            public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
            {
                var merged = EncodingResult.Merge(encodings, false);
                merged.Ids.Add(99);
                merged.Tokens.Add("<tail>");
                merged.TypeIds.Add(0);
                merged.AttentionMask.Add(1);
                merged.SpecialTokensMask.Add(1);
                merged.Words.Add(null);
                merged.Offsets.Add((0, 0));
                return merged;
            }
        }

        private sealed class ConstantDecoder : IDecoder
        {
            private readonly string _value;

            public ConstantDecoder(string value)
            {
                _value = value;
            }

            public string Decode(IEnumerable<string> tokens)
            {
                return _value;
            }
        }
    }
}