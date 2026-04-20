using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Default Newtonsoft.Json-based serializer for tokenizer configuration payloads.
    /// </summary>
    public sealed class DefaultTokenizerJsonSerializer : ITokenizerJsonSerializer
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
        };

        public JObject ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON payload cannot be null or empty.", nameof(json));
            }

            return JObject.Parse(json);
        }

        public T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON payload cannot be null or empty.", nameof(json));
            }

            var result = JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            if (result == null)
            {
                throw new JsonSerializationException($"Failed to deserialize tokenizer JSON into {typeof(T).FullName}.");
            }

            return result;
        }
    }
}