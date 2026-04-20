using System;
using System.IO;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class WordPieceFactoryTests
    {
        [Fact]
        public void CreateWordPiece_EncodesKnownWordsAndSpecialTokens()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");
                var tokenizer = Tokenizer.CreateWordPiece(path);

                var ids = tokenizer.EncodeToIds("hello world");

                Assert.Equal(new[] { 2, 4, 5, 3 }, ids);
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
        public void CreateWordPiece_UsesUnknownTokenWhenWordCannotBeSplit()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\n");
                var tokenizer = Tokenizer.CreateWordPiece(path);

                var ids = tokenizer.EncodeToIds("unknown", addSpecialTokens: false);

                Assert.Equal(new[] { 1 }, ids);
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
        public void CreateWordPiece_DecodesWordPieces()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");
                var tokenizer = Tokenizer.CreateWordPiece(path);

                var decoded = tokenizer.Decode(new[] { 4, 5, 6 });

                Assert.Equal("hello worlds", decoded);
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
        public void DefaultFactory_CreateFromTokenizerJson_UsesJsonVocabularyAndOptions()
        {
            var root = JObject.Parse("""
            {
                            "clean_up_tokenization_spaces": false,
                            "cls_token": "[BOS]",
                            "sep_token": "[EOS]",
                            "mask_token": "[MASK2]",
              "normalizer": {
                "type": "bert",
                "lowercase": true
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

            var tokenizer = TokenizerFactory.CreateWordPieceRuntime(root);
            var ids = tokenizer.EncodeToIds("HELLO world");

            Assert.Equal(new[] { 1, 3, 4, 2 }, ids);
            Assert.Equal("hello world", tokenizer.Decode(new[] { 3, 4 }));
        }

        [Fact]
        public void DefaultFactory_CreateWordPieceRuntime_UsesSiblingTokenizerConfigOverrides()
        {
            var root = JObject.Parse("""
            {
                "model": {
                    "unk_token": "[UNK]",
                    "continuing_subword_prefix": "##",
                    "vocab": {
                        "[UNK]": 0,
                        "[CLS]": 1,
                        "[SEP]": 2,
                        "hello": 3,
                        "world": 4
                    }
                }
            }
            """);
            var tokenizerConfig = JObject.Parse("""
            {
                "do_lower_case": true,
                "clean_up_tokenization_spaces": false,
                "cls_token": "[CLS]",
                "sep_token": "[SEP]"
            }
            """);

            var tokenizer = TokenizerFactory.CreateWordPieceRuntime(root, tokenizerConfig);
            var ids = tokenizer.EncodeToIds("HELLO world");

            Assert.Equal(new[] { 1, 3, 4, 2 }, ids);
        }

        [Fact]
        public void CreateWordPiece_EncodePair_AddsSequenceMetadataAndSpecialTokens()
        {
            var path = Path.GetTempFileName();

            try
            {
                File.WriteAllText(path, "[PAD]\n[UNK]\n[CLS]\n[SEP]\nhello\nworld\n##s\n");
                var tokenizer = Tokenizer.CreateWordPiece(path);

                var encoding = tokenizer.EncodePair("hello", "world");

                Assert.Equal(new[] { 2, 4, 3, 5, 3 }, encoding.Ids);
                Assert.Equal(new[] { 0, 0, 0, 1, 1 }, encoding.TypeIds);
                Assert.Equal(new[] { 1, 0, 1, 0, 1 }, encoding.SpecialTokensMask);
                Assert.Equal((1, 2), encoding.SequenceRanges[0]);
                Assert.Equal((3, 4), encoding.SequenceRanges[1]);
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