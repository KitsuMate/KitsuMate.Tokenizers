using System;
using System.IO;
using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerLoaderTests
    {
        [Fact]
        public void FromLocal_UsesTokenizerJsonWhenPresent()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                  "model": {
                                        "continuing_subword_prefix": "##",
                                        "vocab": { "hello": 0, "[UNK]": 1 }
                  }
                }
                """);

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_FallsBackWhenTokenizerJsonIsMalformed()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), "{ not valid json }");
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_FallsBackWhenTokenizerJsonResolvesToStubAndArtifactsExist()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                  "model": {
                    "type": "UnsupportedType"
                  }
                }
                """);
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_ThrowsOriginalErrorWhenFallbackToOtherVariantsDisabledAndTokenizerJsonIsMalformed()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), "{ not valid json }");
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var exception = Assert.ThrowsAny<Exception>(() =>
                    TokenizerLoader.FromLocal(directory, new TokenizerLoadOptions { FallbackToOtherVariants = false }));

                Assert.DoesNotContain("Could not detect tokenizer type", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_ThrowsUnsupportedErrorWhenFallbackToOtherVariantsDisabled()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                  "model": {
                    "type": "UnsupportedType"
                  }
                }
                """);
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");

                var exception = Assert.Throws<TokenizerNotSupportedException>(() =>
                    TokenizerLoader.FromLocal(directory, new TokenizerLoadOptions { FallbackToOtherVariants = false }));

                Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_ThrowsWhenTokenizerJsonIsUnsupportedAndNoFallbackArtifactsExist()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                  "model": {
                    "type": "UnsupportedType"
                  }
                }
                """);

                var exception = Assert.Throws<TokenizerNotSupportedException>(() => TokenizerLoader.FromLocal(directory));

                Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
                Assert.Contains("not implemented", exception.Message);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_UsesSentencePieceModelWhenTokenizerJsonMissing()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "model.model"), SentencePieceTestData.CreateSimpleBpeModel());

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
                Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

                [Fact]
                public void FromLocal_UsesSentencePieceTokenizerJsonAndSiblingModelWhenAvailable()
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

                                var tokenizer = TokenizerLoader.FromLocal(directory);
                                var encoding = tokenizer.Encode("hello");

                                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
                                Assert.Equal(new[] { 3, 4 }, encoding.Ids);
                                Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
                        }
                        finally
                        {
                                Directory.Delete(directory, recursive: true);
                        }
                }

        [Fact]
        public void FromLocal_UsesWordPieceArtifacts()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[UNK]\nhello\nworld\n");

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_UsesTokenizerConfigOverridesForWordPieceArtifacts()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nHELLO\n");
                File.WriteAllText(Path.Combine(directory, "tokenizer_config.json"), """
                {
                  "do_lower_case": false,
                  "cls_token": "[CLS]",
                  "sep_token": "[SEP]"
                }
                """);

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 4 }, tokenizer.EncodeToIds("HELLO", addSpecialTokens: false));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_UsesBpeArtifacts()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.json"), "{}");
                File.WriteAllText(Path.Combine(directory, "merges.txt"), "#version: 0.2");

                var tokenizer = TokenizerLoader.FromLocal(directory);

                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

                [Fact]
                public void FromLocal_UsesTokenizerConfigOverridesForBpeArtifacts()
                {
                        var directory = CreateTempDirectory();

                        try
                        {
                                File.WriteAllText(Path.Combine(directory, "vocab.json"), """
                                {
                                    "h": 0,
                                    "e": 1,
                                    "l": 2,
                                    "o": 3,
                                    "Ġ": 4,
                                    "t": 5,
                                    "r": 6,
                                    "he": 7,
                                    "hel": 8,
                                    "hell": 9,
                                    "hello": 10,
                                    "Ġt": 11,
                                    "Ġthe": 12,
                                    "Ġther": 13,
                                    "Ġthere": 14
                                }
                                """);
                                File.WriteAllText(Path.Combine(directory, "merges.txt"), "#version: 0.2\nh e\nhe l\nhel l\nhell o\nĠ t\nĠt he\nĠthe r\nĠther e\n");
                                File.WriteAllText(Path.Combine(directory, "tokenizer_config.json"), """
                                {
                                    "model_type": "roberta",
                                    "add_prefix_space": false
                                }
                                """);

                                var tokenizer = TokenizerLoader.FromLocal(directory);
                                var encoding = tokenizer.Encode("hello there");

                                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
                                Assert.Equal(new[] { 10, 14 }, encoding.Ids);
                                Assert.Equal(new[] { "hello", "Ġthere" }, encoding.Tokens);
                                Assert.Equal("hello there", tokenizer.Decode(encoding.Ids));
                        }
                        finally
                        {
                                Directory.Delete(directory, recursive: true);
                        }
                }

        [Fact]
        public void FromLocal_ThrowsWhenArtifactsCannotBeDetected()
        {
            var directory = CreateTempDirectory();

            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => TokenizerLoader.FromLocal(directory));

                Assert.Contains("Could not detect tokenizer type", exception.Message);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_LoadsRealGpt2Fixture()
        {
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "IntegrationTests", "GPT2"));

            var tokenizer = TokenizerLoader.FromLocal(directory);
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5), (5, 6), (6, 12), (12, 13) }, encoding.Offsets);
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaFixture()
        {
            var directory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(directory))
            {
                return;
            }

            var tokenizer = TokenizerLoader.FromLocal(directory);
            var encoding = tokenizer.Encode("Hello, how are you?");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 31414, 6, 141, 32, 47, 116, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "Hello", ",", "Ġhow", "Ġare", "Ġyou", "?", "</s>" }, encoding.Tokens);
            Assert.Equal("Hello, how are you?", tokenizer.Decode(new[] { 0, 31414, 6, 141, 32, 47, 116, 2, 1 }, skipSpecialTokens: true));
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaMaskAsSingleAddedToken()
        {
            var directory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(directory))
            {
                return;
            }

            var tokenizer = TokenizerLoader.FromLocal(directory);
            var encoding = tokenizer.Encode("<mask>");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 50264, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "<mask>", "</s>" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromLocal_LoadsWordPiecePairThroughLoader()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.WriteAllText(Path.Combine(directory, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n");

                var tokenizer = TokenizerLoader.FromLocal(directory);
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
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void FromLocal_LoadsCachedRobertaPairThroughLoader()
        {
            var directory = GetCachedModelDirectory("roberta-base--main");
            if (!Directory.Exists(directory))
            {
                return;
            }

            var tokenizer = TokenizerLoader.FromLocal(directory);
            var encoding = tokenizer.EncodePair("Hello", "world");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 31414, 2, 2, 8331, 2 }, encoding.Ids);
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, encoding.TypeIds);
            Assert.Equal(new[] { 1, 0, 1, 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal((1, 2), encoding.SequenceRanges[0]);
            Assert.Equal((4, 5), encoding.SequenceRanges[1]);
        }

        [Fact]
        public void FromLocal_UsesTiktokenArtifacts()
        {
            var directory = CreateTempDirectory();

            try
            {
                File.Copy(GetGpt2TiktokenPath(), Path.Combine(directory, "gpt2.tiktoken"));

                var tokenizer = TokenizerLoader.FromLocal(directory);
                var encoding = tokenizer.Encode("Hello, world!");

                Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
                Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string GetCachedModelDirectory(string modelDirectoryName)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "kitsumate-tokenizers", "hub", modelDirectoryName);
        }

        private static string GetGpt2TiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "gpt2.tiktoken"));
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "KitsuMate.Tokenizers.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}