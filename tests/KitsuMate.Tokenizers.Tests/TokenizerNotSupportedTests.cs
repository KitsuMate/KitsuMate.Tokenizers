using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerNotSupportedTests
    {
        [Fact]
        public void Create_WithUnsupportedModel_ThrowsMeaningfulException()
        {
            var model = new UnsupportedTokenizerModel("unsupported-model", TokenizerBackendType.Unknown);

            var exception = Assert.Throws<TokenizerNotSupportedException>(() => Tokenizer.Create(model));

            Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
            Assert.Contains("unsupported-model", exception.Message);
            Assert.Contains("not implemented", exception.Message);
        }

        [Fact]
        public void CreateFromTokenizerJson_WithUnsupportedBackend_ThrowsMeaningfulException()
        {
            var pipeline = new TokenizerJsonPipeline(
                tokenizerJsonPath: "unsupported.json",
                name: "unsupported-tokenizer",
                root: new Newtonsoft.Json.Linq.JObject(),
                tokenizerConfigRoot: null,
                backendType: TokenizerBackendType.Unknown,
                addedTokens: System.Array.Empty<TokenizerJsonAddedToken>(),
                truncation: null,
                padding: null,
                normalizerConfig: null,
                preTokenizerConfig: null,
                postProcessorConfig: null,
                decoderConfig: null,
                normalizer: null,
                preTokenizer: null,
                postProcessor: null,
                decoder: null,
                sentencePieceModelPath: null,
                applySentencePieceIdOffset: false,
                addDummyPrefixForSentencePieceBpe: true);

            var exception = Assert.Throws<TokenizerNotSupportedException>(() => TokenizerFactory.Create(pipeline));

            Assert.Equal(TokenizerBackendType.Unknown, exception.BackendType);
            Assert.Contains("unsupported-tokenizer", exception.Message);
            Assert.Contains("not implemented", exception.Message);
        }

        private sealed class UnsupportedTokenizerModel : ITokenizerModel
        {
            public UnsupportedTokenizerModel(string name, TokenizerBackendType backendType)
            {
                Name = name;
                BackendType = backendType;
            }

            public string Name { get; }

            public TokenizerBackendType BackendType { get; }

            public bool SupportsDecode => false;

            public int? TokenToId(string token)
            {
                return null;
            }

            public string? IdToToken(int id)
            {
                return null;
            }

            public System.Collections.Generic.IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue)
            {
                return System.Array.Empty<int>();
            }

            public string? Decode(System.Collections.Generic.IEnumerable<int> ids)
            {
                return null;
            }
        }
    }
}