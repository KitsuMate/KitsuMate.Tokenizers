using System.Collections.Generic;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Low-level model contract used by tokenizers to perform algorithm-specific work.
    /// </summary>
    public interface ITokenizerModel
    {
        string Name { get; }

        TokenizerBackendType BackendType { get; }

        bool SupportsDecode { get; }

        int? TokenToId(string token);

        string? IdToToken(int id);

        IReadOnlyList<int> EncodeToIds(string text, int maxTokenCount = int.MaxValue);

        string? Decode(IEnumerable<int> ids);
    }
}