using System;
using System.Collections.Generic;
using System.IO;
using KitsuMate.Tokenizers.Logging;
using KitsuMate.Tokenizers.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KitsuMate.Tokenizers.Tests
{
    public class TokenizerLoggerTests
    {
        private class TestLogger : ILogger
        {
            public List<LogEntry> Logs { get; } = new List<LogEntry>();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Logs.Add(new LogEntry
                {
                    LogLevel = logLevel,
                    Message = formatter(state, exception),
                    Exception = exception
                });
            }
        }

        private class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
        }

        private class TestLoggerFactory : ILoggerFactory
        {
            public Dictionary<string, TestLogger> Loggers { get; } = new Dictionary<string, TestLogger>();

            public ILogger CreateLogger(string categoryName)
            {
                if (!Loggers.ContainsKey(categoryName))
                {
                    Loggers[categoryName] = new TestLogger();
                }
                return Loggers[categoryName];
            }

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }
        }

        [Fact]
        public void LoggerFactory_DefaultsToNullLogger()
        {
            // Reset to default
            TokenizerLogger.LoggerFactory = null!;
            
            var logger = TokenizerLogger.CreateLogger("TestCategory");
            Assert.NotNull(logger);
        }

        [Fact]
        public void LoggerFactory_CanBeSet()
        {
            var factory = new TestLoggerFactory();
            TokenizerLogger.LoggerFactory = factory;

            var logger = TokenizerLogger.CreateLogger("TestCategory");
            Assert.NotNull(logger);
            Assert.True(factory.Loggers.ContainsKey("TestCategory"));

            // Reset to default
            TokenizerLogger.LoggerFactory = null!;
        }

        [Fact]
        public void FallbackToAlternativeFiles_LogsWarning_WhenTokenizerJsonFails()
        {
            var factory = new TestLoggerFactory();
            TokenizerLogger.LoggerFactory = factory;

            try
            {
                // Create a directory with a malformed tokenizer.json
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                // Create a malformed tokenizer.json
                File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{\"model\": {\"type\": \"unsupported_type\"}}");

                // Create a valid vocab.txt as fallback
                File.WriteAllText(Path.Combine(tempDir, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\n[MASK]\nhello\nworld");

                // Try to load - should fallback and log a warning
                var tokenizer = Tokenizer.FromLocal(tempDir);
                
                Assert.NotNull(tokenizer);

                // Check that a warning was logged
                Assert.True(factory.Loggers.ContainsKey("TokenizerLoader"));
                var loaderLogs = factory.Loggers["TokenizerLoader"].Logs;
                Assert.NotEmpty(loaderLogs);
                
                var warningLog = loaderLogs.Find(l =>
                    l.LogLevel == LogLevel.Warning &&
                    l.Message.Contains("Falling back to sibling artifacts", StringComparison.Ordinal));
                Assert.NotNull(warningLog);
                Assert.Contains("tokenizer.json", warningLog.Message);

                // Cleanup
                Directory.Delete(tempDir, true);
            }
            finally
            {
                TokenizerLogger.LoggerFactory = null!;
            }
        }

        [Fact]
        public void UnsupportedTokenizerJson_LogsWarning_WhenLoaderFallsBackToArtifacts()
        {
            var factory = new TestLoggerFactory();
            TokenizerLogger.LoggerFactory = factory;

            try
            {
                // Create a directory with an unsupported tokenizer.json and a valid vocab.txt fallback
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var tokenizerJson = @"{
                    ""model"": {
                        ""type"": ""UnsupportedType""
                    }
                }";
                File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), tokenizerJson);

                File.WriteAllText(Path.Combine(tempDir, "vocab.txt"), "[PAD]\n[UNK]\n[CLS]\n[SEP]\n[MASK]\nhello\nworld");

                // Try to load - should fallback and log a warning
                var tokenizer = Tokenizer.FromLocal(tempDir);
                
                Assert.NotNull(tokenizer);
                Assert.IsType<Tokenizer>(tokenizer);
                Assert.Equal(TokenizerBackendType.WordPiece, tokenizer.BackendType);

                Assert.True(factory.Loggers.ContainsKey("TokenizerLoader"));
                var loaderLogs = factory.Loggers["TokenizerLoader"].Logs;
                Assert.NotEmpty(loaderLogs);
                
                var warningLog = loaderLogs.Find(l => l.LogLevel == LogLevel.Warning && l.Message.Contains("unsupported native tokenizer"));
                Assert.NotNull(warningLog);
                Assert.Contains("Falling back to sibling artifacts", warningLog.Message);

                // Cleanup
                Directory.Delete(tempDir, true);
            }
            finally
            {
                TokenizerLogger.LoggerFactory = null!;
            }
        }
    }
}
