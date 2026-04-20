using System.Collections.Generic;

namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// Interface for all HuggingFace compatible decoders.
    /// </summary>
    public interface IDecoder
    {
        /// <summary>
        /// Decodes a list of tokens back to text.
        /// </summary>
        /// <param name="tokens">List of tokens to decode.</param>
        /// <returns>Decoded text.</returns>
        string Decode(IEnumerable<string> tokens);
    }
}
