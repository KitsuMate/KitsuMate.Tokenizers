using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KitsuMate.Tokenizers.Logging
{
    /// <summary>
    /// Provides logging functionality for the KitsuMate.Tokenizers library.
    /// Allows users to configure logging by setting a custom ILoggerFactory.
    /// </summary>
    public static class TokenizerLogger
    {
        private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

        /// <summary>
        /// Gets or sets the logger factory used by the KitsuMate.Tokenizers library.
        /// By default, uses NullLoggerFactory which produces no output.
        /// Set this to your application's ILoggerFactory to enable logging.
        /// </summary>
        /// <example>
        /// <code>
        /// // Using Microsoft.Extensions.Logging
        /// var loggerFactory = LoggerFactory.Create(builder =>
        /// {
        ///     builder.AddConsole();
        ///     builder.SetMinimumLevel(LogLevel.Warning);
        /// });
        /// TokenizerLogger.LoggerFactory = loggerFactory;
        /// </code>
        /// </example>
        public static ILoggerFactory LoggerFactory
        {
            get => _loggerFactory;
            set => _loggerFactory = value ?? NullLoggerFactory.Instance;
        }

        /// <summary>
        /// Creates a logger for the specified category.
        /// </summary>
        /// <param name="categoryName">The category name for the logger.</param>
        /// <returns>An ILogger instance.</returns>
        internal static ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        /// <typeparam name="T">The type whose name is used as the logger category.</typeparam>
        /// <returns>An ILogger instance.</returns>
        internal static ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }
    }
}
