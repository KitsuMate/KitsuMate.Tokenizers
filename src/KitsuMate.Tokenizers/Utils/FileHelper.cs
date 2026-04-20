using System;
using System.IO;
using System.Text.Json;

namespace KitsuMate.Tokenizers.Utils
{
    /// <summary>
    /// Helper class for file operations, replacing Unity's FileUtils
    /// </summary>
    internal static class FileHelper
    {
        /// <summary>
        /// Read JSON file and deserialize to specified type
        /// </summary>
        public static T GetJsonFile<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            return JsonSerializer.Deserialize<T>(json, options) 
                ?? throw new InvalidOperationException($"Failed to deserialize file: {filePath}");
        }

        /// <summary>
        /// Get file stream for reading
        /// </summary>
        public static Stream GetFileStream(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return File.OpenRead(filePath);
        }
    }
}
