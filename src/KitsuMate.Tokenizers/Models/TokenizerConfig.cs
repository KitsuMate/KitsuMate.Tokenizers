using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Configuration class for Hugging Face tokenizers.
    /// This class represents the structure of tokenizer.json files used by Hugging Face models.
    /// </summary>
        public class TokenizerConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("truncation")]
        public TruncationConfig Truncation { get; set; }

        [JsonPropertyName("padding")]
        public PaddingConfig Padding { get; set; }

        [JsonPropertyName("added_tokens")]
        public List<AddedToken> AddedTokens { get; set; } = new List<AddedToken>();

        [JsonPropertyName("normalizer")]
        public NormalizerConfig Normalizer { get; set; }

        [JsonPropertyName("pre_tokenizer")]
        public PreTokenizerConfig PreTokenizer { get; set; }

        [JsonPropertyName("post_processor")]
        public PostProcessorConfig PostProcessor { get; set; }

        [JsonPropertyName("decoder")]
        public DecoderConfig Decoder { get; set; }

        [JsonPropertyName("model")]
        public ModelConfig Model { get; set; }

        [JsonPropertyName("vocab_size")]
        public int VocabSize { get; set; }

        [JsonPropertyName("unk_token")]
        public string UnkToken { get; set; } = "[UNK]";

        [JsonPropertyName("sep_token")]
        public string SepToken { get; set; } = "[SEP]";

        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; } = "[PAD]";

        [JsonPropertyName("cls_token")]
        public string ClsToken { get; set; } = "[CLS]";

        [JsonPropertyName("mask_token")]
        public string MaskToken { get; set; } = "[MASK]";

        [JsonPropertyName("bos_token")]
        public string BosToken { get; set; }

        [JsonPropertyName("eos_token")]
        public string EosToken { get; set; }

        [JsonPropertyName("clean_up_tokenization_spaces")]
        public bool CleanUpTokenizationSpaces { get; set; } = true;

        [JsonPropertyName("do_lower_case")]
        public bool DoLowerCase { get; set; } = false;

        [JsonPropertyName("strip_accents")]
        public bool? StripAccents { get; set; }

        [JsonPropertyName("tokenize_chinese_chars")]
        public bool TokenizeChineseChars { get; set; } = true;


        /// <summary>
        /// Creates a BERT-style tokenizer configuration.
        /// </summary>
        public static TokenizerConfig CreateBertConfig(int vocabSize = 30522)
        {
            return new TokenizerConfig
            {
                Version = "1.0",
                VocabSize = vocabSize,
                UnkToken = "[UNK]",
                SepToken = "[SEP]",
                PadToken = "[PAD]",
                ClsToken = "[CLS]",
                MaskToken = "[MASK]",
                DoLowerCase = true,
                TokenizeChineseChars = true,
                Model = new ModelConfig
                {
                    Type = "WordPiece",
                    UnkToken = "[UNK]",
                    ContinuingSubwordPrefix = "##",
                    FuseUnk = true
                },
                Normalizer = new NormalizerConfig
                {
                    Type = "BertNormalizer",
                    Lowercase = true,
                    StripAccents = null,
                    HandleChineseChars = true
                },
                PreTokenizer = new PreTokenizerConfig
                {
                    Type = "BertPreTokenizer"
                },
                PostProcessor = new PostProcessorConfig
                {
                    Type = "BertProcessing",
                    Sep = new List<string> { "[SEP]", "102" },
                    Cls = new List<string> { "[CLS]", "101" }
                },
                Decoder = new DecoderConfig
                {
                    Type = "WordPiece"
                }
            };
        }

        /// <summary>
        /// Creates a GPT-style tokenizer configuration.
        /// </summary>
        public static TokenizerConfig CreateGptConfig(int vocabSize = 50257)
        {
            return new TokenizerConfig
            {
                Version = "1.0",
                VocabSize = vocabSize,
                BosToken = "<|endoftext|>",
                EosToken = "<|endoftext|>",
                UnkToken = "<|endoftext|>",
                Model = new ModelConfig
                {
                    Type = "BPE",
                    Dropout = null,
                    UnkToken = null,
                    ContinuingSubwordPrefix = null,
                    EndOfWordSuffix = null,
                    FuseUnk = false
                },
                Normalizer = null,
                PreTokenizer = new PreTokenizerConfig
                {
                    Type = "ByteLevel",
                    AddPrefixSpace = false,
                    TrimOffsets = true,
                    UseRegex = true
                },
                PostProcessor = new PostProcessorConfig
                {
                    Type = "ByteLevel",
                    TrimOffsets = true
                },
                Decoder = new DecoderConfig
                {
                    Type = "ByteLevel"
                }
            };
        }

        /// <summary>
        /// Creates a RoBERTa-style tokenizer configuration.
        /// </summary>
        public static TokenizerConfig CreateRobertaConfig(int vocabSize = 50265)
        {
            return new TokenizerConfig
            {
                Version = "1.0",
                VocabSize = vocabSize,
                BosToken = "<s>",
                EosToken = "</s>",
                SepToken = "</s>",
                ClsToken = "<s>",
                UnkToken = "<unk>",
                PadToken = "<pad>",
                MaskToken = "<mask>",
                Model = new ModelConfig
                {
                    Type = "BPE",
                    Dropout = null,
                    UnkToken = null,
                    ContinuingSubwordPrefix = null,
                    EndOfWordSuffix = null,
                    FuseUnk = false
                },
                Normalizer = null,
                PreTokenizer = new PreTokenizerConfig
                {
                    Type = "ByteLevel",
                    AddPrefixSpace = true,
                    TrimOffsets = true
                },
                PostProcessor = new PostProcessorConfig
                {
                    Type = "RobertaProcessing",
                    Sep = new List<string> { "</s>", "2" },
                    Cls = new List<string> { "<s>", "0" },
                    TrimOffsets = false
                },
                Decoder = new DecoderConfig
                {
                    Type = "ByteLevel"
                }
            };
        }

        public static TokenizerConfig FromFile(string filePath)
        {
            var json = System.IO.File.ReadAllText(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<TokenizerConfig>(json) 
                ?? throw new InvalidOperationException($"Failed to deserialize tokenizer config from {filePath}");
        }
    }

        public class TruncationConfig
    {
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "Right";

        [JsonPropertyName("max_length")]
        public int MaxLength { get; set; } = 512;

        [JsonPropertyName("strategy")]
        public string Strategy { get; set; } = "LongestFirst";

        [JsonPropertyName("stride")]
        public int Stride { get; set; } = 0;
    }

        public class PaddingConfig
    {
        [JsonPropertyName("direction")]
        public string Direction { get; set; } = "Right";

        [JsonPropertyName("pad_id")]
        public int PadId { get; set; } = 0;

        [JsonPropertyName("pad_type_id")]
        public int PadTypeId { get; set; } = 0;

        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; } = "[PAD]";
    }

        public class AddedToken
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("single_word")]
        public bool SingleWord { get; set; } = false;

        [JsonPropertyName("lstrip")]
        public bool Lstrip { get; set; } = false;

        [JsonPropertyName("rstrip")]
        public bool Rstrip { get; set; } = false;

        [JsonPropertyName("normalized")]
        public bool Normalized { get; set; } = false;

        [JsonPropertyName("special")]
        public bool Special { get; set; } = true;
    }

        public class NormalizerConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("lowercase")]
        public bool? Lowercase { get; set; }

        [JsonPropertyName("strip_accents")]
        public bool? StripAccents { get; set; }

        [JsonPropertyName("handle_chinese_chars")]
        public bool? HandleChineseChars { get; set; }

        [JsonPropertyName("normalizers")]
        public List<NormalizerConfig> Normalizers { get; set; } = new List<NormalizerConfig>();

        [JsonPropertyName("pattern")]
        public PatternConfig Pattern { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("clean_text")]
        public bool? CleanText { get; set; }

        [JsonPropertyName("left")]
        public bool? Left { get; set; }

        [JsonPropertyName("right")]
        public bool? Right { get; set; }

        [JsonPropertyName("precompiled_charsmap")]
        public string PrecompiledCharsmap { get; set; }

        [JsonPropertyName("prepend_string")]
        public string PrependString { get; set; }
    }

        public class PatternConfig
    {
        [JsonPropertyName("Regex")]
        public string Regex { get; set; }

        [JsonPropertyName("String")]
        public string String { get; set; }
    }

        public class PreTokenizerConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("pretokenizers")]
        public List<PreTokenizerConfig> PreTokenizers { get; set; } = new List<PreTokenizerConfig>();

        [JsonPropertyName("add_prefix_space")]
        public bool? AddPrefixSpace { get; set; }

        [JsonPropertyName("trim_offsets")]
        public bool? TrimOffsets { get; set; }

        [JsonPropertyName("use_regex")]
        public bool? UseRegex { get; set; }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }
        
        [JsonPropertyName("pattern_config")]
        public PatternConfig PatternConfig { get; set; }

        // Metaspace pre-tokenizer properties
        [JsonPropertyName("replacement")]
        public string Replacement { get; set; }

        [JsonPropertyName("prepend_scheme")]
        public string PrependScheme { get; set; }

        [JsonPropertyName("split")]
        public bool? Split { get; set; }

        // Digits pre-tokenizer properties
        [JsonPropertyName("individual_digits")]
        public bool? IndividualDigits { get; set; }

        // Split/Punctuation pre-tokenizer properties
        [JsonPropertyName("behavior")]
        public string Behavior { get; set; }

        [JsonPropertyName("invert")]
        public bool? Invert { get; set; }

        // CharDelimiterSplit pre-tokenizer properties
        [JsonPropertyName("delimiter")]
        public string Delimiter { get; set; }

        // FixedLength pre-tokenizer properties
        [JsonPropertyName("length")]
        public int? Length { get; set; }
    }

        public class PostProcessorConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("sep")]
        public List<string> Sep { get; set; } = new List<string>();

        [JsonPropertyName("cls")]
        public List<string> Cls { get; set; } = new List<string>();

        [JsonPropertyName("trim_offsets")]
        public bool TrimOffsets { get; set; } = true;

        [JsonPropertyName("add_prefix_space")]
        public bool? AddPrefixSpace { get; set; }

        [JsonPropertyName("use_regex")]
        public bool? UseRegex { get; set; }

        [JsonPropertyName("processors")]
        public List<PostProcessorConfig> Processors { get; set; } = new List<PostProcessorConfig>();

        // Template processing properties
        [JsonPropertyName("single")]
        public JsonElement? SingleTemplate { get; set; }  // Can be string or complex object

        [JsonPropertyName("pair")]
        public JsonElement? PairTemplate { get; set; }  // Can be string or complex object

        [JsonPropertyName("special_tokens")]
        public Dictionary<string, SpecialTokenConfig> SpecialTokensDict { get; set; } = new Dictionary<string, SpecialTokenConfig>();
    }

        public class SpecialTokenConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("ids")]
        public List<int> Ids { get; set; } = new List<int>();

        [JsonPropertyName("tokens")]
        public List<string> Tokens { get; set; } = new List<string>();
    }

        public class DecoderConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("decoders")]
        public List<DecoderConfig> Decoders { get; set; } = new List<DecoderConfig>();

        // BPE Decoder properties
        [JsonPropertyName("suffix")]
        public string Suffix { get; set; }

        // WordPiece Decoder properties
        [JsonPropertyName("prefix")]
        public string Prefix { get; set; }

        // Common cleanup property for WordPiece and CTC
        [JsonPropertyName("cleanup")]
        public bool? Cleanup { get; set; }

        // CTC Decoder properties
        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; }

        [JsonPropertyName("word_delimiter_token")]
        public string WordDelimiterToken { get; set; }

        // Metaspace Decoder properties
        [JsonPropertyName("replacement")]
        public string Replacement { get; set; }

        [JsonPropertyName("prepend_scheme")]
        public string PrependScheme { get; set; }

        [JsonPropertyName("split")]
        public bool? Split { get; set; }

        // ByteFallback Decoder properties (no specific properties beyond type)
        // No specific properties needed for ByteFallback as per Rust implementation
    }

        public class ModelConfig
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("vocab")]
        public Dictionary<string, int> Vocab { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("merges")]
        public List<string> Merges { get; set; } = new List<string>();

        [JsonPropertyName("unk_token")]
        public string UnkToken { get; set; }

        [JsonPropertyName("continuing_subword_prefix")]
        public string ContinuingSubwordPrefix { get; set; }

        [JsonPropertyName("end_of_word_suffix")]
        public string EndOfWordSuffix { get; set; }

        [JsonPropertyName("fuse_unk")]
        public bool? FuseUnk { get; set; }

        [JsonPropertyName("dropout")]
        public float? Dropout { get; set; }

        [JsonPropertyName("byte_fallback")]
        public bool? ByteFallback { get; set; }

        [JsonPropertyName("ignore_merges")]
        public bool? IgnoreMerges { get; set; }

        [JsonPropertyName("split_on_whitespace_only")]
        public bool? SplitOnWhitespaceOnly { get; set; }

    }
}
