using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using KitsuMate.Tokenizers.Remote;

namespace KitsuMate.Tokenizers.Tests
{
    public class RemoteDownloaderTests
    {
        [Fact]
        public void RemoteDownloader_Constructor_ShouldUseDefaultClient()
        {
            // Arrange & Act
            using var downloader = new RemoteDownloader();

            // Assert - should not throw
            Assert.NotNull(downloader);
        }

        [Fact]
        public void RemoteDownloader_Constructor_ShouldAcceptCustomClient()
        {
            // Arrange
            using var httpClient = new HttpClient();

            // Act
            using var downloader = new RemoteDownloader(httpClient);

            // Assert - should not throw
            Assert.NotNull(downloader);
        }

        [Fact]
        public void RemoteDownloader_Constructor_ShouldAcceptCustomCacheDir()
        {
            // Arrange
            var customCacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // Act
            using var downloader = new RemoteDownloader(cacheDir: customCacheDir);

            // Assert - should not throw
            Assert.NotNull(downloader);
        }

        [Fact]
        public async Task RemoteDownloader_DownloadTokenizerFilesAsync_ShouldThrowForEmptyModelId()
        {
            // Arrange
            using var downloader = new RemoteDownloader();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await downloader.DownloadTokenizerFilesAsync(""));
        }

        [Fact]
        public async Task RemoteDownloader_DownloadTokenizerFilesAsync_ShouldThrowForNullModelId()
        {
            // Arrange
            using var downloader = new RemoteDownloader();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await downloader.DownloadTokenizerFilesAsync(null!));
        }

        [Fact]
        public void RemoteDownloader_Dispose_ShouldNotThrow()
        {
            // Arrange
            var downloader = new RemoteDownloader();

            // Act & Assert - should not throw
            downloader.Dispose();
        }

        // Note: Integration tests that actually download from HuggingFace Hub
        // should be in a separate test class and marked with appropriate categories
        // to avoid running them in CI/CD pipelines without network access
    }
}
