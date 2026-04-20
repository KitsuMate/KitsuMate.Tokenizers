using System;
using System.IO;
using Sentencepiece;

namespace KitsuMate.Tokenizers.Core
{
    internal static class SentencePieceModelInspector
    {
        public static TokenizerBackendType DetectBackendType(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));
            }

            using var stream = File.OpenRead(modelPath);
            return DetectBackendType(stream);
        }

        public static TokenizerBackendType DetectBackendType(Stream modelStream)
        {
            if (modelStream == null)
            {
                throw new ArgumentNullException(nameof(modelStream));
            }

            var model = ModelProto.Parser.ParseFrom(modelStream);
            return model.TrainerSpec.ModelType switch
            {
                TrainerSpec.Types.ModelType.Bpe => TokenizerBackendType.SentencePieceBpe,
                TrainerSpec.Types.ModelType.Unigram => TokenizerBackendType.SentencePieceUnigram,
                _ => TokenizerBackendType.Unknown,
            };
        }
    }
}