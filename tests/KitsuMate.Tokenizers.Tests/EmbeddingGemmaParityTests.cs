using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public sealed class EmbeddingGemmaFactAttribute : FactAttribute
    {
        public static string ModelDirectory => Environment.GetEnvironmentVariable("KITSUMATE_EMBEDDINGGEMMA_FIXTURE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "kitsumate-tokenizers", "hub", "google--embeddinggemma-300m--main");
        public EmbeddingGemmaFactAttribute()
        {
            if (!File.Exists(Path.Combine(ModelDirectory, "tokenizer.json")) || !File.Exists(Path.Combine(ModelDirectory, "tokenizer.model")))
                Skip = "EmbeddingGemma fixture unavailable. Set KITSUMATE_EMBEDDINGGEMMA_FIXTURE to run this integration check.";
        }
    }

    public class EmbeddingGemmaParityTests
    {
        [EmbeddingGemmaFact]
        [Trait("Category", "Integration")]
        public void MatchesUpstreamTokenizerIncludingWhitespaceAndTruncation()
        {
            var directory = EmbeddingGemmaFactAttribute.ModelDirectory;
            Assert.True(directory != null && File.Exists(Path.Combine(directory, "tokenizer.json")),
                "Set KITSUMATE_EMBEDDINGGEMMA_FIXTURE to the downloaded onnx-community/embeddinggemma-300m-ONNX tokenizer directory.");
            var fixtures = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EmbeddingGemmaReference.json")));
            var tokenizers = new[] { Tokenizer.FromLocal(directory!), Tokenizer.FromTokenizerJson(
                File.ReadAllBytes(Path.Combine(directory!, "tokenizer.json")),
                File.ReadAllBytes(Path.Combine(directory!, "tokenizer.model")),
                File.ReadAllBytes(Path.Combine(directory!, "tokenizer_config.json"))) };
            foreach (var tokenizer in tokenizers)
            foreach (var fixture in fixtures)
            {
                var actual = tokenizer.Encode((string)fixture["text"]!, addSpecialTokens: true, maxTokenCount: (int)fixture["maxTokens"]!);
                var expected = fixture["ids"]!.Select(id => (int)id).ToArray();
                Assert.True(actual.Ids.SequenceEqual(expected), $"Token IDs differ for {fixture["name"]}: actual {string.Join(",", actual.Ids)} expected {string.Join(",", expected)}");
            }
        }
    }
}
