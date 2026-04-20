using System;

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Interface for all HuggingFace compatible normalizers.
    /// </summary>
    public interface INormalizer
    {
        /// <summary>
        /// Normalizes the input text.
        /// </summary>
        /// <param name="original">The text to normalize.</param>
        /// <returns>The normalized text.</returns>
        string Normalize(string original);

        /// <summary>
        /// Normalizes the input text.
        /// </summary>
        /// <param name="original">The text to normalize.</param>
        /// <returns>The normalized text.</returns>
        string Normalize(ReadOnlySpan<char> original);
    }
}
