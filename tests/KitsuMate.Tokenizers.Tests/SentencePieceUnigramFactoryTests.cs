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
                Tokenizer.CreateSentencePieceUnigram(nonexistentPath));

            Assert.Contains("Model file not found", exception.Message);
            Assert.Contains(nonexistentPath, exception.Message);
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithNullStream_ShouldThrowArgumentNullException()
        {
            var factory = new TokenizerFactory();

            var exception = Assert.Throws<ArgumentNullException>(() =>
                factory.CreateSentencePieceUnigram((Stream)null!));

            Assert.Equal("modelStream", exception.ParamName);
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithEmptyStream_ShouldThrowException()
        {
            var factory = new TokenizerFactory();
            using var emptyStream = new MemoryStream();

            Assert.ThrowsAny<Exception>(() =>
                factory.CreateSentencePieceUnigram(emptyStream));
        }

        [Fact]
        public void DefaultFactory_CreateSentencePiece_WithInvalidData_ShouldThrowException()
        {
            var factory = new TokenizerFactory();
            using var invalidStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            Assert.ThrowsAny<Exception>(() =>
                factory.CreateSentencePieceUnigram(invalidStream));
        }

        [Fact]
        public void SharedSentencePieceFactorySurfaces_AreAvailableForUnigram()
        {
            var tokenizerMethod = typeof(Tokenizer).GetMethod(
                nameof(Tokenizer.CreateSentencePieceUnigram),
                new[] { typeof(string), typeof(bool) });
            var factoryMethod = typeof(TokenizerFactory).GetMethod(
                nameof(TokenizerFactory.CreateSentencePieceUnigram),
                new[] { typeof(Stream), typeof(bool) });

            Assert.NotNull(tokenizerMethod);
            Assert.NotNull(factoryMethod);
            Assert.True(tokenizerMethod!.IsStatic);
        }
    }
}