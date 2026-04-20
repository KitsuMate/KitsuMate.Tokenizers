using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerSpecialTokenTests
    {
        [Theory]
        [InlineData("[CLS]", "[SEP]")]
        [InlineData("<s>", "</s>")]
        public void EnsureSpecialTokensAreInsertedWhenConfigured(string expectedStartToken, string expectedEndToken)
        {
            var vocabTokens = CreateVocabFor(expectedStartToken, expectedEndToken);
            var vocabPath = Path.GetTempFileName();

            try
            {
                File.WriteAllLines(vocabPath, vocabTokens);

                var tokenizer = Tokenizer.CreateWordPiece(vocabPath, new WordPieceTokenizerOptions
                {
                    LowerCaseBeforeTokenization = false,
                    ApplyBasicTokenization = false,
                    ClassificationToken = expectedStartToken,
                    SeparatorToken = expectedEndToken,
                });

                var startTokenId = Array.IndexOf(vocabTokens.ToArray(), expectedStartToken);
                var endTokenId = Array.IndexOf(vocabTokens.ToArray(), expectedEndToken);
                Assert.True(startTokenId >= 0);
                Assert.True(endTokenId >= 0);

                var ids = tokenizer.EncodeToIds("English: Natural language processing");
                Assert.True(ids.Count >= 2, "Encoded sequence should contain at least two tokens when special tokens are added.");
                Assert.Equal(startTokenId, ids[0]);
                Assert.Equal(endTokenId, ids[^1]);
            }
            finally
            {
                if (File.Exists(vocabPath))
                {
                    File.Delete(vocabPath);
                }
            }
        }

        private static IReadOnlyList<string> CreateVocabFor(string startToken, string endToken)
        {
            var tokens = new List<string>
            {
                "[PAD]",
                "[UNK]",
                "[CLS]",
                "[SEP]",
                "[MASK]",
                startToken,
                endToken,
                "English",
                ":",
                "Natural",
                "language",
                "processing"
            };

            var uniqueTokens = new List<string>();
            foreach (var token in tokens)
            {
                if (!uniqueTokens.Contains(token, StringComparer.Ordinal))
                {
                    uniqueTokens.Add(token);
                }
            }

            return uniqueTokens;
        }

        [Fact]
        public void XlmRoberta_AddsBosEosTokens()
        {
            var tokenizer = Tokenizer.FromPretrained("xlm-roberta-base");
            var text = "English: Natural language processing";
            
            var encoding = tokenizer.Encode(text, addSpecialTokens: true);
            
            Assert.True(encoding.Ids.Count >= 2, $"Expected at least 2 tokens, got {encoding.Ids.Count}");
            Assert.Equal(0, encoding.Ids[0]);
            Assert.Equal(2, encoding.Ids[^1]);
            Assert.Equal("<s>", encoding.Tokens[0]);
            Assert.Equal("</s>", encoding.Tokens[^1]);
            
            var decoded = tokenizer.Decode(encoding.Ids, skipSpecialTokens: false);
            Assert.NotNull(decoded);
        }
    }
}
