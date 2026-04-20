using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KitsuMate.Tokenizers.Remote
{
    /// <summary>
    /// Handles downloading tokenizer files from HuggingFace Hub.
    /// Inspired by HuggingFace transformers library's model download functionality.
    /// </summary>
    internal class RemoteDownloader : IDisposable
    {
        private const string HuggingFaceHub = "https://huggingface.co";
        private const string HuggingFaceResolveEndpoint = "https://huggingface.co/{0}/resolve/{1}/{2}";
        
        // List of tokenizer files that might be needed
        private static readonly string[] TokenizerFiles = new[]
        {
            "config.json",
            "tokenizer.json",
            "tokenizer_config.json",
            "vocab.txt",
            "vocab.json",
            "merges.txt",
            "special_tokens_map.json",
            "added_tokens.json"
        };
        
        private static readonly string[] SentencePieceFiles = new[]
        {
            "sentencepiece.bpe.model",
            "tokenizer.model",
            "spiece.model"
        };

        private readonly HttpMessageInvoker _httpClient;
        private readonly bool _disposeClient;
        private readonly string _cacheDir;

        /// <summary>
        /// Initializes a new instance of the RemoteDownloader class.
        /// </summary>
        /// <param name="httpClient">Optional HTTP client for downloading files. If null, a default client will be created.</param>
        /// <param name="cacheDir">Optional cache directory. If null, uses default cache location.</param>
        public RemoteDownloader(HttpMessageInvoker? httpClient = null, string? cacheDir = null)
        {
            if (httpClient == null)
            {
                _httpClient = new HttpClient();
                _disposeClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }

            _cacheDir = cacheDir ?? GetDefaultCacheDir();
        }

        /// <summary>
        /// Gets the default cache directory for downloaded models.
        /// </summary>
        private static string GetDefaultCacheDir()
        {
            // Use platform-specific cache directory
            // Similar to HuggingFace's default cache: ~/.cache/huggingface/hub
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cacheDir = Path.Combine(homeDir, ".cache", "kitsumate-tokenizers", "hub");
            return cacheDir;
        }

        /// <summary>
        /// Parses a model identifier to extract the model path and revision.
        /// </summary>
        /// <param name="modelId">Model identifier (e.g., "bert-base-uncased" or "bert-base-uncased@main")</param>
        /// <param name="modelPath">Output: The model path (e.g., "bert-base-uncased")</param>
        /// <param name="revision">Output: The revision (default is "main")</param>
        private static void ParseModelId(string modelId, out string modelPath, out string revision)
        {
            var parts = modelId.Split('@');
            modelPath = parts[0];
            revision = parts.Length > 1 ? parts[1] : "main";
        }

        /// <summary>
        /// Downloads tokenizer files from HuggingFace Hub.
        /// </summary>
        /// <param name="modelId">Model identifier (e.g., "bert-base-uncased" or "organization/model-name")</param>
        /// <param name="revision">Git revision (branch, tag, or commit hash). Defaults to "main".</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Path to the local directory containing downloaded files</returns>
        public async Task<string> DownloadTokenizerFilesAsync(
            string modelId, 
            string? revision = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

            // Parse model ID to handle revision in format "model@revision"
            ParseModelId(modelId, out var modelPath, out var parsedRevision);
            revision = revision ?? parsedRevision;

            // Create cache directory for this model
            var modelCacheDir = GetModelCacheDir(modelPath, revision);
            Directory.CreateDirectory(modelCacheDir);

            // Try to download tokenizer files in order of preference
            var downloadedFiles = new List<string>();

            // First, try to get tokenizer.json (most comprehensive)
            await TryDownloadFileAsync(modelPath, revision, "tokenizer.json", modelCacheDir, downloadedFiles, cancellationToken);
            
            // Try to get tokenizer_config.json (contains metadata)
            await TryDownloadFileAsync(modelPath, revision, "tokenizer_config.json", modelCacheDir, downloadedFiles, cancellationToken);

            // Try SentencePiece model files
            foreach (var spFile in SentencePieceFiles)
            {
                if (await TryDownloadFileAsync(modelPath, revision, spFile, modelCacheDir, downloadedFiles, cancellationToken))
                    break; // Only need one SentencePiece file
            }

            // Try vocab files
            await TryDownloadFileAsync(modelPath, revision, "vocab.txt", modelCacheDir, downloadedFiles, cancellationToken);
            await TryDownloadFileAsync(modelPath, revision, "vocab.json", modelCacheDir, downloadedFiles, cancellationToken);
            
            // If vocab.json exists, try to get merges.txt
            if (downloadedFiles.Any(f => Path.GetFileName(f) == "vocab.json"))
            {
                await TryDownloadFileAsync(modelPath, revision, "merges.txt", modelCacheDir, downloadedFiles, cancellationToken);
            }

            // Try other optional files
            await TryDownloadFileAsync(modelPath, revision, "special_tokens_map.json", modelCacheDir, downloadedFiles, cancellationToken);
            await TryDownloadFileAsync(modelPath, revision, "added_tokens.json", modelCacheDir, downloadedFiles, cancellationToken);

            if (downloadedFiles.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No tokenizer files found for model '{modelId}'. " +
                    "The model may not exist or may not have tokenizer files.");
            }

            return modelCacheDir;
        }

        /// <summary>
        /// Gets the cache directory for a specific model and revision.
        /// </summary>
        private string GetModelCacheDir(string modelPath, string revision)
        {
            // Create a safe directory name from model path
            var safeModelPath = modelPath.Replace("/", "--");
            return Path.Combine(_cacheDir, $"{safeModelPath}--{revision}");
        }

        /// <summary>
        /// Attempts to download a file from HuggingFace Hub.
        /// </summary>
        /// <returns>True if file was downloaded successfully, false otherwise</returns>
        private async Task<bool> TryDownloadFileAsync(
            string modelPath,
            string revision,
            string fileName,
            string targetDir,
            List<string> downloadedFiles,
            CancellationToken cancellationToken)
        {
            var targetPath = Path.Combine(targetDir, fileName);
            
            // Check if file already exists in cache
            if (File.Exists(targetPath))
            {
                downloadedFiles.Add(targetPath);
                return true;
            }

            // Construct download URL
            var url = string.Format(HuggingFaceResolveEndpoint, modelPath, revision, fileName);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await SendRequestAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    // File doesn't exist on server, skip it
                    return false;
                }

                // Download file content
#if NETSTANDARD2_0
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(targetPath))
                {
                    await contentStream.CopyToAsync(fileStream);
                }
#else
                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = File.Create(targetPath))
                {
                    await contentStream.CopyToAsync(fileStream, cancellationToken);
                }
#endif

                downloadedFiles.Add(targetPath);
                return true;
            }
            catch (HttpRequestException)
            {
                // File doesn't exist or network error, skip it
                return false;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Other errors, skip the file but continue
                return false;
            }
        }

        /// <summary>
        /// Sends an HTTP request using the configured HttpMessageInvoker.
        /// </summary>
        private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // HttpClient inherits from HttpMessageInvoker and has SendAsync with CancellationToken
            if (_httpClient is HttpClient httpClient)
            {
                return await httpClient.SendAsync(request, cancellationToken);
            }
            else
            {
                // For raw HttpMessageInvoker, SendAsync signature differs between frameworks
                // In .NET Standard 2.0, HttpMessageInvoker.SendAsync takes CancellationToken
                // In newer frameworks, it also supports CancellationToken
                return await _httpClient.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// Disposes of resources used by the downloader.
        /// </summary>
        public void Dispose()
        {
            if (_disposeClient)
            {
                (_httpClient as IDisposable)?.Dispose();
            }
        }
    }
}
