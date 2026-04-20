using System;
using System.IO;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class SentencePieceUnigramFactoryTests
    {
        [Fact]
        public void CreateSentencePiece_WithNonexistentFile_ShouldThrowFileNotFoundException()
        {
            var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".model");

            var exception = Assert.Throws<FileNotFoundException>(() =>
                Tokenizer.CreateSentencePiece(nonexistentPath, TokenizerBackendType.SentencePieceUnigram));

            Assert.Contains("Model file not found", exception.Message);
            Assert.Contains(nonexistentPath, exception.Message);
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithNullStream_ShouldThrowArgumentNullException()
        {
            var factory = new TokenizerFactory();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                factory.CreateSentencePiece((Stream)null!, TokenizerBackendType.SentencePieceUnigram));

            Assert.Equal("modelStream", exception.ParamName);
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithEmptyStream_ShouldThrowException()
        {
            var factory = new TokenizerFactory();
            using var emptyStream = new MemoryStream();

            Assert.ThrowsAny<Exception>(() =>
                factory.CreateSentencePiece(emptyStream, TokenizerBackendType.SentencePieceUnigram));
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithInvalidData_ShouldThrowException()
        {
            var factory = new TokenizerFactory();
            using var invalidStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            Assert.ThrowsAny<Exception>(() =>
                factory.CreateSentencePiece(invalidStream, TokenizerBackendType.SentencePieceUnigram));
        }

        [Fact]
        public void SharedSentencePieceFactorySurfaces_AreAvailableForUnigram()
        {
            var tokenizerMethod = typeof(Tokenizer).GetMethod(
                nameof(Tokenizer.CreateSentencePiece),
                new[] { typeof(string), typeof(TokenizerBackendType), typeof(bool), typeof(bool) });
            var factoryMethod = typeof(TokenizerFactory).GetMethod(
                nameof(TokenizerFactory.CreateSentencePiece),
                new[] { typeof(Stream), typeof(TokenizerBackendType), typeof(bool), typeof(bool) });

            Assert.NotNull(tokenizerMethod);
            Assert.NotNull(factoryMethod);
            Assert.True(tokenizerMethod!.IsStatic);
        }
    }
}