using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Parallel loader that discovers tokenizer artifacts without relying on the legacy runtime.
    /// </summary>
    public static class TokenizerLoader
    {
        public static ITokenizer FromLocal(string modelDirectory, TokenizerLoadOptions? loadOptions = null, TokenizerFactory? factory = null, ITokenizerJsonSerializer? jsonSerializer = null)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory))
            {
                throw new ArgumentException("Model directory cannot be null or empty.", nameof(modelDirectory));
            }

            if (!Directory.Exists(modelDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: {modelDirectory}");
            }

            loadOptions ??= new TokenizerLoadOptions();
            factory ??= new TokenizerFactory(jsonSerializer);
            jsonSerializer ??= new DefaultTokenizerJsonSerializer();
            var tokenizerConfigRoot = LoadTokenizerConfigRoot(modelDirectory, jsonSerializer);
            TokenizerNotSupportedException? deferredUnsupportedBackend = null;

            var tokenizerJsonPath = Path.Combine(modelDirectory, "tokenizer.json");
            if (File.Exists(tokenizerJsonPath))
            {
                try
                {
                    var tokenizerFromJson = factory.CreateFromTokenizerJson(tokenizerJsonPath);
                    return tokenizerFromJson;
                }
                catch (TokenizerNotSupportedException ex)
                {
                    if (!loadOptions.FallbackToOtherVariants)
                    {
                        throw;
                    }

                    deferredUnsupportedBackend = ex;
                }
                catch (NotSupportedException)
                {
                    if (!loadOptions.FallbackToOtherVariants)
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    if (!loadOptions.FallbackToOtherVariants)
                    {
                        throw;
                    }
                }
            }

            var sentencePieceModel = Directory.GetFiles(modelDirectory, "*.model").Length > 0
                ? Directory.GetFiles(modelDirectory, "*.model")[0]
                : null;
            if (sentencePieceModel != null)
            {
                var backendType = DetectSentencePieceBackendType(modelDirectory, tokenizerConfigRoot);
                var applyIdOffset = ShouldApplySentencePieceIdOffset(modelDirectory, tokenizerConfigRoot);
                using var stream = File.OpenRead(sentencePieceModel);
                return factory.CreateSentencePiece(stream, backendType, applyIdOffset);
            }

            var vocabTxtPath = Path.Combine(modelDirectory, "vocab.txt");
            if (File.Exists(vocabTxtPath))
            {
                return factory.CreateWordPiece(vocabTxtPath, CreateWordPieceOptions(tokenizerConfigRoot));
            }

            var vocabJsonPath = Path.Combine(modelDirectory, "vocab.json");
            var mergesPath = Path.Combine(modelDirectory, "merges.txt");
            if (File.Exists(vocabJsonPath) && File.Exists(mergesPath))
            {
                return factory.CreateBpe(vocabJsonPath, mergesPath, CreateBpeOptions(tokenizerConfigRoot));
            }

            var tiktokenFiles = Directory.GetFiles(modelDirectory, "*.tiktoken");
            if (tiktokenFiles.Length > 0)
            {
                return factory.CreateTiktoken(tiktokenFiles[0]);
            }

            if (deferredUnsupportedBackend != null)
            {
                throw deferredUnsupportedBackend;
            }

            throw new InvalidOperationException(
                $"Could not detect tokenizer type in directory: {modelDirectory}. Supported files: tokenizer.json, vocab.txt, vocab.json + merges.txt, *.model");
        }

        private static TokenizerBackendType DetectSentencePieceBackendType(string modelDirectory, JObject? tokenizerConfigRoot)
        {
            var modelPath = Directory.GetFiles(modelDirectory, "*.model").Length > 0
                ? Directory.GetFiles(modelDirectory, "*.model")[0]
                : null;
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                try
                {
                    var inspectedType = SentencePieceModelInspector.DetectBackendType(modelPath);
                    if (inspectedType != TokenizerBackendType.Unknown)
                    {
                        return inspectedType;
                    }
                }
                catch
                {
                    // Ignore malformed SentencePiece models and fall back to sibling config heuristics.
                }
            }

            if (tokenizerConfigRoot == null)
            {
                return TokenizerBackendType.SentencePieceUnigram;
            }

            try
            {
                var modelType = tokenizerConfigRoot["model_type"]?.Value<string>()?.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(modelType) && modelType.IndexOf("bpe", StringComparison.Ordinal) >= 0)
                {
                    return TokenizerBackendType.SentencePieceBpe;
                }
            }
            catch
            {
                // Ignore malformed config and fall back to the most common SentencePiece runtime.
            }

            return TokenizerBackendType.SentencePieceUnigram;
        }

        private static bool ShouldApplySentencePieceIdOffset(string modelDirectory, JObject? tokenizerConfigRoot)
        {
            var directoryName = Path.GetFileName(modelDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (directoryName.IndexOf("xlm-roberta", StringComparison.OrdinalIgnoreCase) >= 0 ||
                directoryName.IndexOf("xlm_roberta", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (tokenizerConfigRoot == null)
            {
                return false;
            }

            try
            {
                var modelType = tokenizerConfigRoot["model_type"]?.Value<string>();
                return !string.IsNullOrWhiteSpace(modelType) &&
                       modelType.IndexOf("xlm", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static JObject? LoadTokenizerConfigRoot(string modelDirectory, ITokenizerJsonSerializer jsonSerializer)
        {
            var tokenizerConfigPath = Path.Combine(modelDirectory, "tokenizer_config.json");
            if (!File.Exists(tokenizerConfigPath))
            {
                return null;
            }

            try
            {
                return jsonSerializer.ParseObject(File.ReadAllText(tokenizerConfigPath));
            }
            catch
            {
                return null;
            }
        }

        private static WordPieceTokenizerOptions? CreateWordPieceOptions(JObject? tokenizerConfigRoot)
        {
            if (tokenizerConfigRoot == null)
            {
                return null;
            }

            var options = new WordPieceTokenizerOptions
            {
                CleanUpTokenizationSpaces = tokenizerConfigRoot["clean_up_tokenization_spaces"]?.Value<bool?>() ?? true,
                LowerCaseBeforeTokenization = tokenizerConfigRoot["do_lower_case"]?.Value<bool?>() ?? true,
                ApplyBasicTokenization = tokenizerConfigRoot["do_basic_tokenize"]?.Value<bool?>() ?? true,
                UnknownToken = tokenizerConfigRoot["unk_token"]?.Value<string>() ?? "[UNK]",
                ClassificationToken = tokenizerConfigRoot["cls_token"]?.Value<string>() ?? "[CLS]",
                SeparatorToken = tokenizerConfigRoot["sep_token"]?.Value<string>() ?? "[SEP]",
                PaddingToken = tokenizerConfigRoot["pad_token"]?.Value<string>() ?? "[PAD]",
                MaskToken = tokenizerConfigRoot["mask_token"]?.Value<string>() ?? "[MASK]",
            };

            return options;
        }

        private static BpeTokenizerOptions? CreateBpeOptions(JObject? tokenizerConfigRoot)
        {
            if (tokenizerConfigRoot == null)
            {
                return null;
            }

            var options = new BpeTokenizerOptions
            {
                UnknownToken = tokenizerConfigRoot["unk_token"]?.Value<string>(),
                ContinuingSubwordPrefix = tokenizerConfigRoot["continuing_subword_prefix"]?.Value<string>(),
                EndOfWordSuffix = tokenizerConfigRoot["end_of_word_suffix"]?.Value<string>(),
                CleanUpTokenizationSpaces = tokenizerConfigRoot["clean_up_tokenization_spaces"]?.Value<bool?>() ?? true,
                AddPrefixSpace = tokenizerConfigRoot["add_prefix_space"]?.Value<bool?>() ?? false,
                UseRegex = tokenizerConfigRoot["use_regex"]?.Value<bool?>() ?? true,
            };

            var modelType = tokenizerConfigRoot["model_type"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(modelType) &&
                (modelType.IndexOf("gpt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 modelType.IndexOf("roberta", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 modelType.IndexOf("byte", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                options.UseByteLevel = true;
                if (modelType.IndexOf("roberta", StringComparison.OrdinalIgnoreCase) >= 0 && tokenizerConfigRoot["add_prefix_space"] == null)
                {
                    options.AddPrefixSpace = true;
                }
            }

            return options;
        }
    }
}