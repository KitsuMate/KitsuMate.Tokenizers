using System;
using System.IO;
using System.Text;
using KitsuMate.Tokenizers.Core;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class DefaultTokenizerFactoryTests
    {
        [Fact]
        public void CreateFromTokenizerJson_DetectsWordPieceAndReturnsStub()
        {
            var path = Path.GetTempFileName();

            try
            {
                                File.WriteAllText(path, """
                                {
                                    "model": {
                                        "continuing_subword_prefix": "##",
                                        "vocab": { "hello": 0, "[UNK]": 1 }
                                    }
                                }
                                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(path);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 0 }, tokenizer.EncodeToIds("hello", addSpecialTokens: false));
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
        public void CreateFromTokenizerJson_WordPieceUsesParsedPipelineComponents()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "clean_up_tokenization_spaces": false,
                    "cls_token": "[BOS]",
                    "sep_token": "[EOS]",
                    "normalizer": {
                        "type": "bert",
                        "lowercase": true
                    },
                    "pre_tokenizer": {
                        "type": "WhitespaceSplit"
                    },
                    "decoder": {
                        "type": "WordPiece",
                        "prefix": "##",
                        "cleanup": false
                    },
                    "post_processor": {
                        "type": "BertProcessing",
                        "sep": ["[EOS]", 2],
                        "cls": ["[BOS]", 1]
                    },
                    "model": {
                        "type": "WordPiece",
                        "unk_token": "[UNK]",
                        "continuing_subword_prefix": "##",
                        "vocab": {
                            "[UNK]": 0,
                            "[BOS]": 1,
                            "[EOS]": 2,
                            "hello": 3,
                            "world": 4
                        }
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = Assert.IsAssignableFrom<ITokenizer>(factory.CreateFromTokenizerJson(path));
                var pipeline = tokenizer;

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.IsType<BertNormalizer>(pipeline.Normalizer);
                Assert.IsType<WhitespaceSplitPreTokenizer>(pipeline.PreTokenizer);
                Assert.IsType<BertPostProcessor>(pipeline.PostProcessor);
                Assert.IsType<WordPieceDecoder>(pipeline.Decoder);
                Assert.Equal(new[] { 1, 3, 4, 2 }, tokenizer.EncodeToIds("HELLO world"));
                Assert.Equal("hello world", tokenizer.Decode(new[] { 3, 4 }));
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
        public void ParseTokenizerJsonPipeline_CapturesTokenizerLevelState()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "truncation": {
                        "direction": "Left",
                        "max_length": 128,
                        "strategy": "LongestFirst",
                        "stride": 16
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Left",
                        "length": 128,
                        "pad_to_multiple_of": 8,
                        "pad_id": 99,
                        "pad_type_id": 3,
                        "pad_token": "<pad>"
                    },
                    "added_tokens": [
                        {
                            "id": 99,
                            "content": "<pad>",
                            "single_word": false,
                            "lstrip": false,
                            "rstrip": false,
                            "normalized": false,
                            "special": true
                        },
                        {
                            "id": 100,
                            "content": "<extra>",
                            "single_word": true,
                            "lstrip": true,
                            "rstrip": false,
                            "special": false
                        }
                    ],
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

                var factory = new TokenizerFactory();
                var pipeline = factory.ParseTokenizerJsonPipeline(path);

                Assert.Equal(2, pipeline.AddedTokens.Count);
                Assert.Equal("<pad>", pipeline.AddedTokens[0].Content);
                Assert.True(pipeline.AddedTokens[0].Special);
                Assert.Equal("<extra>", pipeline.AddedTokens[1].Content);
                Assert.True(pipeline.AddedTokens[1].SingleWord);
                Assert.True(pipeline.AddedTokens[1].LStrip);
                Assert.True(pipeline.AddedTokens[1].Normalized);

                Assert.NotNull(pipeline.Truncation);
                Assert.Equal("Left", pipeline.Truncation!.Direction);
                Assert.Equal(128, pipeline.Truncation.MaxLength);
                Assert.Equal("LongestFirst", pipeline.Truncation.Strategy);
                Assert.Equal(16, pipeline.Truncation.Stride);

                Assert.NotNull(pipeline.Padding);
                Assert.Equal("Fixed", pipeline.Padding!.Strategy);
                Assert.Equal("Left", pipeline.Padding.Direction);
                Assert.Equal(128, pipeline.Padding.Length);
                Assert.Equal(99, pipeline.Padding.PadId);
                Assert.Equal(3, pipeline.Padding.PadTypeId);
                Assert.Equal("<pad>", pipeline.Padding.PadToken);
                Assert.Equal(8, pipeline.Padding.PadToMultipleOf);
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
        public void CreateDecoder_BuildsNativeSequenceDecoder()
        {
            var decoder = TokenizerJsonComponentFactory.CreateDecoder(JObject.Parse("""
            {
                "type": "Sequence",
                "decoders": [
                    {
                        "type": "WordPiece",
                        "prefix": "##",
                        "cleanup": false
                    }
                ]
            }
            """));

            Assert.IsType<SequenceDecoder>(decoder);
            Assert.Equal("helloworld", decoder!.Decode(new[] { "hello", "world" }));
        }

        [Fact]
        public void CreatePreTokenizer_BuildsNativeSequencePreTokenizer()
        {
            var preTokenizer = TokenizerJsonComponentFactory.CreatePreTokenizer(JObject.Parse("""
            {
                "type": "Sequence",
                "pretokenizers": [
                    {
                        "type": "WhitespaceSplit"
                    }
                ]
            }
            """));

            Assert.IsType<SequencePreTokenizer>(preTokenizer);
            Assert.Equal(new[] { (0, 5), (6, 5) }, preTokenizer!.PreTokenize("hello world"));
        }

        [Fact]
        public void CreateNormalizer_BuildsNativeSequenceNormalizer()
        {
            var normalizer = TokenizerJsonComponentFactory.CreateNormalizer(JObject.Parse("""
            {
                "type": "Sequence",
                "normalizers": [
                    {
                        "type": "Lowercase"
                    },
                    {
                        "type": "Strip",
                        "left": true,
                        "right": true
                    }
                ]
            }
            """));

            Assert.IsType<SequenceNormalizer>(normalizer);
            Assert.Equal("hello", normalizer!.Normalize(" Hello "));
        }

        [Fact]
        public void CreateFromTokenizerJson_DetectsUnigramAndThrowsNotSupported()
        {
            var path = Path.GetTempFileName();

            try
            {
                                File.WriteAllText(path, """
                                {
                                    "model": {
                                        "scores": [ -1.0, -2.0 ]
                                    }
                                }
                                """);

                var factory = new TokenizerFactory();
                var exception = Assert.Throws<TokenizerNotSupportedException>(() => factory.CreateFromTokenizerJson(path));

                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, exception.BackendType);
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
        public void CreateFromTokenizerJson_DoesNotAutoApplyPipelineTruncationAndPaddingDuringEncode()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "truncation": {
                        "direction": "Left",
                        "max_length": 4,
                        "strategy": "LongestFirst",
                        "stride": 0
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Left",
                        "length": 6,
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
                            "again": 3,
                            "<pad>": 99
                        }
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(path);
                var encoding = tokenizer.Encode("hello world again hello world", addSpecialTokens: false);

                Assert.Equal(new[] { 1, 2, 3, 1, 2 }, encoding.Ids);
                Assert.Equal(new[] { "hello", "world", "again", "hello", "world" }, encoding.Tokens);
                Assert.Equal(new[] { 0, 0, 0, 0, 0 }, encoding.TypeIds);
                Assert.Equal(new[] { 1, 1, 1, 1, 1 }, encoding.AttentionMask);
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
        public void CreateFromTokenizerJson_DoesNotAutoApplyPairTruncationBeforePostProcessingOrPaddingAfterward()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "truncation": {
                        "direction": "Left",
                        "max_length": 7,
                        "strategy": "LongestFirst",
                        "stride": 0
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Left",
                        "length": 9,
                        "pad_id": 99,
                        "pad_type_id": 7,
                        "pad_token": "<pad>"
                    },
                    "post_processor": {
                        "type": "BertProcessing",
                        "sep": ["[SEP]", 11],
                        "cls": ["[CLS]", 10]
                    },
                    "model": {
                        "type": "WordPiece",
                        "unk_token": "[UNK]",
                        "continuing_subword_prefix": "##",
                        "vocab": {
                            "[UNK]": 0,
                            "hello": 1,
                            "world": 2,
                            "again": 3,
                            "[CLS]": 10,
                            "[SEP]": 11,
                            "<pad>": 99
                        }
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(path);
                var encoding = tokenizer.EncodePair("hello world again", "world again", addSpecialTokens: true);

                Assert.Equal(new[] { 10, 1, 2, 3, 11, 2, 3, 11 }, encoding.Ids);
                Assert.Equal(new[] { "[CLS]", "hello", "world", "again", "[SEP]", "world", "again", "[SEP]" }, encoding.Tokens);
                Assert.Equal(new[] { 0, 0, 0, 0, 0, 1, 1, 1 }, encoding.TypeIds);
                Assert.Equal(new[] { 1, 1, 1, 1, 1, 1, 1, 1 }, encoding.AttentionMask);
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
        public void CreateFromTokenizerJson_BpeRetainsParsedPipelineComponents()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, """
                {
                    "normalizer": {
                        "type": "Lowercase"
                    },
                    "pre_tokenizer": {
                        "type": "WhitespaceSplit"
                    },
                    "decoder": {
                        "type": "BPE"
                    },
                    "post_processor": {
                        "type": "BertProcessing",
                        "sep": ["[SEP]", 2],
                        "cls": ["[CLS]", 1]
                    },
                    "truncation": {
                        "max_length": 4,
                        "stride": 0,
                        "strategy": "LongestFirst",
                        "direction": "Right"
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Right",
                        "length": 4,
                        "pad_id": 0,
                        "pad_type_id": 0,
                        "pad_token": "<pad>"
                    },
                    "model": {
                        "type": "BPE",
                        "vocab": {
                            "hello": 0
                        },
                        "merges": []
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = Assert.IsAssignableFrom<ITokenizer>(factory.CreateFromTokenizerJson(path));
                var pipeline = tokenizer;

                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
                Assert.IsType<LowercaseNormalizer>(pipeline.Normalizer);
                Assert.IsType<WhitespaceSplitPreTokenizer>(pipeline.PreTokenizer);
                Assert.IsType<BertPostProcessor>(pipeline.PostProcessor);
                Assert.IsType<BpeDecoder>(pipeline.Decoder);
                Assert.Null(pipeline.Truncation);
                Assert.Null(pipeline.Padding);
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
        public void CreateSentencePieceUnigram_RejectsBpeModels()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleBpeModel());

            var exception = Assert.Throws<NotSupportedException>(() => factory.CreateSentencePieceUnigram(stream));

            Assert.Contains("Unigram", exception.Message);
        }

        [Fact]
        public void CreateSentencePiece_CreatesWorkingUnigramTokenizer()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleUnigramModel());

            var tokenizer = factory.CreateSentencePieceUnigram(stream);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
            Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void CreateSentencePiece_HonorsRemoveExtraWhitespacesDuringNormalization()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleUnigramModel(
                addDummyPrefix: false,
                removeExtraWhitespaces: true,
                escapeWhitespaces: false,
                treatWhitespaceAsSuffix: false));

            var tokenizer = factory.CreateSentencePieceUnigram(stream);
            var encoding = tokenizer.Encode("   hello   ");

            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
            Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void CreateSentencePiece_HonorsDummyPrefixAndEscapedWhitespaceNormalization()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateDummyPrefixUnigramModel());

            var tokenizer = factory.CreateSentencePieceUnigram(stream);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "▁he", "llo" }, encoding.Tokens);
            Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void CreateSentencePiece_AppliesPrecompiledCharsMapBeforeTokenization()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateCharsMapUnigramModel());

            var tokenizer = factory.CreateSentencePieceUnigram(stream);
            var encoding = tokenizer.Encode("héllo");

            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
            Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void CreateFromTokenizerJson_AppliesNativePrecompiledCharsMapNormalizer()
        {
            var path = Path.GetTempFileName();

            try
            {
                var charsMap = Convert.ToBase64String(SentencePieceTestData.CreateAccentCharMapBlob());
                File.WriteAllText(path, $$"""
                {
                    "normalizer": {
                        "type": "Precompiled",
                        "precompiled_charsmap": "{{charsMap}}"
                    },
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

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(path);
                var encoding = tokenizer.Encode("héllo", addSpecialTokens: false);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 1 }, encoding.Ids);
                Assert.Equal(new[] { "hello" }, encoding.Tokens);
                Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
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
        public void CreateSentencePiece_CreatesWorkingBpeTokenizer()
        {
            var factory = new TokenizerFactory();
            using var stream = new MemoryStream(SentencePieceTestData.CreateSimpleBpeModel());

            var tokenizer = factory.CreateSentencePieceBpe(stream);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
            Assert.Equal(new[] { 13 }, encoding.Ids);
            Assert.Equal(new[] { "▁hello" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5) }, encoding.Offsets);
            Assert.Equal("hello", tokenizer.Decode(encoding.Ids));
        }

        [Fact]
        public void CreateFromTokenizerJson_InfersSentencePieceUnigramFromSiblingModelWhenModelTypeIsMissing()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "spiece.model"), SentencePieceTestData.CreateSimpleUnigramModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                    "model": {
                        "unk_id": 0,
                        "vocab": []
                    },
                    "normalizer": {
                        "type": "Precompiled"
                    },
                    "pre_tokenizer": {
                        "type": "Sequence"
                    },
                    "decoder": {
                        "type": "Metaspace"
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(Path.Combine(directory, "tokenizer.json"));
                var encoding = tokenizer.Encode("hello");

                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
                Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateFromTokenizerJson_SentencePieceUnigramRetainsParsedPipelineComponents()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "spiece.model"), SentencePieceTestData.CreateSimpleUnigramModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                    "model": {
                        "unk_id": 0,
                        "vocab": []
                    },
                    "normalizer": {
                        "type": "Lowercase"
                    },
                    "pre_tokenizer": {
                        "type": "WhitespaceSplit"
                    },
                    "decoder": {
                        "type": "Metaspace"
                    },
                    "post_processor": {
                        "type": "BertProcessing",
                        "sep": ["[SEP]", 2],
                        "cls": ["[CLS]", 1]
                    },
                    "truncation": {
                        "max_length": 8,
                        "stride": 0,
                        "strategy": "LongestFirst",
                        "direction": "Right"
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Right",
                        "length": 8,
                        "pad_id": 0,
                        "pad_type_id": 0,
                        "pad_token": "<pad>"
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = Assert.IsAssignableFrom<ITokenizer>(factory.CreateFromTokenizerJson(Path.Combine(directory, "tokenizer.json")));
                var pipeline = tokenizer;

                Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
                Assert.IsType<LowercaseNormalizer>(pipeline.Normalizer);
                Assert.IsType<WhitespaceSplitPreTokenizer>(pipeline.PreTokenizer);
                Assert.IsType<BertPostProcessor>(pipeline.PostProcessor);
                Assert.IsType<MetaspaceDecoder>(pipeline.Decoder);
                Assert.NotNull(pipeline.Truncation);
                Assert.NotNull(pipeline.Padding);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateFromTokenizerJson_InfersSentencePieceBpeFromActualModelTypeWhenFilenameContainsBpe()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "sentencepiece.bpe.model"), SentencePieceTestData.CreateSimpleBpeModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                    "model": {
                        "unk_id": 0,
                        "vocab": []
                    },
                    "post_processor": {
                        "type": "TemplateProcessing",
                        "single": [
                            { "SpecialToken": { "id": "<s>", "type_id": 0 } },
                            { "Sequence": { "id": "A", "type_id": 0 } },
                            { "SpecialToken": { "id": "</s>", "type_id": 0 } }
                        ],
                        "special_tokens": {
                            "<s>": { "id": "<s>", "ids": [1], "tokens": ["<s>"] },
                            "</s>": { "id": "</s>", "ids": [2], "tokens": ["</s>"] }
                        }
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(Path.Combine(directory, "tokenizer.json"));
                var encoding = tokenizer.Encode("hello");

                Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
                Assert.Equal(new[] { 1, 13, 2 }, encoding.Ids);
                Assert.Equal(new[] { "<s>", "▁hello", "</s>" }, encoding.Tokens);
                Assert.Equal(new (int Start, int End)[] { (0, 0), (0, 5), (0, 0) }, encoding.Offsets);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateFromTokenizerJson_SentencePieceBpeRetainsParsedPipelineComponents()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "sentencepiece.bpe.model"), SentencePieceTestData.CreateSimpleBpeModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                    "model": {
                        "unk_id": 0,
                        "vocab": []
                    },
                    "normalizer": {
                        "type": "Lowercase"
                    },
                    "pre_tokenizer": {
                        "type": "WhitespaceSplit"
                    },
                    "decoder": {
                        "type": "Metaspace"
                    },
                    "post_processor": {
                        "type": "BertProcessing",
                        "sep": ["[SEP]", 2],
                        "cls": ["[CLS]", 1]
                    },
                    "truncation": {
                        "max_length": 8,
                        "stride": 0,
                        "strategy": "LongestFirst",
                        "direction": "Right"
                    },
                    "padding": {
                        "strategy": "Fixed",
                        "direction": "Right",
                        "length": 8,
                        "pad_id": 0,
                        "pad_type_id": 0,
                        "pad_token": "<pad>"
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = Assert.IsAssignableFrom<ITokenizer>(factory.CreateFromTokenizerJson(Path.Combine(directory, "tokenizer.json")));
                var pipeline = tokenizer;

                Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
                Assert.IsType<LowercaseNormalizer>(pipeline.Normalizer);
                Assert.IsType<WhitespaceSplitPreTokenizer>(pipeline.PreTokenizer);
                Assert.IsType<BertPostProcessor>(pipeline.PostProcessor);
                Assert.IsType<MetaspaceDecoder>(pipeline.Decoder);
                Assert.NotNull(pipeline.Truncation);
                Assert.NotNull(pipeline.Padding);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateFromTokenizerJson_SentencePieceBpeCanDisableDummyPrefixFromPreTokenizerShape()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.WriteAllBytes(Path.Combine(directory, "tokenizer.model"), SentencePieceTestData.CreateSimpleBpeModel());
                File.WriteAllText(Path.Combine(directory, "tokenizer.json"), """
                {
                    "model": {
                        "type": "BPE",
                        "unk_id": 0,
                        "vocab": []
                    },
                    "pre_tokenizer": {
                        "type": "Split",
                        "pattern": { "String": " " },
                        "behavior": "MergedWithPrevious",
                        "invert": false
                    }
                }
                """);

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateFromTokenizerJson(Path.Combine(directory, "tokenizer.json"));
                var encoding = tokenizer.Encode("hello");

                Assert.Equal(TokenizerBackendType.SentencePieceBpe, tokenizer.BackendType);
                Assert.Equal(new[] { 12 }, encoding.Ids);
                Assert.Equal(new[] { "hello" }, encoding.Tokens);
                Assert.Equal(new (int Start, int End)[] { (0, 5) }, encoding.Offsets);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateWordPiece_CreatesWorkingTokenizerFromVocab()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateWordPiece(path);

                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
                Assert.Equal(new[] { 2, 4, 5, 3 }, tokenizer.EncodeToIds("hello world"));
                Assert.Equal("hello worlds", tokenizer.Decode(new[] { 4, 5, 6 }));
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
        public void CreateFromTokenizerJson_CreatesWorkingTokenizerFromBytesAndStream()
        {
            var json = Encoding.UTF8.GetBytes("""
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

            var factory = new TokenizerFactory();
            var fromBytes = factory.CreateFromTokenizerJson(json);
            using var jsonStream = new MemoryStream(json, writable: false);
            var fromStream = factory.CreateFromTokenizerJson(jsonStream);

            Assert.Equal(TokenizerBackendType.WordPiece, fromBytes.BackendType);
            Assert.Equal(new[] { 1 }, fromBytes.EncodeToIds("HELLO", addSpecialTokens: false));
            Assert.Equal(new[] { 1 }, fromStream.EncodeToIds("HELLO", addSpecialTokens: false));
        }

        [Fact]
        public void CreateFromTokenizerJson_FromBytesWithUnknownModel_DoesNotTreatMemorySourceAsPath()
        {
            var json = Encoding.UTF8.GetBytes("""
            {
                "model": {
                }
            }
            """);

            var factory = new TokenizerFactory();

            var exception = Assert.Throws<TokenizerNotSupportedException>(() => factory.CreateFromTokenizerJson(json));

            Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
        }

        [Fact]
        public void CreateFromTokenizerJson_FromBytes_SupportsObjectShapedPaddingStrategy()
        {
            var json = Encoding.UTF8.GetBytes("""
            {
                "padding": {
                    "strategy": {
                        "Fixed": 8
                    },
                    "direction": "Right",
                    "pad_id": 0,
                    "pad_type_id": 0,
                    "pad_token": "[PAD]"
                },
                "normalizer": {
                    "type": "BertNormalizer",
                    "clean_text": true,
                    "handle_chinese_chars": true,
                    "strip_accents": null,
                    "lowercase": true
                },
                "pre_tokenizer": {
                    "type": "BertPreTokenizer"
                },
                "post_processor": {
                    "type": "TemplateProcessing",
                    "single": [
                        { "SpecialToken": { "id": "[CLS]", "type_id": 0 } },
                        { "Sequence": { "id": "A", "type_id": 0 } },
                        { "SpecialToken": { "id": "[SEP]", "type_id": 0 } }
                    ],
                    "special_tokens": {
                        "[CLS]": { "id": "[CLS]", "ids": [101], "tokens": ["[CLS]"] },
                        "[SEP]": { "id": "[SEP]", "ids": [102], "tokens": ["[SEP]"] }
                    }
                },
                "decoder": {
                    "type": "WordPiece",
                    "prefix": "##",
                    "cleanup": true
                },
                "model": {
                    "type": "WordPiece",
                    "unk_token": "[UNK]",
                    "continuing_subword_prefix": "##",
                    "vocab": {
                        "[PAD]": 0,
                        "[UNK]": 100,
                        "[CLS]": 101,
                        "[SEP]": 102,
                        "hello": 200
                    }
                }
            }
            """);

            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateFromTokenizerJson(json);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);
            Assert.Equal(3, encoding.Ids.Count);
            Assert.Equal(new[] { 101, 200, 102 }, encoding.Ids);
        }

        [Fact]
        public void CreateWordPiece_CreatesWorkingTokenizerFromBytesAndStream()
        {
            var vocab = Encoding.UTF8.GetBytes("[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n");
            var factory = new TokenizerFactory();
            var fromBytes = factory.CreateWordPiece(vocab);
            using var vocabStream = new MemoryStream(vocab, writable: false);
            var fromStream = factory.CreateWordPiece(vocabStream);

            Assert.Equal(new[] { 2, 4, 5, 3 }, fromBytes.EncodeToIds("hello world"));
            Assert.Equal(new[] { 2, 4, 5, 3 }, fromStream.EncodeToIds("hello world"));
        }

        [Fact]
        public void CreateBpe_CreatesWorkingTokenizerFromFiles()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                var vocabPath = Path.Combine(directory, "vocab.json");
                var mergesPath = Path.Combine(directory, "merges.txt");
                File.WriteAllText(vocabPath, "{\"h\":0,\"e\":1,\"l\":2,\"o\":3,\"he\":4,\"hel\":5,\"hell\":6,\"hello\":7}");
                File.WriteAllText(mergesPath, "#version: 0.2\nh e\nhe l\nhel l\nhell o\n");

                var factory = new TokenizerFactory();
                var tokenizer = factory.CreateBpe(vocabPath, mergesPath);
                var encoding = tokenizer.Encode("hello", addSpecialTokens: false);

                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
                Assert.Equal(new[] { 7 }, encoding.Ids);
                Assert.Equal(new[] { "hello" }, encoding.Tokens);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void CreateBpe_CreatesWorkingTokenizerFromBytesAndStreams()
        {
            var vocab = Encoding.UTF8.GetBytes("{\"h\":0,\"e\":1,\"l\":2,\"o\":3,\"he\":4,\"hel\":5,\"hell\":6,\"hello\":7}");
            var merges = Encoding.UTF8.GetBytes("#version: 0.2\nh e\nhe l\nhel l\nhell o\n");
            var factory = new TokenizerFactory();
            var fromBytes = factory.CreateBpe(vocab, merges);
            using var vocabStream = new MemoryStream(vocab, writable: false);
            using var mergesStream = new MemoryStream(merges, writable: false);
            var fromStreams = factory.CreateBpe(vocabStream, mergesStream);

            Assert.Equal(new[] { 7 }, fromBytes.Encode("hello", addSpecialTokens: false).Ids);
            Assert.Equal(new[] { 7 }, fromStreams.Encode("hello", addSpecialTokens: false).Ids);
        }

        [Fact]
        public void CreateTiktoken_CreatesWorkingTokenizerFromFixture()
        {
            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateTiktoken(GetGpt2TiktokenPath());
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Tiktoken, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
        }

        [Fact]
        public void CreateTiktoken_CreatesWorkingTokenizerFromBytesAndStream()
        {
            var vocab = File.ReadAllBytes(GetGpt2TiktokenPath());
            var factory = new TokenizerFactory();
            var fromBytes = factory.CreateTiktoken(vocab, "gpt2");
            using var vocabStream = new MemoryStream(vocab, writable: false);
            var fromStream = factory.CreateTiktoken(vocabStream, "gpt2");

            Assert.Equal(new[] { 15496, 11, 995, 0 }, fromBytes.Encode("Hello, world!").Ids);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, fromStream.Encode("Hello, world!").Ids);
        }

        [Fact]
        public void CreateSentencePiece_CreatesWorkingTokenizerFromBytes()
        {
            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateSentencePieceUnigram(SentencePieceTestData.CreateSimpleUnigramModel());
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(TokenizerBackendType.SentencePieceUnigram, tokenizer.BackendType);
            Assert.Equal(new[] { 3, 4 }, encoding.Ids);
            Assert.Equal(new[] { "he", "llo" }, encoding.Tokens);
        }

        [Fact]
        public void CreateFromTokenizerJson_LoadsRealGpt2Fixture()
        {
            var tokenizerJsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "IntegrationTests", "GPT2", "tokenizer.json"));

            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateFromTokenizerJson(tokenizerJsonPath);
            var encoding = tokenizer.Encode("Hello, world!");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 15496, 11, 995, 0 }, encoding.Ids);
            Assert.Equal(new[] { "Hello", ",", "Ġworld", "!" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5), (5, 6), (6, 12), (12, 13) }, encoding.Offsets);
        }

        [Fact]
        public void CreateFromTokenizerJson_LoadsCachedRobertaFixture()
        {
            var tokenizerJsonPath = Path.Combine(GetCachedModelDirectory("roberta-base--main"), "tokenizer.json");
            if (!File.Exists(tokenizerJsonPath))
            {
                return;
            }

            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateFromTokenizerJson(tokenizerJsonPath);
            var encoding = tokenizer.Encode("Hello, how are you?");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 31414, 6, 141, 32, 47, 116, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "Hello", ",", "Ġhow", "Ġare", "Ġyou", "?", "</s>" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 0), (0, 5), (5, 6), (7, 10), (11, 14), (15, 18), (18, 19), (0, 0) }, encoding.Offsets);
            Assert.Equal("<s>Hello, how are you?</s>", tokenizer.Decode(encoding.Ids));
            Assert.Equal("Hello, how are you?", tokenizer.Decode(new[] { 0, 31414, 6, 141, 32, 47, 116, 2, 1 }, skipSpecialTokens: true));
        }

        [Fact]
        public void CreateFromTokenizerJson_LoadsCachedRobertaMaskAsSingleAddedToken()
        {
            var tokenizerJsonPath = Path.Combine(GetCachedModelDirectory("roberta-base--main"), "tokenizer.json");
            if (!File.Exists(tokenizerJsonPath))
            {
                return;
            }

            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateFromTokenizerJson(tokenizerJsonPath);
            var encoding = tokenizer.Encode("<mask>");

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 0, 50264, 2 }, encoding.Ids);
            Assert.Equal(new[] { "<s>", "<mask>", "</s>" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void CreateFromTokenizerJson_LoadsCachedT5FixtureThroughSentencePieceUnigram()
        {
            var tokenizerJsonPath = Path.Combine(GetCachedModelDirectory("t5-small--main"), "tokenizer.json");
            if (!File.Exists(tokenizerJsonPath))
            {
                return;
            }

            var factory = new TokenizerFactory();
            var tokenizer = factory.CreateFromTokenizerJson(tokenizerJsonPath);
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