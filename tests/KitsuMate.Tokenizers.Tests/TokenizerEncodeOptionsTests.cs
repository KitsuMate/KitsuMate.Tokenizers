using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerEncodeOptionsTests
    {
        [Fact]
        public void Encode_WithDefaultOptions_ReturnsStructuredResult()
        {
            var vocabPath = Path.Combine("..", "..", "..", "..", "IntegrationTests", "BertWordPiece", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                return;
            }

            var tokenizer = Tokenizer.CreateWordPiece(vocabPath);

            var result = tokenizer.Encode("Hello world!", new TokenizerEncodeOptions());

            Assert.NotNull(result);
            Assert.NotEmpty(result.InputIds);
            Assert.NotEmpty(result.Ids);
            Assert.Equal(result.Ids, result.InputIds);
            Assert.Equal(result.TypeIds, result.TokenTypeIds);
            Assert.NotEmpty(result.AttentionMask);
            Assert.Equal(result.Ids.Count, result.AttentionMask.Count);
        }

        [Fact]
        public void Encode_WithTruncationOptions_TruncatesCorrectly()
        {
            var vocabPath = Path.Combine("..", "..", "..", "..", "IntegrationTests", "BertWordPiece", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                return;
            }

            var tokenizer = Tokenizer.CreateWordPiece(vocabPath);

            var result = tokenizer.Encode(
                "This is a very long sentence that should be truncated to a specific length",
                new TokenizerEncodeOptions
                {
                    MaxLength = 10,
                    Truncation = TokenizerTruncationMode.LongestFirst,
                });

            Assert.Equal(10, result.Ids.Count);
            Assert.Equal(10, result.AttentionMask.Count);
        }

        [Fact]
        public void Encode_WithPaddingOptions_PadsCorrectly()
        {
            var vocabPath = Path.Combine("..", "..", "..", "..", "IntegrationTests", "BertWordPiece", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                return;
            }

            var tokenizer = Tokenizer.CreateWordPiece(vocabPath);

            var result = tokenizer.Encode(
                "Hello",
                new TokenizerEncodeOptions
                {
                    MaxLength = 20,
                    Padding = TokenizerPaddingMode.MaxLength,
                });

            Assert.Equal(20, result.Ids.Count);
            Assert.Equal(20, result.AttentionMask.Count);
            var paddingCount = result.AttentionMask.Count(mask => mask == 0);
            Assert.True(paddingCount > 0, "Should have padding tokens with attention mask 0");
        }

        [Fact]
        public void EncodeBatch_ReturnsMultipleResults()
        {
            var vocabPath = Path.Combine("..", "..", "..", "..", "IntegrationTests", "BertWordPiece", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                return;
            }

            var tokenizer = Tokenizer.CreateWordPiece(vocabPath);
            var texts = new[] { "Hello world", "How are you?", "This is a test" };

            var results = tokenizer.EncodeBatch(texts);

            Assert.Equal(3, results.Count);
            Assert.All(results, result =>
            {
                Assert.NotEmpty(result.InputIds);
                Assert.NotEmpty(result.AttentionMask);
            });
        }

        [Fact]
        public void EncodeBatch_WithLongestPadding_PadsToLongest()
        {
            var vocabPath = Path.Combine("..", "..", "..", "..", "IntegrationTests", "BertWordPiece", "vocab.txt");
            if (!File.Exists(vocabPath))
            {
                return;
            }

            var tokenizer = Tokenizer.CreateWordPiece(vocabPath);
            var texts = new[] { "Hello", "Hello world test", "Hi" };

            var results = tokenizer.EncodeBatch(texts, new TokenizerEncodeOptions
            {
                Padding = TokenizerPaddingMode.Longest,
            });

            var maxLength = results.Max(result => result.Ids.Count);
            Assert.All(results, result => Assert.Equal(maxLength, result.Ids.Count));
            Assert.All(results, result => Assert.Equal(maxLength, result.AttentionMask.Count));
        }

        [Fact]
        public void EncodingResult_Pad_PadsCorrectly()
        {
            var encoding = new EncodingResult
            {
                Ids = new List<int> { 1, 2, 3 },
                TypeIds = new List<int> { 0, 0, 0 },
                Tokens = new List<string> { "a", "b", "c" },
                AttentionMask = new List<int> { 1, 1, 1 },
                SpecialTokensMask = new List<int> { 0, 0, 0 }
            };

            encoding.Pad(10, "right", 0, 0, "[PAD]");

            Assert.Equal(10, encoding.Ids.Count);
            Assert.Equal(10, encoding.AttentionMask.Count);
            Assert.Equal(7, encoding.AttentionMask.Count(mask => mask == 0));
            Assert.Equal(3, encoding.AttentionMask.Count(mask => mask == 1));
        }

        [Fact]
        public void EncodingResult_Truncate_TruncatesCorrectly()
        {
            var encoding = new EncodingResult
            {
                Ids = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                TypeIds = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                Tokens = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" },
                AttentionMask = new List<int> { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
                SpecialTokensMask = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            };

            encoding.Truncate(5);

            Assert.Equal(5, encoding.Ids.Count);
            Assert.Equal(5, encoding.AttentionMask.Count);
            Assert.Equal(5, encoding.Tokens.Count);
        }

        [Fact]
        public void Encode_WithPaddingOptions_UsesDefaultPaddingMetadataWhenTokenizerHasNoActivePaddingConfiguration()
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

                Assert.Equal(new[] { 1, 0, 0, 0 }, result.Ids);
                Assert.Equal(new[] { "hello", "[PAD]", "[PAD]", "[PAD]" }, result.Tokens);
                Assert.Equal(new[] { 0, 0, 0, 0 }, result.TypeIds);
                Assert.Equal(new[] { 1, 0, 0, 0 }, result.AttentionMask);
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
        public void EncodeBatch_WithPaddingOptions_UsesDefaultPaddingMetadataWhenTokenizerHasNoActivePaddingConfiguration()
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
                Assert.Equal(new[] { 1, 0 }, results[0].Ids);
                Assert.Equal(new[] { "hello", "[PAD]" }, results[0].Tokens);
                Assert.Equal(new[] { 0, 0 }, results[0].TypeIds);
                Assert.Equal(new[] { 1, 0 }, results[0].AttentionMask);
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
        public void Encode_WithExplicitPaddingSide_OverridesTokenizerDefault()
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
                    PaddingSide = TokenizerSide.Right,
                });

                Assert.Equal(new[] { 1, 0, 0, 0 }, result.Ids);
                Assert.Equal(new[] { "hello", "[PAD]", "[PAD]", "[PAD]" }, result.Tokens);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}