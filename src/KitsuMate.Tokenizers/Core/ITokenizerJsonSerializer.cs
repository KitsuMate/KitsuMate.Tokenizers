using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Isolates tokenizer-related JSON parsing behind Newtonsoft.Json.
    /// </summary>
    public interface ITokenizerJsonSerializer
    {
        JObject ParseObject(string json);

        T Deserialize<T>(string json) where T : class;
    }
}