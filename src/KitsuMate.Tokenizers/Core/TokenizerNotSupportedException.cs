using System;

namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Thrown when a tokenizer backend is recognized but not implemented yet.
    /// </summary>
    public sealed class TokenizerNotSupportedException : NotSupportedException
    {
        public TokenizerNotSupportedException(string message, TokenizerBackendType backendType)
            : base(message)
        {
            BackendType = backendType;
        }

        public TokenizerBackendType BackendType { get; }
    }
}