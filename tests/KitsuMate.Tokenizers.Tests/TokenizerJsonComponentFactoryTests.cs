using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers.Core;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerJsonComponentFactoryTests
    {
        [Fact]
        public void CreateDecoder_BuildsWordPieceDecoder()
        {
            var decoder = TokenizerJsonComponentFactory.CreateDecoder(JObject.Parse("""
            {
                "type": "WordPiece",
                "prefix": "##",
                "cleanup": true
            }
            """));

            Assert.IsType<WordPieceDecoder>(decoder);
            Assert.Equal("unbelievable", decoder!.Decode(new[] { "un", "##believ", "##able" }));
        }

        [Fact]
        public void CreateNormalizer_BuildsLowercaseNormalizer()
        {
            var normalizer = TokenizerJsonComponentFactory.CreateNormalizer(JObject.Parse("""
            {
                "type": "Lowercase"
            }
            """));

            Assert.IsType<LowercaseNormalizer>(normalizer);
            Assert.Equal("hello world", normalizer!.Normalize("HELLO World"));
        }

        [Fact]
        public void CreatePreTokenizer_BuildsWhitespacePreTokenizer()
        {
            var preTokenizer = TokenizerJsonComponentFactory.CreatePreTokenizer(JObject.Parse("""
            {
                "type": "WhitespaceSplit"
            }
            """));

            Assert.IsType<WhitespaceSplitPreTokenizer>(preTokenizer);
            Assert.Equal(new[] { (0, 5), (6, 5), (12, 4) }, preTokenizer!.PreTokenize("Hello World Test").ToList());
        }

        [Fact]
        public void CreatePostProcessor_BuildsBertPostProcessor()
        {
            var postProcessor = TokenizerJsonComponentFactory.CreatePostProcessor(new PostProcessorConfig
            {
                Type = "BertProcessing",
                Cls = new List<string> { "[CLS]", "101" },
                Sep = new List<string> { "[SEP]", "102" }
            });

            Assert.IsType<BertPostProcessor>(postProcessor);
        }

        [Fact]
        public void CreatePostProcessor_ProcessesSingleEncoding()
        {
            var postProcessor = TokenizerJsonComponentFactory.CreatePostProcessor(new PostProcessorConfig
            {
                Type = "BertProcessing",
                Cls = new List<string> { "[CLS]", "101" },
                Sep = new List<string> { "[SEP]", "102" }
            });

            var result = postProcessor!.Process(
                new List<EncodingResult>
                {
                    EncodingResult.FromTokenData(new List<int> { 1, 2 }, new List<string> { "hello", "world" }, 0)
                },
                addSpecialTokens: true);

            Assert.Equal(new[] { "[CLS]", "hello", "world", "[SEP]" }, result.Tokens);
            Assert.Equal(new[] { 101, 1, 2, 102 }, result.Ids);
        }
    }
}