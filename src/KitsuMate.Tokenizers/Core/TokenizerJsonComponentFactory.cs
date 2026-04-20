using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using KitsuMate.Tokenizers.Decoders;
using KitsuMate.Tokenizers.Normalizers;
using KitsuMate.Tokenizers.PostProcessors;
using KitsuMate.Tokenizers.PreTokenizers;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Shared native factory for reconstructing tokenizer components from tokenizer.json.
    /// This is the first extraction step toward Rust-shaped per-instance pipeline assembly.
    /// </summary>
    internal static class TokenizerJsonComponentFactory
    {
        internal static INormalizer? CreateNormalizer(JObject? normalizer)
        {
            var config = ParseNormalizerConfig(normalizer);
            return config == null ? null : CreateNativeNormalizer(config);
        }

        private static INormalizer CreateNativeNormalizer(NormalizerConfig config)
        {
            switch (config.Type?.ToLowerInvariant())
            {
                case "bert":
                case "bertnormalizer":
                    return new BertNormalizer(config);
                case "nfd":
                    return new UnicodeNormalizer(NormalizationForm.FormD);
                case "nfc":
                    return new UnicodeNormalizer(NormalizationForm.FormC);
                case "nfkd":
                    return new UnicodeNormalizer(NormalizationForm.FormKD);
                case "nfkc":
                    return new UnicodeNormalizer(NormalizationForm.FormKC);
                case "lowercase":
                    return new LowercaseNormalizer();
                case "stripaccents":
                    return new StripAccentsNormalizer();
                case "replace":
                    return new ReplaceNormalizer(config);
                case "sequence":
                    return new SequenceNormalizer(config, CreateNativeNormalizer);
                case "nmt":
                    return new NmtNormalizer();
                case "strip":
                    return new StripNormalizer(config);
                case "precompiled":
                    return new PrecompiledNormalizer(config);
                case "prepend":
                    return new PrependNormalizer(config);
                case "bytelevel":
                    return new ByteLevelNormalizer();
                default:
                    return new DefaultNormalizer(config);
            }
        }

        internal static IPreTokenizer? CreatePreTokenizer(JObject? preTokenizer)
        {
            var config = ParsePreTokenizerConfig(preTokenizer);
            return config == null ? null : CreateNativePreTokenizer(config);
        }

        private static IPreTokenizer CreateNativePreTokenizer(PreTokenizerConfig config)
        {
            switch (config.Type?.ToLowerInvariant())
            {
                case "berttokenizer":
                case "bertpretokenizer":
                case "bert":
                    return new BertPreTokenizer();
                case "bytelevel":
                case "byte_level":
                    return new ByteLevelPreTokenizer(config);
                case "chardelimitersplit":
                case "char_delimiter_split":
                    return new CharDelimiterSplitPreTokenizer(config);
                case "digits":
                    return new DigitsPreTokenizer(config);
                case "metaspace":
                    return new MetaspacePreTokenizer(config);
                case "punctuation":
                    return new PunctuationPreTokenizer(config);
                case "sequence":
                    return new SequencePreTokenizer(config, CreateNativePreTokenizer);
                case "split":
                    return new SplitPreTokenizer(config);
                case "unicodescripts":
                case "unicode_scripts":
                    return new UnicodeScriptsPreTokenizer();
                case "whitespace":
                    return new WhitespacePreTokenizer();
                case "whitespacesplit":
                case "whitespace_split":
                    return new WhitespaceSplitPreTokenizer();
                case "fixedlength":
                case "fixed_length":
                    return new FixedLengthPreTokenizer(config);
                default:
                    return new DefaultPreTokenizer();
            }
        }

        internal static IDecoder? CreateDecoder(JObject? decoder)
        {
            var config = ParseDecoderConfig(decoder);
            return config == null ? null : CreateNativeDecoder(config);
        }

        private static IDecoder CreateNativeDecoder(DecoderConfig config)
        {
            switch (config.Type?.ToLowerInvariant())
            {
                case "bpe":
                case "bpedecoder":
                    return new BpeDecoder(config.Suffix ?? "</w>");
                case "bytefallback":
                    return new ByteFallbackDecoder();
                case "bytelevel":
                    return new ByteLevelDecoder();
                case "ctc":
                    return new CtcDecoder(config.PadToken ?? "<pad>", config.WordDelimiterToken ?? "|", config.Cleanup ?? true);
                case "metaspace":
                    return new MetaspaceDecoder(config.Replacement ?? " ", config.PrependScheme ?? "always");
                case "wordpiece":
                    return new WordPieceDecoder(config.Prefix ?? "##", config.Cleanup ?? true);
                case "sequence":
                    return new SequenceDecoder(config.Decoders, CreateNativeDecoder);
                default:
                    return new DefaultDecoder();
            }
        }

        internal static IPostProcessor? CreatePostProcessor(PostProcessorConfig? config, PreTokenizerConfig? preTokenizer = null)
        {
            if (config == null)
            {
                return null;
            }

            switch (config.Type?.ToLowerInvariant())
            {
                case "bertprocessing":
                    return new BertPostProcessor(
                        ReadSpecialToken(config.Cls, "[CLS]", 101),
                        ReadSpecialToken(config.Sep, "[SEP]", 102));
                case "robertaprocessing":
                    return new RobertaPostProcessor(
                        ReadSpecialToken(config.Cls, "<s>", 0),
                        ReadSpecialToken(config.Sep, "</s>", 2),
                        config.TrimOffsets,
                        config.AddPrefixSpace ?? true);
                case "bytelevel":
                    return new ByteLevelPostProcessor(
                        config.AddPrefixSpace ?? true,
                        config.TrimOffsets,
                        config.UseRegex ?? preTokenizer?.UseRegex ?? true);
                case "templateprocessing":
                    if (!TryParseTemplateProcessing(config, out var single, out var pair, out var specialTokens))
                    {
                        return null;
                    }

                    if (TryResolveTemplateTokens(single, specialTokens, out var startToken, out var endToken, out var isRoberta) && isRoberta)
                    {
                        return new RobertaPostProcessor(startToken, endToken, config.TrimOffsets, config.AddPrefixSpace ?? true);
                    }

                    return new TemplatePostProcessor(single, pair, specialTokens);
                case "sequence":
                    return new SequencePostProcessor(
                        config.Processors
                            .Select(processor => CreatePostProcessor(processor, preTokenizer))
                            .Where(processor => processor != null)
                            .Cast<IPostProcessor>()
                            .ToList());
                default:
                    return null;
            }
        }

        internal static NormalizerConfig? ParseNormalizerConfig(JObject? element)
        {
            if (element == null)
            {
                return null;
            }

            var config = new NormalizerConfig
            {
                Type = element["type"]?.Value<string>() ?? string.Empty
            };

            if (element["lowercase"]?.Type == JTokenType.Boolean)
            {
                config.Lowercase = element["lowercase"]!.Value<bool>();
            }

            if (element["strip_accents"]?.Type == JTokenType.Boolean)
            {
                config.StripAccents = element["strip_accents"]!.Value<bool>();
            }

            if (element["clean_text"]?.Type == JTokenType.Boolean)
            {
                config.CleanText = element["clean_text"]!.Value<bool>();
            }

            if (element["precompiled_charsmap"]?.Type == JTokenType.String)
            {
                config.PrecompiledCharsmap = element["precompiled_charsmap"]!.Value<string>() ?? string.Empty;
            }

            if (element["handle_chinese_chars"]?.Type == JTokenType.Boolean)
            {
                config.HandleChineseChars = element["handle_chinese_chars"]!.Value<bool>();
            }

            if (element["normalizers"] is JArray normalizers)
            {
                config.Normalizers = normalizers
                    .OfType<JObject>()
                    .Select(ParseNormalizerConfig)
                    .Where(item => item != null)
                    .Cast<NormalizerConfig>()
                    .ToList();
            }

            return config;
        }

        internal static PreTokenizerConfig? ParsePreTokenizerConfig(JObject? element)
        {
            if (element == null)
            {
                return null;
            }

            var config = new PreTokenizerConfig
            {
                Type = element["type"]?.Value<string>() ?? string.Empty,
                AddPrefixSpace = ReadNullableBool(element, "add_prefix_space"),
                TrimOffsets = ReadNullableBool(element, "trim_offsets"),
                UseRegex = ReadNullableBool(element, "use_regex"),
                Pattern = ReadPatternString(element["pattern"]) ?? string.Empty,
                PatternConfig = ParsePatternConfig(element["pattern"] ?? element["pattern_config"]),
                Replacement = element["replacement"]?.Value<string>() ?? string.Empty,
                PrependScheme = element["prepend_scheme"]?.Value<string>() ?? string.Empty,
                Split = ReadNullableBool(element, "split"),
                IndividualDigits = ReadNullableBool(element, "individual_digits"),
                Behavior = element["behavior"]?.Value<string>() ?? string.Empty,
                Invert = ReadNullableBool(element, "invert"),
                Delimiter = element["delimiter"]?.Value<string>() ?? string.Empty,
                Length = element["length"]?.Value<int?>(),
            };

            if (element["pretokenizers"] is JArray preTokenizers)
            {
                config.PreTokenizers = preTokenizers
                    .OfType<JObject>()
                    .Select(ParsePreTokenizerConfig)
                    .Where(item => item != null)
                    .Cast<PreTokenizerConfig>()
                    .ToList();
            }

            return config;
        }

        internal static DecoderConfig? ParseDecoderConfig(JObject? element)
        {
            if (element == null)
            {
                return null;
            }

            var config = new DecoderConfig
            {
                Type = element["type"]?.Value<string>() ?? string.Empty,
                Suffix = element["suffix"]?.Value<string>() ?? string.Empty,
                Prefix = element["prefix"]?.Value<string>() ?? string.Empty,
                Cleanup = ReadNullableBool(element, "cleanup"),
                PadToken = element["pad_token"]?.Value<string>() ?? string.Empty,
                WordDelimiterToken = element["word_delimiter_token"]?.Value<string>() ?? string.Empty,
                Replacement = element["replacement"]?.Value<string>() ?? string.Empty,
                PrependScheme = element["prepend_scheme"]?.Value<string>() ?? string.Empty,
                Split = ReadNullableBool(element, "split"),
            };

            if (element["decoders"] is JArray decoders)
            {
                config.Decoders = decoders
                    .OfType<JObject>()
                    .Select(ParseDecoderConfig)
                    .Where(item => item != null)
                    .Cast<DecoderConfig>()
                    .ToList();
            }

            return config;
        }

        internal static PostProcessorConfig? ParsePostProcessorConfig(JObject? element)
        {
            if (element == null)
            {
                return null;
            }

            var config = new PostProcessorConfig
            {
                Type = element["type"]?.Value<string>() ?? string.Empty,
                Sep = ParseTokenArray(element["sep"] as JArray),
                Cls = ParseTokenArray(element["cls"] as JArray),
                TrimOffsets = element["trim_offsets"]?.Value<bool?>() ?? true,
                AddPrefixSpace = ReadNullableBool(element, "add_prefix_space"),
                UseRegex = ReadNullableBool(element, "use_regex"),
                SingleTemplate = ParseJsonElement(element["single"]),
                PairTemplate = ParseJsonElement(element["pair"]),
                SpecialTokensDict = ParseSpecialTokensDict(element["special_tokens"] as JObject),
            };

            if (element["processors"] is JArray processors)
            {
                config.Processors = processors
                    .OfType<JObject>()
                    .Select(ParsePostProcessorConfig)
                    .Where(item => item != null)
                    .Cast<PostProcessorConfig>()
                    .ToList();
            }

            return config;
        }

        internal static bool UsesByteLevel(PreTokenizerConfig? preTokenizer, DecoderConfig? decoder)
        {
            return HasPreTokenizerType(preTokenizer, "ByteLevel") || HasDecoderType(decoder, "ByteLevel");
        }

        internal static bool IsSplitOnSpaceMergedWithPrevious(PreTokenizerConfig? preTokenizer)
        {
            return preTokenizer != null &&
                   string.Equals(preTokenizer.Type, "Split", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(preTokenizer.PatternConfig?.String ?? preTokenizer.Pattern, " ", StringComparison.Ordinal) &&
                   string.Equals(preTokenizer.Behavior, "MergedWithPrevious", StringComparison.OrdinalIgnoreCase) &&
                   (preTokenizer.Invert ?? false) == false;
        }

        internal static IEnumerable<(string Token, int Id)> EnumerateSpecialTokens(PostProcessorConfig? config)
        {
            if (config == null)
            {
                yield break;
            }

            switch (config.Type?.ToLowerInvariant())
            {
                case "bertprocessing":
                    yield return ReadSpecialToken(config.Cls, "[CLS]", 101);
                    yield return ReadSpecialToken(config.Sep, "[SEP]", 102);
                    yield break;
                case "robertaprocessing":
                    yield return ReadSpecialToken(config.Cls, "<s>", 0);
                    yield return ReadSpecialToken(config.Sep, "</s>", 2);
                    yield break;
                case "templateprocessing":
                    if (TryParseTemplateProcessing(config, out _, out _, out var specialTokens))
                    {
                        foreach (var token in EnumerateSpecialTokens(specialTokens))
                        {
                            yield return token;
                        }
                    }

                    yield break;
                case "sequence":
                    foreach (var processor in config.Processors)
                    {
                        foreach (var token in EnumerateSpecialTokens(processor))
                        {
                            yield return token;
                        }
                    }

                    yield break;
            }
        }

        internal static IPostProcessor? CreateTemplatePostProcessor(JObject? postProcessor)
        {
            if (!TryParseTemplateProcessing(postProcessor, out var single, out var pair, out var specialTokens))
            {
                return null;
            }

            return new TemplatePostProcessor(single, pair, specialTokens);
        }

        internal static bool TryParseTemplateProcessing(
            JObject? postProcessor,
            out Template single,
            out Template pair,
            out Tokens specialTokens)
        {
            single = new Template();
            pair = new Template();
            specialTokens = new Tokens();

            if (postProcessor == null)
            {
                return false;
            }

            var type = postProcessor["type"]?.Value<string>();
            if (!string.Equals(type, "TemplateProcessing", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (postProcessor["single"] is not JArray singleTemplate ||
                postProcessor["special_tokens"] is not JObject specialTokensRoot)
            {
                return false;
            }

            single = ParseTemplate(singleTemplate);
            pair = postProcessor["pair"] is JArray pairTemplate ? ParseTemplate(pairTemplate) : new Template(single);
            specialTokens = ParseSpecialTokens(specialTokensRoot);
            return true;
        }

        internal static bool TryParseTemplateProcessing(
            PostProcessorConfig config,
            out Template single,
            out Template pair,
            out Tokens specialTokens)
        {
            single = new Template();
            pair = new Template();
            specialTokens = new Tokens();

            if (!string.Equals(config.Type, "TemplateProcessing", StringComparison.OrdinalIgnoreCase) ||
                config.SingleTemplate == null ||
                config.SingleTemplate.Value.ValueKind != JsonValueKind.Array ||
                config.SpecialTokensDict == null ||
                config.SpecialTokensDict.Count == 0)
            {
                return false;
            }

            single = ParseTemplate(config.SingleTemplate.Value);
            pair = config.PairTemplate != null && config.PairTemplate.Value.ValueKind == JsonValueKind.Array
                ? ParseTemplate(config.PairTemplate.Value)
                : new Template(single);
            specialTokens = ParseSpecialTokens(config.SpecialTokensDict);
            return true;
        }

        internal static bool TryResolveTemplateTokens(Template singleTemplate, Tokens specialTokens, out (string Token, int Id) startToken, out (string Token, int Id) endToken, out bool isRoberta)
        {
            startToken = default;
            endToken = default;
            isRoberta = false;

            string? firstTokenId = null;
            string? lastTokenId = null;
            foreach (var piece in singleTemplate)
            {
                if (string.IsNullOrWhiteSpace(piece.SpecialTokenId))
                {
                    continue;
                }

                firstTokenId ??= piece.SpecialTokenId;
                lastTokenId = piece.SpecialTokenId;
            }

            if (string.IsNullOrEmpty(firstTokenId) || string.IsNullOrEmpty(lastTokenId))
            {
                return false;
            }

            if (!TryReadTemplateSpecialToken(specialTokens, firstTokenId!, out startToken) ||
                !TryReadTemplateSpecialToken(specialTokens, lastTokenId!, out endToken))
            {
                return false;
            }

            isRoberta = string.Equals(startToken.Token, "<s>", StringComparison.Ordinal) &&
                        string.Equals(endToken.Token, "</s>", StringComparison.Ordinal);
            return true;
        }

        internal static IEnumerable<(string Token, int Id)> EnumerateSpecialTokens(Tokens specialTokens)
        {
            foreach (var token in specialTokens.Values)
            {
                for (var index = 0; index < Math.Min(token.Ids.Count, token.Tokens.Count); index++)
                {
                    yield return (token.Tokens[index], token.Ids[index]);
                }
            }
        }

        private static Template ParseTemplate(JArray templateArray)
        {
            var template = new Template();
            foreach (var entry in templateArray.OfType<JObject>())
            {
                if (entry["Sequence"] is JObject sequence)
                {
                    var id = sequence["id"]?.Value<string>();
                    var typeId = sequence["type_id"]?.Value<int?>() ?? 0;
                    template.Add(Piece.FromString($"${id}:{typeId}"));
                    continue;
                }

                if (entry["SpecialToken"] is JObject specialToken)
                {
                    var id = specialToken["id"]?.Value<string>();
                    var typeId = specialToken["type_id"]?.Value<int?>() ?? 0;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        template.Add(Piece.FromString($"{id}:{typeId}"));
                    }
                }
            }

            return template;
        }

        private static Template ParseTemplate(JsonElement templateArray)
        {
            var template = new Template();
            foreach (var entry in templateArray.EnumerateArray())
            {
                if (entry.TryGetProperty("Sequence", out var sequence))
                {
                    var id = sequence.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                    var typeId = sequence.TryGetProperty("type_id", out var typeIdProperty) && typeIdProperty.TryGetInt32(out var parsedTypeId)
                        ? parsedTypeId
                        : 0;
                    template.Add(Piece.FromString($"${id}:{typeId}"));
                    continue;
                }

                if (entry.TryGetProperty("SpecialToken", out var specialToken))
                {
                    var id = specialToken.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                    var typeId = specialToken.TryGetProperty("type_id", out var typeIdProperty) && typeIdProperty.TryGetInt32(out var parsedTypeId)
                        ? parsedTypeId
                        : 0;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        template.Add(Piece.FromString($"{id}:{typeId}"));
                    }
                }
            }

            return template;
        }

        private static Tokens ParseSpecialTokens(JObject specialTokensRoot)
        {
            var tokens = new List<SpecialToken>();
            foreach (var property in specialTokensRoot.Properties())
            {
                if (property.Value is not JObject tokenObject)
                {
                    continue;
                }

                var id = tokenObject["id"]?.Value<string>() ?? property.Name;
                var ids = tokenObject["ids"] is JArray idsArray
                    ? idsArray.Values<int>().ToList()
                    : new List<int>();
                var tokenValues = tokenObject["tokens"] is JArray tokensArray
                    ? tokensArray.Values<string>().Where(value => value != null).Cast<string>().ToList()
                    : new List<string>();

                if (ids.Count == 0 || tokenValues.Count == 0)
                {
                    continue;
                }

                tokens.Add(new SpecialToken(id, ids, tokenValues));
            }

            return new Tokens(tokens);
        }

        private static Tokens ParseSpecialTokens(Dictionary<string, SpecialTokenConfig> specialTokensRoot)
        {
            var tokens = new List<SpecialToken>();
            foreach (var property in specialTokensRoot)
            {
                var tokenObject = property.Value;
                if (tokenObject == null)
                {
                    continue;
                }

                var id = tokenObject.Id ?? property.Key;
                var ids = tokenObject.Ids?.ToList() ?? new List<int>();
                var tokenValues = tokenObject.Tokens?.Where(value => value != null).Cast<string>().ToList() ?? new List<string>();

                if (ids.Count == 0 || tokenValues.Count == 0)
                {
                    continue;
                }

                tokens.Add(new SpecialToken(id, ids, tokenValues));
            }

            return new Tokens(tokens);
        }

        private static bool TryReadTemplateSpecialToken(Tokens specialTokens, string tokenId, out (string Token, int Id) token)
        {
            token = default;
            if (!specialTokens.TryGetValue(tokenId, out var tokenConfig) || tokenConfig.Ids.Count == 0 || tokenConfig.Tokens.Count == 0)
            {
                return false;
            }

            token = (tokenConfig.Tokens[0], tokenConfig.Ids[0]);
            return true;
        }

        private static PatternConfig? ParsePatternConfig(JToken? token)
        {
            return token switch
            {
                JObject obj => new PatternConfig
                {
                    Regex = obj["Regex"]?.Value<string>() ?? string.Empty,
                    String = obj["String"]?.Value<string>() ?? string.Empty
                },
                JValue value when value.Type == JTokenType.String => new PatternConfig
                {
                    String = value.Value<string>() ?? string.Empty
                },
                _ => null,
            };
        }

        private static JsonElement? ParseJsonElement(JToken? token)
        {
            if (token == null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(token.ToString());
            return document.RootElement.Clone();
        }

        private static Dictionary<string, SpecialTokenConfig> ParseSpecialTokensDict(JObject? specialTokensRoot)
        {
            var tokens = new Dictionary<string, SpecialTokenConfig>(StringComparer.Ordinal);
            if (specialTokensRoot == null)
            {
                return tokens;
            }

            foreach (var property in specialTokensRoot.Properties())
            {
                if (property.Value is not JObject tokenObject)
                {
                    continue;
                }

                tokens[property.Name] = new SpecialTokenConfig
                {
                    Id = tokenObject["id"]?.Value<string>() ?? string.Empty,
                    Ids = tokenObject["ids"] is JArray idsArray ? idsArray.Values<int>().ToList() : new List<int>(),
                    Tokens = tokenObject["tokens"] is JArray tokensArray
                        ? tokensArray.Values<string>().Where(value => value != null).Cast<string>().ToList()
                        : new List<string>(),
                };
            }

            return tokens;
        }

        private static List<string> ParseTokenArray(JArray? tokenArray)
        {
            if (tokenArray == null)
            {
                return new List<string>();
            }

            return tokenArray.Select(token => token.Type == JTokenType.String ? token.Value<string>() : token.ToString())
                .Where(value => !string.IsNullOrEmpty(value))
                .Cast<string>()
                .ToList();
        }

        private static (string Token, int Id) ReadSpecialToken(IReadOnlyList<string> tokenArray, string defaultToken, int defaultId)
        {
            if (tokenArray.Count >= 2)
            {
                if (int.TryParse(tokenArray[1], out var trailingId))
                {
                    return (tokenArray[0], trailingId);
                }

                if (int.TryParse(tokenArray[0], out var leadingId))
                {
                    return (tokenArray[1], leadingId);
                }
            }

            return (defaultToken, defaultId);
        }

        private static string? ReadPatternString(JToken? token)
        {
            return token switch
            {
                JObject obj => obj["String"]?.Value<string>() ?? obj["Regex"]?.Value<string>(),
                JValue value when value.Type == JTokenType.String => value.Value<string>(),
                _ => null,
            };
        }

        private static bool? ReadNullableBool(JObject element, string propertyName)
        {
            return element[propertyName]?.Type == JTokenType.Boolean ? element[propertyName]!.Value<bool>() : null;
        }

        private static bool HasPreTokenizerType(PreTokenizerConfig? config, string expectedType)
        {
            if (config == null)
            {
                return false;
            }

            if (string.Equals(config.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return config.PreTokenizers.Any(child => HasPreTokenizerType(child, expectedType));
        }

        private static bool HasDecoderType(DecoderConfig? config, string expectedType)
        {
            if (config == null)
            {
                return false;
            }

            if (string.Equals(config.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return config.Decoders.Any(child => HasDecoderType(child, expectedType));
        }
    }
}