#nullable disable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Configuration class for Hugging Face tokenizers.
    /// This class represents the structure of tokenizer.json files used by Hugging Face models.
    /// </summary>
        public class TokenizerConfig
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("truncation")]
        public TruncationConfig Truncation { get; set; }

        [JsonProperty("padding")]
        public PaddingConfig Padding { get; set; }

        [JsonProperty("added_tokens")]
        public List<AddedToken> AddedTokens { get; set; } = new List<AddedToken>();

        [JsonProperty("normalizer")]
        public NormalizerConfig Normalizer { get; set; }

        [JsonProperty("pre_tokenizer")]
        public PreTokenizerConfig PreTokenizer { get; set; }

        [JsonProperty("post_processor")]
        public PostProcessorConfig PostProcessor { get; set; }

        [JsonProperty("decoder")]
        public DecoderConfig Decoder { get; set; }

        [JsonProperty("model")]
        public ModelConfig Model { get; set; }

        [JsonProperty("vocab_size")]
        public int VocabSize { get; set; }

        [JsonProperty("unk_token")]
        public string UnkToken { get; set; } = "[UNK]";

        [JsonProperty("sep_token")]
        public string SepToken { get; set; } = "[SEP]";

        [JsonProperty("pad_token")]
        public string PadToken { get; set; } = "[PAD]";

        [JsonProperty("cls_token")]
        public string ClsToken { get; set; } = "[CLS]";

        [JsonProperty("mask_token")]
        public string MaskToken { get; set; } = "[MASK]";

        [JsonProperty("bos_token")]
        public string BosToken { get; set; }

        [JsonProperty("eos_token")]
        public string EosToken { get; set; }

        [JsonProperty("clean_up_tokenization_spaces")]
        public bool CleanUpTokenizationSpaces { get; set; } = true;

        [JsonProperty("do_lower_case")]
        public bool DoLowerCase { get; set; } = false;

        [JsonProperty("strip_accents")]
        public bool? StripAccents { get; set; }

        [JsonProperty("tokenize_chinese_chars")]
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
            return JsonConvert.DeserializeObject<TokenizerConfig>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize tokenizer config from {filePath}");
        }
    }

        public class TruncationConfig
    {
        [JsonProperty("direction")]
        public string Direction { get; set; } = "Right";

        [JsonProperty("max_length")]
        public int MaxLength { get; set; } = 512;

        [JsonProperty("strategy")]
        public string Strategy { get; set; } = "LongestFirst";

        [JsonProperty("stride")]
        public int Stride { get; set; } = 0;
    }

        public class PaddingConfig
    {
        [JsonProperty("direction")]
        public string Direction { get; set; } = "Right";

        [JsonProperty("pad_id")]
        public int PadId { get; set; } = 0;

        [JsonProperty("pad_type_id")]
        public int PadTypeId { get; set; } = 0;

        [JsonProperty("pad_token")]
        public string PadToken { get; set; } = "[PAD]";
    }

        public class AddedToken
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("single_word")]
        public bool SingleWord { get; set; } = false;

        [JsonProperty("lstrip")]
        public bool Lstrip { get; set; } = false;

        [JsonProperty("rstrip")]
        public bool Rstrip { get; set; } = false;

        [JsonProperty("normalized")]
        public bool Normalized { get; set; } = false;

        [JsonProperty("special")]
        public bool Special { get; set; } = true;
    }

        public class NormalizerConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("lowercase")]
        public bool? Lowercase { get; set; }

        [JsonProperty("strip_accents")]
        public bool? StripAccents { get; set; }

        [JsonProperty("handle_chinese_chars")]
        public bool? HandleChineseChars { get; set; }

        [JsonProperty("normalizers")]
        public List<NormalizerConfig> Normalizers { get; set; } = new List<NormalizerConfig>();

        [JsonProperty("pattern")]
        public PatternConfig Pattern { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("clean_text")]
        public bool? CleanText { get; set; }

        [JsonProperty("left")]
        public bool? Left { get; set; }

        [JsonProperty("right")]
        public bool? Right { get; set; }

        [JsonProperty("precompiled_charsmap")]
        public string PrecompiledCharsmap { get; set; }

        [JsonProperty("prepend_string")]
        public string PrependString { get; set; }
    }

        public class PatternConfig
    {
        [JsonProperty("Regex")]
        public string Regex { get; set; }

        [JsonProperty("String")]
        public string String { get; set; }
    }

        public class PreTokenizerConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("pretokenizers")]
        public List<PreTokenizerConfig> PreTokenizers { get; set; } = new List<PreTokenizerConfig>();

        [JsonProperty("add_prefix_space")]
        public bool? AddPrefixSpace { get; set; }

        [JsonProperty("trim_offsets")]
        public bool? TrimOffsets { get; set; }

        [JsonProperty("use_regex")]
        public bool? UseRegex { get; set; }

        [JsonProperty("pattern")]
        public string Pattern { get; set; }
        
        [JsonProperty("pattern_config")]
        public PatternConfig PatternConfig { get; set; }

        // Metaspace pre-tokenizer properties
        [JsonProperty("replacement")]
        public string Replacement { get; set; }

        [JsonProperty("prepend_scheme")]
        public string PrependScheme { get; set; }

        [JsonProperty("split")]
        public bool? Split { get; set; }

        // Digits pre-tokenizer properties
        [JsonProperty("individual_digits")]
        public bool? IndividualDigits { get; set; }

        // Split/Punctuation pre-tokenizer properties
        [JsonProperty("behavior")]
        public string Behavior { get; set; }

        [JsonProperty("invert")]
        public bool? Invert { get; set; }

        // CharDelimiterSplit pre-tokenizer properties
        [JsonProperty("delimiter")]
        public string Delimiter { get; set; }

        // FixedLength pre-tokenizer properties
        [JsonProperty("length")]
        public int? Length { get; set; }
    }

        public class PostProcessorConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("sep")]
        public List<string> Sep { get; set; } = new List<string>();

        [JsonProperty("cls")]
        public List<string> Cls { get; set; } = new List<string>();

        [JsonProperty("trim_offsets")]
        public bool TrimOffsets { get; set; } = true;

        [JsonProperty("add_prefix_space")]
        public bool? AddPrefixSpace { get; set; }

        [JsonProperty("use_regex")]
        public bool? UseRegex { get; set; }

        [JsonProperty("processors")]
        public List<PostProcessorConfig> Processors { get; set; } = new List<PostProcessorConfig>();

        // Template processing properties
        [JsonProperty("single")]
        public JToken SingleTemplate { get; set; }

        [JsonProperty("pair")]
        public JToken PairTemplate { get; set; }

        [JsonProperty("special_tokens")]
        public Dictionary<string, SpecialTokenConfig> SpecialTokensDict { get; set; } = new Dictionary<string, SpecialTokenConfig>();
    }

        public class SpecialTokenConfig
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("ids")]
        public List<int> Ids { get; set; } = new List<int>();

        [JsonProperty("tokens")]
        public List<string> Tokens { get; set; } = new List<string>();
    }

        public class DecoderConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("decoders")]
        public List<DecoderConfig> Decoders { get; set; } = new List<DecoderConfig>();

        // BPE Decoder properties
        [JsonProperty("suffix")]
        public string Suffix { get; set; }

        // WordPiece Decoder properties
        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        // Common cleanup property for WordPiece and CTC
        [JsonProperty("cleanup")]
        public bool? Cleanup { get; set; }

        // CTC Decoder properties
        [JsonProperty("pad_token")]
        public string PadToken { get; set; }

        [JsonProperty("word_delimiter_token")]
        public string WordDelimiterToken { get; set; }

        // Metaspace Decoder properties
        [JsonProperty("replacement")]
        public string Replacement { get; set; }

        [JsonProperty("prepend_scheme")]
        public string PrependScheme { get; set; }

        [JsonProperty("split")]
        public bool? Split { get; set; }

        // ByteFallback Decoder properties (no specific properties beyond type)
        // No specific properties needed for ByteFallback as per Rust implementation
    }

        public class ModelConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("vocab")]
        public Dictionary<string, int> Vocab { get; set; } = new Dictionary<string, int>();

        [JsonProperty("merges")]
        public List<string> Merges { get; set; } = new List<string>();

        [JsonProperty("unk_token")]
        public string UnkToken { get; set; }

        [JsonProperty("continuing_subword_prefix")]
        public string ContinuingSubwordPrefix { get; set; }

        [JsonProperty("end_of_word_suffix")]
        public string EndOfWordSuffix { get; set; }

        [JsonProperty("fuse_unk")]
        public bool? FuseUnk { get; set; }

        [JsonProperty("dropout")]
        public float? Dropout { get; set; }

        [JsonProperty("byte_fallback")]
        public bool? ByteFallback { get; set; }

        [JsonProperty("ignore_merges")]
        public bool? IgnoreMerges { get; set; }

        [JsonProperty("split_on_whitespace_only")]
        public bool? SplitOnWhitespaceOnly { get; set; }

    }
}
