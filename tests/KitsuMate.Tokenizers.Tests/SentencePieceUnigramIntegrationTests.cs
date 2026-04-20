using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;

namespace KitsuMate.Tokenizers.Tests
{
    /// <summary>
    /// Integration tests for SentencePiece Unigram tokenizer using real T5 model
    /// </summary>
    public class SentencePieceUnigramIntegrationTests
    {
        private static readonly string TestDataPath = Path.Combine(
            Directory.GetCurrentDirectory(), 
            "TestData", 
            "t5_spiece.model");

        [Fact]
        public void FromModel_WithRealT5Model_CanLoadAndEncode()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange & Act
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);

            // Assert
            Assert.NotNull(tokenizer);
            
            // Test basic encoding
            var text = "Hello world";
            var ids = tokenizer.EncodeToIds(text);
            
            Assert.NotNull(ids);
            Assert.NotEmpty(ids);
            Assert.True(ids.Count > 0, "Should encode to at least one token");
        }

        [Fact]
        public void FromModel_WithRealT5Model_CanEncodeAndDecode()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);
            var originalText = "translate English to German: Hello, how are you?";

            // Act
            var ids = tokenizer.EncodeToIds(originalText);
            var decoded = tokenizer.Decode(ids);

            // Assert
            Assert.NotNull(decoded);
            Assert.NotEmpty(decoded);
            // Note: Decoded text may differ slightly due to tokenization (spaces, etc.)
            // but should contain the core content
        }

        [Fact]
        public void FromModel_WithRealT5Model_ProducesConsistentResults()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);
            var text = "The quick brown fox jumps over the lazy dog";

            // Act - Encode the same text multiple times
            var ids1 = tokenizer.EncodeToIds(text);
            var ids2 = tokenizer.EncodeToIds(text);
            var ids3 = tokenizer.EncodeToIds(text);

            // Assert - Results should be identical
            Assert.Equal(ids1, ids2);
            Assert.Equal(ids2, ids3);
        }

        [Fact]
        public void FromModel_WithRealT5Model_HandlesDifferentTextLengths()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);
            var testCases = new Dictionary<string, string>
            {
                { "Short", "Hi" },
                { "Medium", "Hello, how are you doing today?" },
                { "Long", "The quick brown fox jumps over the lazy dog. This sentence contains all letters of the alphabet." }
            };

            // Act & Assert
            foreach (var (name, text) in testCases)
            {
                var ids = tokenizer.EncodeToIds(text);
                Assert.NotNull(ids);
                Assert.NotEmpty(ids);
                
                var decoded = tokenizer.Decode(ids);
                Assert.NotNull(decoded);
                Assert.NotEmpty(decoded);
            }
        }

        [Fact]
        public void FromModel_WithRealT5Model_HandlesUnicodeText()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);
            var unicodeTexts = new[]
            {
                "Héllo wörld",           // Accented characters
                "你好世界",                // Chinese
                "Здравствуй мир",        // Russian
                "مرحبا بالعالم",          // Arabic
                "🌍 Hello world 🌎"      // Emojis
            };

            // Act & Assert
            foreach (var text in unicodeTexts)
            {
                var ids = tokenizer.EncodeToIds(text);
                Assert.NotNull(ids);
                Assert.NotEmpty(ids);
                
                // Should be able to decode without throwing
                var decoded = tokenizer.Decode(ids);
                Assert.NotNull(decoded);
            }
        }

        [Fact]
        public void FromModel_WithRealT5Model_CountTokensMatchesEncoding()
        {
            // Skip if test data not available
            if (!File.Exists(TestDataPath))
            {
                return; // Skip test if model file not found
            }

            // Arrange
            var tokenizer = Tokenizer.CreateSentencePiece(TestDataPath, TokenizerBackendType.SentencePieceUnigram);
            var texts = new[]
            {
                "Hello world",
                "The quick brown fox",
                "Testing tokenization consistency"
            };

            // Act & Assert
            foreach (var text in texts)
            {
                var ids = tokenizer.EncodeToIds(text);
                var count = tokenizer.CountTokens(text);
                
                // CountTokens should match the length of encoded IDs
                Assert.Equal(ids.Count, count);
            }
        }
    }
}
