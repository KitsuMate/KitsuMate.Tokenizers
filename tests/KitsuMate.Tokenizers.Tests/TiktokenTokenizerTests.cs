using System;
using System.IO;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TiktokenTokenizerTests
    {
        [Fact]
        public void FromFile_LoadsGpt2EncodingAndMatchesKnownIds()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetGpt2TiktokenPath());
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5), (5, 6), (6, 12), (12, 13) }, encoding.Offsets);
            Assert.Equal("Hello, world!", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void FromFile_RecognizesEndOfTextAsSpecialToken()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetGpt2TiktokenPath());
            var encoding = tokenizer.Encode("<|endoftext|>");

            Assert.Equal(new[] { 50256 }, encoding.Ids);
            Assert.Equal(new[] { "<|endoftext|>" }, encoding.Tokens);
            Assert.Equal(new[] { 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<|endoftext|>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void EncodePair_TracksSequenceMetadataWithoutInjectedSpecials()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetGpt2TiktokenPath());
            var encoding = tokenizer.EncodePair("Hello", "world");

            Assert.Equal(new[] { 15496, 6894 }, encoding.Ids);
            Assert.Equal(new[] { 0, 1 }, encoding.TypeIds);
            Assert.Equal((0, 1), encoding.SequenceRanges[0]);
            Assert.Equal((1, 2), encoding.SequenceRanges[1]);
        }

        [Fact]
        public void FromFile_LoadsCl100kBaseEncodingAndMatchesDocumentedIds()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetCl100kBaseTiktokenPath());
            var encoding = tokenizer.Encode("tiktoken is great!");

            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Equal(new[] { 83, 1609, 5963, 374, 2294, 0 }, encoding.Ids);
            Assert.Equal("tiktoken is great!", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void FromFile_LoadsO200kBaseEncodingAndMatchesDocumentedIds()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetO200kBaseTiktokenPath());
            var encoding = tokenizer.Encode("Hello, world! How are you today?");

            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Equal(new[] { 13225, 11, 2375, 0, 3253, 553, 481, 4044, 30 }, encoding.Ids);
            Assert.Equal("Hello, world! How are you today?", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void FromFile_RecognizesCl100kBaseEndOfTextAsSpecialToken()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetCl100kBaseTiktokenPath());
            var encoding = tokenizer.Encode("<|endoftext|>");

            Assert.Equal(new[] { 100257 }, encoding.Ids);
            Assert.Equal(new[] { "<|endoftext|>" }, encoding.Tokens);
            Assert.Equal(new[] { 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<|endoftext|>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromFile_RecognizesO200kBaseEndOfPromptAsSpecialToken()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetO200kBaseTiktokenPath());
            var encoding = tokenizer.Encode("<|endofprompt|>");

            Assert.Equal(new[] { 200018 }, encoding.Ids);
            Assert.Equal(new[] { "<|endofprompt|>" }, encoding.Tokens);
            Assert.Equal(new[] { 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<|endofprompt|>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromFile_LoadsP50kEditVariantAndRecognizesFimPrefix()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetP50kBaseTiktokenPath(), "p50k_edit");
            var encoding = tokenizer.Encode("<|fim_prefix|>");

            Assert.Equal(new[] { 50281 }, encoding.Ids);
            Assert.Equal(new[] { "<|fim_prefix|>" }, encoding.Tokens);
            Assert.Equal(new[] { 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<|fim_prefix|>", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void FromFile_LoadsO200kHarmonyVariantAndRecognizesHarmonySpecialTokens()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetO200kBaseTiktokenPath(), "o200k_harmony");
            var encoding = tokenizer.Encode("<|channel|><|message|><|endofprompt|>");

            Assert.Equal(new[] { 200005, 200008, 200018 }, encoding.Ids);
            Assert.Equal(new[] { "<|channel|>", "<|message|>", "<|endofprompt|>" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 1, 1 }, encoding.SpecialTokensMask);
            Assert.Equal("<|channel|><|message|><|endofprompt|>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void FromFile_ResolvesCl100kBaseModelAliases()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetCl100kBaseTiktokenPath(), "gpt-4");
            var encoding = tokenizer.Encode("<|endofprompt|>");

            Assert.Equal("cl100k_base", tokenizer.Name);
            Assert.Equal(new[] { 100276 }, encoding.Ids);
        }

        [Fact]
        public void FromFile_ResolvesO200kBaseModelPrefixes()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetO200kBaseTiktokenPath(), "gpt-4o-2024-05-13");
            var encoding = tokenizer.Encode("Hello, world! How are you today?");

            Assert.Equal("o200k_base", tokenizer.Name);
            Assert.Equal(new[] { 13225, 11, 2375, 0, 3253, 553, 481, 4044, 30 }, encoding.Ids);
        }

        [Fact]
        public void FromFile_ResolvesO200kHarmonyModelPrefixes()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetO200kBaseTiktokenPath(), "gpt-oss-120b");
            var encoding = tokenizer.Encode("<|channel|>");

            Assert.Equal("o200k_harmony", tokenizer.Name);
            Assert.Equal(new[] { 200005 }, encoding.Ids);
        }

        [Fact]
        public void FromFile_ResolvesP50kEditModelAliases()
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetP50kBaseTiktokenPath(), "text-davinci-edit-001");
            var encoding = tokenizer.Encode("<|fim_suffix|>");

            Assert.Equal("p50k_edit", tokenizer.Name);
            Assert.Equal(new[] { 50283 }, encoding.Ids);
        }

        [Theory]
        [InlineData("text-davinci-003", "p50k_base", "<|endoftext|>", 50256)]
        [InlineData("gpt-3.5-turbo-0301", "cl100k_base", "<|endofprompt|>", 100276)]
        [InlineData("gpt-5", "o200k_base", "<|endofprompt|>", 200018)]
        [InlineData("o1", "o200k_base", "<|endofprompt|>", 200018)]
        [InlineData("o3-mini", "o200k_base", "<|endofprompt|>", 200018)]
        [InlineData("text-embedding-3-large", "cl100k_base", "<|endofprompt|>", 100276)]
        [InlineData("gpt-35-turbo", "cl100k_base", "<|endofprompt|>", 100276)]
        public void FromFile_ResolvesAdditionalRemoteModelMappings(string modelName, string expectedEncodingName, string probeText, int expectedTokenId)
        {
            var tokenizer = Tokenizer.CreateTiktoken(GetPathForEncoding(expectedEncodingName), modelName);
            var encoding = tokenizer.Encode(probeText);

            Assert.Equal(expectedEncodingName, tokenizer.Name);
            Assert.Equal(new[] { expectedTokenId }, encoding.Ids);
        }

        private static string GetGpt2TiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "gpt2.tiktoken"));
        }

        private static string GetCl100kBaseTiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "cl100k_base.tiktoken"));
        }

        private static string GetP50kBaseTiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "p50k_base.tiktoken"));
        }

        private static string GetO200kBaseTiktokenPath()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "TestData", "Tiktoken", "o200k_base.tiktoken"));
        }

        private static string GetPathForEncoding(string encodingName)
        {
            return encodingName switch
            {
                "gpt2" => GetGpt2TiktokenPath(),
                "r50k_base" => GetGpt2TiktokenPath(),
                "p50k_base" => GetP50kBaseTiktokenPath(),
                "p50k_edit" => GetP50kBaseTiktokenPath(),
                "cl100k_base" => GetCl100kBaseTiktokenPath(),
                "o200k_base" => GetO200kBaseTiktokenPath(),
                "o200k_harmony" => GetO200kBaseTiktokenPath(),
                _ => throw new InvalidOperationException($"Unsupported test encoding '{encodingName}'."),
            };
        }
    }
}