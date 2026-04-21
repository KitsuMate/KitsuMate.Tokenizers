using System;
using System.IO;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class BpeFactoryTests
    {
        [Fact]
        public void CreateBpe_LoadsVocabularyAndMerges()
        {
            var directory = CreateTempDirectory();

            try
            {
                var vocabPath = Path.Combine(directory, "vocab.json");
                var mergesPath = Path.Combine(directory, "merges.txt");
                File.WriteAllText(vocabPath, "{\"[UNK]\":0,\"h\":1,\"e\":2,\"l\":3,\"o</w>\":4,\"he\":5,\"hel\":6,\"hell\":7,\"hello</w>\":8,\"w\":9,\"o\":10,\"r\":11,\"d</w>\":12,\"wo\":13,\"wor\":14,\"worl\":15,\"world</w>\":16}");
                File.WriteAllText(mergesPath, "#version: 0.2\nh e\nhe l\nhel l\nhell o</w>\nw o\nwo r\nwor l\nworl d</w>\n");

                var tokenizer = Tokenizer.CreateBpe(vocabPath, mergesPath, new BpeTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    EndOfWordSuffix = "</w>",
                });
                var encoding = tokenizer.Encode("hello world");

                Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
                Assert.Equal(new[] { 8, 16 }, tokenizer.EncodeToIds("hello world"));
                Assert.Equal("hello world", tokenizer.Decode(new[] { 8, 16 }));
                Assert.Equal(new[] { "hello</w>", "world</w>" }, encoding.Tokens);
                Assert.Equal(new (int Start, int End)[] { (0, 5), (6, 11) }, encoding.Offsets);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void CreateBpeRuntime_LoadsBpeScaffold()
        {
            var root = JObject.Parse("""
            {
                            "pre_tokenizer": {
                                "type": "WhitespaceSplit"
                            },
                            "decoder": {
                                "type": "BPEDecoder",
                                "suffix": "</w>"
                            },
              "model": {
                "type": "BPE",
                "vocab": {
                                    "[UNK]": 0,
                                    "h": 1,
                                    "e": 2,
                                    "l": 3,
                                    "o</w>": 4,
                                    "he": 5,
                                    "hel": 6,
                                    "hell": 7,
                                    "hello</w>": 8,
                                    "w": 9,
                                    "o": 10,
                                    "r": 11,
                                    "d</w>": 12,
                                    "wo": 13,
                                    "wor": 14,
                                    "worl": 15,
                                    "world</w>": 16
                },
                                "unk_token": "[UNK]",
                                "end_of_word_suffix": "</w>",
                                "merges": ["h e", "he l", "hel l", "hell o</w>", "w o", "wo r", "wor l", "worl d</w>"]
              }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);

            Assert.Equal(TokenizerBackendType.Bpe, tokenizer.BackendType);
            Assert.Equal(new[] { 8, 16 }, tokenizer.EncodeToIds("hello world"));
            Assert.Equal("hello world", tokenizer.Decode(new[] { 8, 16 }));
        }

        [Fact]
        public void CreateBpeRuntime_WithByteLevelConfig_UsesVisibleSpaceTokens()
        {
            var root = JObject.Parse("""
            {
                "pre_tokenizer": {
                    "type": "ByteLevel",
                    "add_prefix_space": false,
                    "trim_offsets": true
                },
                "decoder": {
                    "type": "ByteLevel",
                    "add_prefix_space": true,
                    "trim_offsets": true
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
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
                    },
                    "merges": ["h e", "he l", "hel l", "hell o", "Ġ t", "Ġt he", "Ġthe r", "Ġther e"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hello there");

            Assert.Equal(new[] { 10, 14 }, encoding.Ids);
            Assert.Equal(new[] { "hello", "Ġthere" }, encoding.Tokens);
            Assert.Equal("hello there", tokenizer.Decode(encoding.Ids));
            Assert.Equal(new (int Start, int End)[] { (0, 5), (5, 11) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithoutPreTokenizer_PreservesExplicitSpaceTokenFromTokenizerJson()
        {
            var root = JObject.Parse("""
            {
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "a": 0,
                        " ": 1,
                        "b": 2
                    },
                    "merges": []
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("a b", addSpecialTokens: false);

            Assert.Equal(new[] { 0, 1, 2 }, encoding.Ids);
            Assert.Equal(new[] { "a", " ", "b" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 1), (1, 2), (2, 3) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithByteLevelPostProcessor_TrimsLeadingSpaceOffsets()
        {
            var root = JObject.Parse("""
            {
                "pre_tokenizer": {
                    "type": "ByteLevel",
                    "add_prefix_space": false,
                    "trim_offsets": true
                },
                "post_processor": {
                    "type": "ByteLevel",
                    "add_prefix_space": false,
                    "trim_offsets": true
                },
                "decoder": {
                    "type": "ByteLevel",
                    "add_prefix_space": true,
                    "trim_offsets": true
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
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
                    },
                    "merges": ["h e", "he l", "hel l", "hell o", "Ġ t", "Ġt he", "Ġthe r", "Ġther e"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hello there");

            Assert.Equal(new[] { 10, 14 }, encoding.Ids);
            Assert.Equal(new[] { "hello", "Ġthere" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5), (6, 11) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithAddedSpecialToken_EncodesExactMatch()
        {
            var root = JObject.Parse("""
            {
                "added_tokens": [
                    {
                        "id": 99,
                        "content": "<mask>",
                        "special": true
                    }
                ],
                "decoder": {
                    "type": "BPEDecoder",
                    "suffix": "</w>"
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "h": 1,
                        "e": 2,
                        "l": 3,
                        "o</w>": 4,
                        "he": 5,
                        "hel": 6,
                        "hell": 7,
                        "hello</w>": 8
                    },
                    "unk_token": "[UNK]",
                    "end_of_word_suffix": "</w>",
                    "merges": ["h e", "he l", "hel l", "hell o</w>"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("<mask>", addSpecialTokens: false);

            Assert.Equal(new[] { 99 }, encoding.Ids);
            Assert.Equal(new[] { "<mask>" }, encoding.Tokens);
            Assert.Equal(new[] { 1 }, encoding.SpecialTokensMask);
            Assert.Equal(new (int Start, int End)[] { (0, 6) }, encoding.Offsets);
            Assert.Equal("<mask>", tokenizer.Decode(encoding.Ids));
            Assert.Equal(string.Empty, tokenizer.Decode(encoding.Ids, skipSpecialTokens: true));
        }

        [Fact]
        public void CreateBpeRuntime_WithNormalizedAddedToken_MatchesNormalizedInput()
        {
            var root = JObject.Parse("""
            {
                "normalizer": {
                    "type": "Lowercase"
                },
                "added_tokens": [
                    {
                        "id": 99,
                        "content": "day",
                        "normalized": true,
                        "special": false
                    }
                ],
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "d": 1,
                        "a": 2,
                        "y": 3
                    },
                    "unk_token": "[UNK]",
                    "merges": []
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("DAY", addSpecialTokens: false);

            Assert.Equal(new[] { 99 }, encoding.Ids);
            Assert.Equal(new[] { "day" }, encoding.Tokens);
            Assert.Equal(new[] { 0 }, encoding.SpecialTokensMask);
            Assert.Equal(new (int Start, int End)[] { (0, 3) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithNonNormalizedAddedToken_DoesNotMatchNormalizedInput()
        {
            var root = JObject.Parse("""
            {
                "normalizer": {
                    "type": "Lowercase"
                },
                "added_tokens": [
                    {
                        "id": 99,
                        "content": "day",
                        "normalized": false,
                        "special": false
                    }
                ],
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "D": 1,
                        "A": 2,
                        "Y": 3
                    },
                    "unk_token": "[UNK]",
                    "merges": []
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("DAY", addSpecialTokens: false);

            Assert.Equal(new[] { 1, 2, 3 }, encoding.Ids);
            Assert.Equal(new[] { "D", "A", "Y" }, encoding.Tokens);
            Assert.Equal(new[] { 0, 0, 0 }, encoding.SpecialTokensMask);
            Assert.Equal(new (int Start, int End)[] { (0, 1), (1, 2), (2, 3) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithSingleWordAddedToken_DoesNotMatchInsideWord()
        {
            var root = JObject.Parse("""
            {
                "added_tokens": [
                    {
                        "id": 99,
                        "content": "cat",
                        "single_word": true,
                        "special": false
                    }
                ],
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "c": 1,
                        "a": 2,
                        "t": 3,
                        "b": 4,
                        "o": 5
                    },
                    "unk_token": "[UNK]",
                    "merges": []
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var exact = tokenizer.Encode("cat", addSpecialTokens: false);
            var embedded = tokenizer.Encode("bobcat", addSpecialTokens: false);

            Assert.Equal(new[] { 99 }, exact.Ids);
            Assert.Equal(new[] { 4, 5, 4, 1, 2, 3 }, embedded.Ids);
        }

        [Fact]
        public void CreateBpeRuntime_WithStripAddedToken_ConsumesAdjacentWhitespace()
        {
            var root = JObject.Parse("""
            {
                "added_tokens": [
                    {
                        "id": 99,
                        "content": "<mask>",
                        "lstrip": true,
                        "rstrip": true,
                        "special": true
                    }
                ],
                "decoder": {
                    "type": "BPEDecoder"
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "h": 1,
                        "i": 2,
                        "t": 3,
                        "e": 4
                    },
                    "unk_token": "[UNK]",
                    "merges": []
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hi <mask> te", addSpecialTokens: false);

            Assert.Equal(new[] { 1, 2, 99, 3, 4 }, encoding.Ids);
            Assert.Equal(new (int Start, int End)[] { (0, 1), (1, 2), (2, 10), (10, 11), (11, 12) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_WithTemplateProcessing_AddsBertStyleSpecialTokens()
        {
            var root = JObject.Parse("""
            {
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
                    "type": "BPEDecoder",
                    "suffix": "</w>"
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "h": 1,
                        "e": 2,
                        "l": 3,
                        "o</w>": 4,
                        "he": 5,
                        "hel": 6,
                        "hell": 7,
                        "hello</w>": 8
                    },
                    "unk_token": "[UNK]",
                    "end_of_word_suffix": "</w>",
                    "merges": ["h e", "he l", "hel l", "hell o</w>"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(new[] { 101, 8, 102 }, encoding.Ids);
            Assert.Equal(new[] { "[CLS]", "hello</w>", "[SEP]" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 0, 1 }, encoding.SpecialTokensMask);
        }

        [Fact]
        public void CreateBpeRuntime_WithSequencePostProcessor_ComposesSupportedProcessors()
        {
            var root = JObject.Parse("""
            {
                "pre_tokenizer": {
                    "type": "ByteLevel",
                    "add_prefix_space": false,
                    "trim_offsets": true,
                    "use_regex": true
                },
                "post_processor": {
                    "type": "Sequence",
                    "processors": [
                        {
                            "type": "ByteLevel",
                            "add_prefix_space": false,
                            "trim_offsets": true
                        },
                        {
                            "type": "BertProcessing",
                            "cls": ["[CLS]", 101],
                            "sep": ["[SEP]", 102]
                        }
                    ]
                },
                "decoder": {
                    "type": "ByteLevel",
                    "add_prefix_space": true,
                    "trim_offsets": true
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
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
                    },
                    "merges": ["h e", "he l", "hel l", "hell o", "Ġ t", "Ġt he", "Ġthe r", "Ġther e"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hello there");

            Assert.Equal(new[] { 101, 10, 14, 102 }, encoding.Ids);
            Assert.Equal(new[] { "[CLS]", "hello", "Ġthere", "[SEP]" }, encoding.Tokens);
            Assert.Equal(new[] { 1, 0, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal(new (int Start, int End)[] { (0, 0), (0, 5), (6, 11), (0, 0) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpeRuntime_EncodePair_UsesBertPostProcessorForSecondSequence()
        {
            var root = JObject.Parse("""
            {
                "post_processor": {
                    "type": "BertProcessing",
                    "cls": ["[CLS]", 101],
                    "sep": ["[SEP]", 102]
                },
                "decoder": {
                    "type": "BPEDecoder",
                    "suffix": "</w>"
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "[UNK]": 0,
                        "h": 1,
                        "e": 2,
                        "l": 3,
                        "o</w>": 4,
                        "w": 5,
                        "o": 6,
                        "r": 7,
                        "d</w>": 8,
                        "he": 9,
                        "hel": 10,
                        "hell": 11,
                        "hello</w>": 12,
                        "wo": 13,
                        "wor": 14,
                        "worl": 15,
                        "world</w>": 16
                    },
                    "unk_token": "[UNK]",
                    "end_of_word_suffix": "</w>",
                    "merges": ["h e", "he l", "hel l", "hell o</w>", "w o", "wo r", "wor l", "worl d</w>"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.EncodePair("hello", "world");

            Assert.Equal(new[] { 101, 12, 102, 16, 102 }, encoding.Ids);
            Assert.Equal(new[] { 0, 0, 0, 1, 1 }, encoding.TypeIds);
            Assert.Equal(new[] { 1, 0, 1, 0, 1 }, encoding.SpecialTokensMask);
            Assert.Equal((1, 2), encoding.SequenceRanges[0]);
            Assert.Equal((3, 4), encoding.SequenceRanges[1]);
        }

        [Fact]
        public void CreateBpeRuntime_WithByteLevelPrefixSpace_PrefixesFirstWord()
        {
            var root = JObject.Parse("""
            {
                "pre_tokenizer": {
                    "type": "ByteLevel",
                    "add_prefix_space": true,
                    "trim_offsets": true
                },
                "decoder": {
                    "type": "ByteLevel",
                    "add_prefix_space": true,
                    "trim_offsets": true
                },
                "model": {
                    "type": "BPE",
                    "vocab": {
                        "Ġ": 0,
                        "h": 1,
                        "e": 2,
                        "l": 3,
                        "o": 4,
                        "Ġh": 5,
                        "Ġhe": 6,
                        "Ġhel": 7,
                        "Ġhell": 8,
                        "Ġhello": 9
                    },
                    "merges": ["Ġ h", "Ġh e", "Ġhe l", "Ġhel l", "Ġhell o"]
                }
            }
            """);

            var tokenizer = TokenizerFactory.CreateBpeRuntime(root);
            var encoding = tokenizer.Encode("hello");

            Assert.Equal(new[] { 9 }, encoding.Ids);
            Assert.Equal(new[] { "Ġhello" }, encoding.Tokens);
            Assert.Equal(new (int Start, int End)[] { (0, 5) }, encoding.Offsets);
        }

        [Fact]
        public void CreateBpe_UsesUnknownTokenForMissingSymbols()
        {
            var directory = CreateTempDirectory();

            try
            {
                var vocabPath = Path.Combine(directory, "vocab.json");
                var mergesPath = Path.Combine(directory, "merges.txt");
                File.WriteAllText(vocabPath, "{\"[UNK]\":0,\"a</w>\":1}");
                File.WriteAllText(mergesPath, "#version: 0.2\n");

                var tokenizer = Tokenizer.CreateBpe(vocabPath, mergesPath, new BpeTokenizerOptions
                {
                    UnknownToken = "[UNK]",
                    EndOfWordSuffix = "</w>",
                });

                Assert.Equal(new[] { 0 }, tokenizer.EncodeToIds("z"));
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
    }
}