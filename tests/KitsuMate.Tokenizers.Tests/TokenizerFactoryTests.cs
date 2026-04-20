using System;
using System.IO;
using System.Text;
using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerPublicApiTests
    {
        [Fact]
        public void FromLocal_ExposesTokenizerLoaderPublicly()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var tokenizer = Tokenizer.FromLocal(directory);

                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void CreateBpe_ExposesSharedBpeScaffoldPublicly()
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
        public void CreateWordPiece_ExposesSharedWordPieceScaffoldPublicly()
        {
            var vocabPath = Path.GetTempFileName();

            try
            {
                File.WriteAllText(vocabPath, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");

                var tokenizer = Tokenizer.CreateWordPiece(vocabPath);

                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 2, 4, 5, 3 }, tokenizer.EncodeToIds("hello world"));
            }
            finally
            {
                if (File.Exists(vocabPath))
                {
                    File.Delete(vocabPath);
                }
            }
        }

        [Fact]
        public void CreateWordPiece_ExposesSharedWordPieceScaffoldPubliclyFromBytesAndStream()
        {
            var vocab = Encoding.UTF8.GetBytes("[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");
            var fromBytes = Tokenizer.CreateWordPiece(vocab);
            using var vocabStream = new MemoryStream(vocab, writable: false);
            var fromStream = Tokenizer.CreateWordPiece(vocabStream);

            Assert.Equal(new[] { 2, 4, 5, 3 }, fromBytes.EncodeToIds("hello world"));
            Assert.Equal(new[] { 2, 4, 5, 3 }, fromStream.EncodeToIds("hello world"));
        }

        [Fact]
        public void CreateTiktoken_ExposesSharedTiktokenScaffoldPublicly()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetGpt2TiktokenPath());
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.IsType<Tokenizer>(tokenizer);
            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
        }

        [Fact]
        public void CreateBpe_ExposesSharedBpeScaffoldPubliclyFromBytesAndStreams()
        {
            var vocab = Encoding.UTF8.GetBytes("{\"h\":0,\"e\":1,\"l\":2,\"o\":3,\"he\":4,\"hel\":5,\"hell\":6,\"hello\":7}");
            var merges = Encoding.UTF8.GetBytes("#version: 0.2\nh e\nhe l\nhel l\nhell o\n");
            var fromBytes = Tokenizer.CreateBpe(vocab, merges);
            using var vocabStream = new MemoryStream(vocab, writable: false);
            using var mergesStream = new MemoryStream(merges, writable: false);
            var fromStreams = Tokenizer.CreateBpe(vocabStream, mergesStream);

            Assert.Equal(new[] { 7 }, fromBytes.Encode("hello", addSpecialTokens: false).Ids);
            Assert.Equal(new[] { 7 }, fromStreams.Encode("hello", addSpecialTokens: false).Ids);
        }

        [Fact]
        public void CreateTiktoken_ExposesSharedTiktokenScaffoldPubliclyFromBytesAndStream()
        {
            var vocab = File.ReadAllBytes(GetGpt2TiktokenPath());
            var fromBytes = Tokenizer.CreateTiktoken(vocab, "gpt2");
            using var vocabStream = new MemoryStream(vocab, writable: false);
            var fromStream = Tokenizer.CreateTiktoken(vocabStream, "gpt2");

            Assert.Equal(new[] { 15496, 11, 995, 0 }, fromBytes.Encode("Hello, world!").Ids);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, fromStream.Encode("Hello, world!").Ids);
        }

        [Fact]
        public void CreateSentencePiece_ExposesSharedSentencePieceScaffoldPublicly()
        {
            var modelPath = Path.GetTempFileName();

            try
            {
                File.WriteAllBytes(modelPath, SentencePieceTestData.CreateSimpleUnigramModel());

                var tokenizer = Tokenizer.CreateSentencePiece(modelPath, TokenizerBackendType.SentencePieceUnigram);
                var encoding = tokenizer.Encode("hello");

                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
                Assert.Equal(new[] { 3, 4 }, encoding.Ids);
                Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
            }
            finally
            {
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }
            }
        }

        [Fact]
        public void CreateSentencePiece_ExposesSharedSentencePieceScaffoldPubliclyFromBytesAndStream()
        {
            var model = SentencePieceTestData.CreateSimpleUnigramModel();
            var fromBytes = Tokenizer.CreateSentencePiece(model, TokenizerBackendType.SentencePieceUnigram);
            using var modelStream = new MemoryStream(model, writable: false);
            var fromStream = Tokenizer.CreateSentencePiece(modelStream, TokenizerBackendType.SentencePieceUnigram);

            Assert.Equal(new[] { 3, 4 }, fromBytes.Encode("hello").Ids);
            Assert.Equal(new[] { 3, 4 }, fromStream.Encode("hello").Ids);
        }

        [Fact]
        public void FromTokenizerJson_LoadsLocalGpt2FixtureThroughFactory()
        {
            var tokenizerJsonPath = Path.Combine(GetGpt2FixtureDirectory(), "tokenizer.json");

            var tokenizer = Tokenizer.FromTokenizerJson(tokenizerJsonPath);
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5), (5, 6), (6, 12), (12, 13) }, encoding.Offsets);
            Assert.Equal("Hello, world!", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void FromTokenizerJson_LoadsSelfContainedWordPieceThroughBytesAndStream()
        {
            var tokenizerJson = Encoding.UTF8.GetBytes("""
            {
                "normalizer": { "type": "Lowercase" },
                "model": {
                    "type": "WordPiece",
                    "unk_token": "[UNK]",
                    "continuing_subword_prefix": "##",
                    "vocab": {
                        "[UNK]": 0,
                        "hello": 1
                    }
                }
            }
            """);

            var fromBytes = Tokenizer.FromTokenizerJson(tokenizerJson);
            using var tokenizerJsonStream = new MemoryStream(tokenizerJson, writable: false);
            var fromStream = Tokenizer.FromTokenizerJson(tokenizerJsonStream);

            Assert.Equal(new[] { 1 }, fromBytes.EncodeToIds("HELLO", addSpecialTokens: false));
            Assert.Equal(new[] { 1 }, fromStream.EncodeToIds("HELLO", addSpecialTokens: false));
        }

        [Fact]
        public void FromLocal_LoadsLocalGpt2FixtureThroughTokenizerLoader()
        {
            var modelDirectory = GetGpt2FixtureDirectory();

            var tokenizer = Tokenizer.FromLocal(modelDirectory);
            var encoding = tokenizer.Encode("This is a test of the GPT-2 tokenizer.");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 1212, 318, 257, 1332, 286, 262, 402, 11571, 12, 17, 11241, 7509, 13 }, encoding.Ids);
            Assert.Equal(new[] { "This", "Ġis", "Ġa", "Ġtest", "Ġof", "Ġthe", "ĠG", "PT", "-", "2", "Ġtoken", "izer", "." }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 4), (4, 7), (7, 9), (9, 14), (14, 17), (17, 21), (21, 23), (23, 25), (25, 26), (26, 27), (27, 33), (33, 37), (37, 38) }, encoding.Offsets);
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaFixtureThroughTokenizerLoader()
        {
            var modelDirectory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            var tokenizer = Tokenizer.FromLocal(modelDirectory);
            var encoding = tokenizer.Encode("Hello, how are you?");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 31414, 6, 141, 32, 47, 116, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "Hello", ",", "Ġhow", "Ġare", "Ġyou", "?", "</s>" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 0), (0, 5), (5, 6), (7, 10), (11, 14), (15, 18), (18, 19), (0, 0) }, encoding.Offsets);
            Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<s>Hello, how are you?</s>", tokenizer.Decode(encoding.Ids));
            Assert.Equal("Hello, how are you?", tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaMaskAsSingleAddedToken()
        {
            var modelDirectory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            var tokenizer = Tokenizer.FromLocal(modelDirectory);
            var encoding = tokenizer.Encode("<mask>");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 50264, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "<mask>", "</s>" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal(new (int Start, int End)[] { (0, 0), (0, 6), (0, 0) }, encoding.Offsets);
            Assert.Equal("<s><mask></s>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromLocal_LoadsCachedEmbeddingGemmaFixtureThroughTokenizerLoader()
        {
            var modelDirectory = GetCachedModelDirectory("google--embeddinggemma-300m--main");
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            var tokenizer = Tokenizer.FromLocal(modelDirectory);

            var first = tokenizer.Encode("Embedding Gemma should eventually run here.");
            Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
            Assert.Equal(new[] { 2, 205511, 147224, 1374, 10734, 1845, 1590, 236761, 1 }, first.Ids);

            var second = tokenizer.Encode("Offset tracking should be measured too.");
            Assert.Equal(new[] { 2, 13585, 16074, 1374, 577, 8434, 2311, 236761, 1 }, second.Ids);
        }

        [Fact]
        public void FromLocal_LoadsCachedT5FixtureThroughTokenizerLoader()
        {
            var modelDirectory = GetCachedModelDirectory("t5-small--main");
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            var tokenizer = Tokenizer.FromLocal(modelDirectory);
            var encoding = tokenizer.Encode("translate English to German: Hello world");

            Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
            Assert.Equal(new[] { 13959, 1566, 12, 2968, 10, 8774, 296, 1 }, encoding.Ids);
            Assert.Equal(new (int Start, int End)[]
            {
                (0, 9),
                (10, 17),
                (18, 20),
                (21, 27),
                (27, 28),
                (29, 34),
                (35, 40),
                (0, 0),
            }, encoding.Offsets);
        }

        [Fact]
        public void FromLocal_LoadsWordPiecePairThroughFactory()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n");

                var tokenizer = Tokenizer.FromLocal(directory);
                var encoding = tokenizer.EncodePair("hello", "world");

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 2, 4, 3, 5, 3 }, encoding.Ids);
                Assert.Equal(new[] { 0, 0, 0, 1, 1 }, encoding.TypeIds);
                Assert.Equal(new[] { 1, 0, 1, 0, 1 }, encoding.SpecialTokensMask);
                Assert.Equal((1, 2), encoding.SequenceRanges[0]);
                Assert.Equal((3, 4), encoding.SequenceRanges[1]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaPairThroughFactory()
        {
            var modelDirectory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(modelDirectory))
            {
                return;
            }

            var tokenizer = Tokenizer.FromLocal(modelDirectory);
            var encoding = tokenizer.EncodePair("Hello", "world");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 31414, 2, 2, 8331, 2 }, encoding.Ids);
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, encoding.TypeIds);
            Assert.Equal(new[] { 1, 0, 1, 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal((1, 2), encoding.SequenceRanges[0]);
            Assert.Equal((4, 5), encoding.SequenceRanges[1]);
        }

        [Fact]
        public void FromLocal_LoadsGpt2TiktokenArtifactsThroughFactory()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.Copy(GetGpt2TiktokenPath(), Path.Combine(directory, "gpt2.tiktoken"));

                var tokenizer = Tokenizer.FromLocal(directory);
                var encoding = tokenizer.Encode("Hello, world!");

                Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
                Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
                Assert.Equal("Hello, world!", tokenizer.Decode(encoding.Ids));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void FromLocal_LoadsSentencePieceUnigramWhenTokenizerJsonAndModelExist()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "model.model"), SentencePieceTestData.CreateSimpleUnigramModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                                {
                                    "model": {
                                        "type": "Unigram",
                                        "scores": [ -1.0 ]
                                    }
                                }
                                """);

                var tokenizer = Tokenizer.FromLocal(directory);
                var encoding = tokenizer.Encode("hello");

                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
                Assert.Equal(new[] { 3, 4 }, encoding.Ids);
                Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "KitsuMate.Tokenizers.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetGpt2FixtureDirectory()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "IntegrationTests", "GPT2"));
        }

        private static string GetCachedModelDirectory(string modelDirectoryName)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "kitsumate-tokenizers", "hub", modelDirectoryName);
        }

        private static string GetGpt2TiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "gpt2.tiktoken"));
        }
    }
}