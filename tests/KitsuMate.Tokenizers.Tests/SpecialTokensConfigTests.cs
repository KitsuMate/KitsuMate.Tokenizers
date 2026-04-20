using System;
using System.Collections.Generic;
using Xunit;
using KitsuMate.Tokenizers;

namespace KitsuMate.Tokenizers.Tests
{
    public class SpecialTokensConfigTests
    {
        [Fact]
        public void SpecialTokensConfig_AddSpecialTokens_FromDictionary()
        {
            // Arrange
            var config = new SpecialTokensConfig();
            var tokens = new Dictionary<string, object>
            {
                { "bos_token", "<s>" },
                { "eos_token", "</s>" },
                { "pad_token", "<pad>" },
                { "unk_token", "<unk>" }
            };

            // Act
            config.AddSpecialTokens(tokens);

            // Assert
            Assert.NotNull(config.BosToken);
            Assert.Equal("<s>", config.BosToken.Content);
            Assert.NotNull(config.EosToken);
            Assert.Equal("</s>", config.EosToken.Content);
            Assert.NotNull(config.PadToken);
            Assert.Equal("<pad>", config.PadToken.Content);
            Assert.NotNull(config.UnkToken);
            Assert.Equal("<unk>", config.UnkToken.Content);
        }

        [Fact]
        public void SpecialTokensConfig_AddTokens_AddsNonSpecialTokens()
        {
            // Arrange
            var config = new SpecialTokensConfig();
            var tokens = new List<string> { "token1", "token2", "token3" };

            // Act
            config.AddTokens(tokens);

            // Assert
            Assert.Equal(3, config.AdditionalSpecialTokens.Count);
            Assert.All(config.AdditionalSpecialTokens, t => Assert.False(t.Special));
            Assert.Contains(config.AdditionalSpecialTokens, t => t.Content == "token1");
            Assert.Contains(config.AdditionalSpecialTokens, t => t.Content == "token2");
            Assert.Contains(config.AdditionalSpecialTokens, t => t.Content == "token3");
        }

        [Fact]
        public void SpecialTokensConfig_GetAllSpecialTokens_ReturnsAllTokens()
        {
            // Arrange
            var config = new SpecialTokensConfig
            {
                BosToken = new AddedToken { Id = 1, Content = "<s>" },
                EosToken = new AddedToken { Id = 2, Content = "</s>" },
                PadToken = new AddedToken { Id = 0, Content = "<pad>" }
            };
            config.AdditionalSpecialTokens.Add(new AddedToken { Id = 3, Content = "<extra1>", Special = true });

            // Act
            var allTokens = config.GetAllSpecialTokens();

            // Assert
            Assert.Equal(4, allTokens.Count);
            Assert.Equal("<s>", allTokens[1]);
            Assert.Equal("</s>", allTokens[2]);
            Assert.Equal("<pad>", allTokens[0]);
            Assert.Equal("<extra1>", allTokens[3]);
        }

        [Fact]
        public void SpecialTokensConfig_GetAllSpecialTokenContents_ReturnsUniqueContents()
        {
            // Arrange
            var config = new SpecialTokensConfig
            {
                BosToken = new AddedToken { Id = 1, Content = "<s>" },
                EosToken = new AddedToken { Id = 2, Content = "</s>" },
                PadToken = new AddedToken { Id = 0, Content = "<pad>" }
            };
            config.AdditionalSpecialTokens.Add(new AddedToken { Id = 3, Content = "<extra>", Special = true });
            config.AdditionalSpecialTokens.Add(new AddedToken { Id = 4, Content = "<extra>", Special = true }); // Duplicate

            // Act
            var contents = config.GetAllSpecialTokenContents();

            // Assert
            Assert.Equal(4, contents.Count); // Should be unique
            Assert.Contains("<s>", contents);
            Assert.Contains("</s>", contents);
            Assert.Contains("<pad>", contents);
            Assert.Contains("<extra>", contents);
        }

        [Fact]
        public void SpecialTokensConfig_FromAddedTokens_MapsCorrectly()
        {
            // Arrange
            var addedTokens = new List<AddedToken>
            {
                new AddedToken { Id = 0, Content = "[PAD]", Special = true },
                new AddedToken { Id = 1, Content = "[UNK]", Special = true },
                new AddedToken { Id = 2, Content = "[CLS]", Special = true },
                new AddedToken { Id = 3, Content = "[SEP]", Special = true },
                new AddedToken { Id = 4, Content = "[MASK]", Special = true }
            };

            var tokenizerConfig = new TokenizerConfig
            {
                PadToken = "[PAD]",
                UnkToken = "[UNK]",
                ClsToken = "[CLS]",
                SepToken = "[SEP]",
                MaskToken = "[MASK]"
            };

            // Act
            var specialTokens = SpecialTokensConfig.FromAddedTokens(addedTokens, tokenizerConfig);

            // Assert
            Assert.NotNull(specialTokens.PadToken);
            Assert.Equal("[PAD]", specialTokens.PadToken.Content);
            Assert.Equal(0, specialTokens.PadToken.Id);
            
            Assert.NotNull(specialTokens.UnkToken);
            Assert.Equal("[UNK]", specialTokens.UnkToken.Content);
            Assert.Equal(1, specialTokens.UnkToken.Id);
            
            Assert.NotNull(specialTokens.ClsToken);
            Assert.Equal("[CLS]", specialTokens.ClsToken.Content);
            Assert.Equal(2, specialTokens.ClsToken.Id);
            
            Assert.NotNull(specialTokens.SepToken);
            Assert.Equal("[SEP]", specialTokens.SepToken.Content);
            Assert.Equal(3, specialTokens.SepToken.Id);
            
            Assert.NotNull(specialTokens.MaskToken);
            Assert.Equal("[MASK]", specialTokens.MaskToken.Content);
            Assert.Equal(4, specialTokens.MaskToken.Id);

            // AdditionalSpecialTokens should be empty since all tokens are mapped to standard positions
            Assert.Empty(specialTokens.AdditionalSpecialTokens);
        }
    }
}
