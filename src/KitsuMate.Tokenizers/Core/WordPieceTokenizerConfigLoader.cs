using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    internal static class WordPieceTokenizerConfigLoader
    {
        public static WordPieceTokenizerOptions CreateOptions(TokenizerJsonPipeline pipeline)
        {
            return CreateOptionsCore(
                pipeline.Root,
                pipeline.TokenizerConfigRoot,
                pipeline.NormalizerConfig,
                pipeline.PreTokenizerConfig,
                pipeline.PostProcessorConfig,
                pipeline.DecoderConfig);
        }

        public static WordPieceTokenizerOptions CreateOptions(JObject tokenizerJsonRoot, JObject? tokenizerConfigRoot = null)
        {
            return CreateOptionsCore(
                tokenizerJsonRoot,
                tokenizerConfigRoot,
                TokenizerJsonComponentFactory.ParseNormalizerConfig(tokenizerJsonRoot["normalizer"] as JObject),
                TokenizerJsonComponentFactory.ParsePreTokenizerConfig(tokenizerJsonRoot["pre_tokenizer"] as JObject),
                TokenizerJsonComponentFactory.ParsePostProcessorConfig(tokenizerJsonRoot["post_processor"] as JObject),
                TokenizerJsonComponentFactory.ParseDecoderConfig(tokenizerJsonRoot["decoder"] as JObject));
        }

        private static WordPieceTokenizerOptions CreateOptionsCore(
            JObject tokenizerJsonRoot,
            JObject? tokenizerConfigRoot,
            NormalizerConfig? normalizer,
            PreTokenizerConfig? preTokenizer,
            PostProcessorConfig? postProcessor,
            DecoderConfig? decoder)
        {
            var options = new WordPieceTokenizerOptions();

            var model = tokenizerJsonRoot["model"] as JObject;
            if (model != null)
            {
                options.ContinuingSubwordPrefix = model["continuing_subword_prefix"]?.Value<string>() ?? options.ContinuingSubwordPrefix;
                options.UnknownToken = model["unk_token"]?.Value<string>() ?? options.UnknownToken;
                options.MaxInputCharsPerWord = model["max_input_chars_per_word"]?.Value<int?>() ?? options.MaxInputCharsPerWord;
            }

            options.CleanUpTokenizationSpaces = tokenizerJsonRoot["clean_up_tokenization_spaces"]?.Value<bool?>() ?? options.CleanUpTokenizationSpaces;
            options.UnknownToken = tokenizerJsonRoot["unk_token"]?.Value<string>() ?? options.UnknownToken;
            options.ClassificationToken = tokenizerJsonRoot["cls_token"]?.Value<string>() ?? options.ClassificationToken;
            options.SeparatorToken = tokenizerJsonRoot["sep_token"]?.Value<string>() ?? options.SeparatorToken;
            options.PaddingToken = tokenizerJsonRoot["pad_token"]?.Value<string>() ?? options.PaddingToken;
            options.MaskToken = tokenizerJsonRoot["mask_token"]?.Value<string>() ?? options.MaskToken;

            if (normalizer != null)
            {
                var normalizerType = normalizer.Type?.ToLowerInvariant();
                if (normalizerType == "bert")
                {
                    options.LowerCaseBeforeTokenization = normalizer.Lowercase ?? options.LowerCaseBeforeTokenization;
                }
                else if (normalizerType == "lowercase")
                {
                    options.LowerCaseBeforeTokenization = true;
                }
            }

            if (preTokenizer != null)
            {
                var preTokenizerType = preTokenizer.Type?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(preTokenizerType))
                {
                    options.ApplyBasicTokenization = preTokenizerType is "bertpretokenizer" or "bert";
                }
            }

            if (decoder != null)
            {
                options.ContinuingSubwordPrefix = string.IsNullOrWhiteSpace(decoder.Prefix) ? options.ContinuingSubwordPrefix : decoder.Prefix;
                options.CleanUpTokenizationSpaces = decoder.Cleanup ?? options.CleanUpTokenizationSpaces;
            }

            if (postProcessor != null)
            {
                if (postProcessor.Cls.Count > 0)
                {
                    options.ClassificationToken = postProcessor.Cls[0];
                }

                if (postProcessor.Sep.Count > 0)
                {
                    options.SeparatorToken = postProcessor.Sep[0];
                }
            }

            if (tokenizerConfigRoot != null)
            {
                options.CleanUpTokenizationSpaces = tokenizerConfigRoot["clean_up_tokenization_spaces"]?.Value<bool?>() ?? options.CleanUpTokenizationSpaces;
                options.LowerCaseBeforeTokenization = tokenizerConfigRoot["do_lower_case"]?.Value<bool?>() ?? options.LowerCaseBeforeTokenization;
                options.UnknownToken = tokenizerConfigRoot["unk_token"]?.Value<string>() ?? options.UnknownToken;
                options.ClassificationToken = tokenizerConfigRoot["cls_token"]?.Value<string>() ?? options.ClassificationToken;
                options.SeparatorToken = tokenizerConfigRoot["sep_token"]?.Value<string>() ?? options.SeparatorToken;
                options.PaddingToken = tokenizerConfigRoot["pad_token"]?.Value<string>() ?? options.PaddingToken;
                options.MaskToken = tokenizerConfigRoot["mask_token"]?.Value<string>() ?? options.MaskToken;
            }

            return options;
        }
    }
}