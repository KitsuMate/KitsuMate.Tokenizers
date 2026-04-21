using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IntegrationTests
{
    /// <summary>
    /// Integration tests that compare the C# KitsuMate.Tokenizers implementation with Python transformers library.
    /// Tests are dynamically generated based on tokenizer configurations in subdirectories.
    /// Each subdirectory should contain: config.json, vocab files, and will get reference_outputs.json generated.
    /// </summary>
    public class TokenizerIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        
        // Cache for reference outputs to avoid redundant Python execution
        private static readonly Dictionary<string, List<ReferenceOutput>> _referenceCache = new();
        private static readonly Dictionary<string, ITokenizer> _tokenizerInstanceCache = new(StringComparer.OrdinalIgnoreCase);


        public TokenizerIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Dispose()
        {
            // Test runner will call this when tests complete
        }

        public class TokenizerConfig
        {
            public string name { get; set; } = "";
            public string description { get; set; } = "";
            public JToken? python_config { get; set; }
            public List<string> test_cases { get; set; } = new List<string>();
            public bool is_remote { get; set; } = false;
            public List<RemoteModelConfig> remote_models { get; set; } = new List<RemoteModelConfig>();
        }

        public class RemoteModelConfig
        {
            public string model_id { get; set; } = "";
            public List<string> test_cases { get; set; } = new List<string>();
        }

        public class ReferenceOutput
        {
            public string text { get; set; } = "";
            public List<string> tokens { get; set; } = new List<string>();
            public List<int> ids { get; set; } = new List<int>();
            public string decoded { get; set; } = "";
            public string model_id { get; set; } = "";
        }

        /// <summary>
        /// Discovers all tokenizer test configurations in subdirectories
        /// </summary>
        private static IEnumerable<string> DiscoverTokenizerConfigs()
        {
            var baseDir = Directory.GetCurrentDirectory();
            var configFiles = Directory.GetFiles(baseDir, "config.json", SearchOption.AllDirectories);
            
            // Filter to only include direct subdirectories, not nested ones
            return configFiles
                .Where(f => Path.GetDirectoryName(f) != baseDir)
                .Select(Path.GetDirectoryName)
                .Where(d => d != null)
                .Cast<string>()
                .ToList();
        }

        /// <summary>
        /// Generates reference outputs by running the Python script for a specific configuration.
        /// Results are cached to avoid redundant executions.
        /// </summary>
        private void GenerateReferenceOutputs(string configDir)
        {

            if (_referenceCache.ContainsKey(configDir))
            {
                _output.WriteLine($"Using cached reference outputs for {Path.GetFileName(configDir)}");
                return;
            }
            

            if (!ReferenceOutputsNeedRegeneration(configDir))
            {
                _output.WriteLine($"Reference outputs already present for {Path.GetFileName(configDir)}; skipping regeneration.");
                return;
            }

            var pythonScript = Path.Combine(Directory.GetCurrentDirectory(), "generate_reference.py");
            
            if (!File.Exists(pythonScript))
            {
                throw new FileNotFoundException($"Python reference generator not found at: {pythonScript}");
            }

            _output.WriteLine($"Generating reference outputs for {Path.GetFileName(configDir)}...");

            string pythonExe = FindPythonExecutable();

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{pythonScript}\" \"{configDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Python process");
            }
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Python script failed with exit code {process.ExitCode}. Run generate_reference.py manually for details.");
            }

            _output.WriteLine($"Reference outputs generated for {Path.GetFileName(configDir)}");
        }

        private static bool ReferenceOutputsNeedRegeneration(string configDir)
        {
            var referencePath = Path.Combine(configDir, "reference_outputs.json");
            if (!File.Exists(referencePath))
                return true;

            var referenceTimestamp = File.GetLastWriteTimeUtc(referencePath);

            var configPath = Path.Combine(configDir, "config.json");
            if (File.Exists(configPath) && File.GetLastWriteTimeUtc(configPath) > referenceTimestamp)
                return true;

            return false;
        }

        /// <summary>
        /// Finds a Python executable that can import transformers.
        /// </summary>
        private string FindPythonExecutable()
        {
            foreach (var candidate in GetPythonCandidates())
            {
                if (CanImportTransformers(candidate))
                {
                    _output.WriteLine($"Using Python interpreter: {candidate}");
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                "Could not find a Python interpreter with the 'transformers' package. " +
                "Set KITSUMATE_TOKENIZERS_PYTHON to a suitable interpreter path if needed.");
        }

        private static IEnumerable<string> GetPythonCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var overridePath = Environment.GetEnvironmentVariable("KITSUMATE_TOKENIZERS_PYTHON");
            if (!string.IsNullOrWhiteSpace(overridePath) && seen.Add(overridePath))
            {
                yield return overridePath;
            }

            var repoRoot = FindRepositoryRoot();
            var venvPython = Path.Combine(repoRoot.FullName, ".venv", "Scripts", "python.exe");
            if (File.Exists(venvPython) && seen.Add(venvPython))
            {
                yield return venvPython;
            }

            foreach (var candidate in new[] { "python3", "python" })
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static bool CanImportTransformers(string pythonExecutable)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    Arguments = "-c \"import transformers\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return false;
                }

                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static DirectoryInfo FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "KitsuMate.Tokenizers.sln")))
                {
                    return current;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not find repository root from test base directory.");
        }

        /// <summary>
        /// Loads tokenizer configuration from config.json
        /// </summary>
        private TokenizerConfig LoadConfig(string configDir)
        {
            var configPath = Path.Combine(configDir, "config.json");
            var json = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<TokenizerConfig>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize config from {configPath}");
        }

        /// <summary>
        /// Loads reference outputs generated by Python.
        /// Uses cache to avoid redundant file reads.
        /// </summary>
        private List<ReferenceOutput> LoadReferenceOutputs(string configDir)
        {

            if (_referenceCache.TryGetValue(configDir, out var cached))
                return cached;
   

            var referencePath = Path.Combine(configDir, "reference_outputs.json");
            
            if (!File.Exists(referencePath))
            {
                throw new FileNotFoundException($"Reference outputs not found at: {referencePath}");
            }

            var json = File.ReadAllText(referencePath);
            var outputs = JsonConvert.DeserializeObject<List<ReferenceOutput>>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize reference outputs from {referencePath}");

            _referenceCache[configDir] = outputs;
            return outputs;
        }

        private ITokenizer GetOrCreateTokenizerInstance(string cacheKey, Func<ITokenizer> factory)
        {

                if (!_tokenizerInstanceCache.TryGetValue(cacheKey, out var tokenizer))
                {
                    tokenizer = factory();
                    _tokenizerInstanceCache[cacheKey] = tokenizer;
                }

                return tokenizer;
            
        }

        private ITokenizer ResolveTokenizerInstance(TokenizerConfig config, ReferenceOutput reference, string configDir)
        {
            if (config.is_remote)
            {
                var modelId = reference.model_id;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    throw new InvalidOperationException($"Remote reference missing model_id for configuration '{config.name}'.");
                }

                var cacheKey = $"remote::{modelId}";
                return GetOrCreateTokenizerInstance(cacheKey, () =>
                {
                    _output.WriteLine($"Loading tokenizer from remote: {modelId}");
                    return Tokenizer.FromPretrained(modelId);
                });
            }

            var localCacheKey = $"local::{configDir}";
            return GetOrCreateTokenizerInstance(localCacheKey, () => Tokenizer.FromLocal(configDir));
        }


        /// <summary>
        /// Theory test that runs for each discovered tokenizer configuration
        /// </summary>
        [Theory]
        [MemberData(nameof(GetTokenizerConfigDirectories))]
        public void TokenizerEncode_MatchesPythonImplementation(string configDir)
        {
            // Load configuration and reference outputs
            var config = LoadConfig(configDir);

            // Generate reference outputs only when newer data is required
            GenerateReferenceOutputs(configDir);
            
            var referenceOutputs = LoadReferenceOutputs(configDir);

            _output.WriteLine($"Testing {config.name}: {config.description}");
            _output.WriteLine($"Test cases: {referenceOutputs.Count}");

            // Test each case
            foreach (var reference in referenceOutputs)
            {
                var tokenizer = ResolveTokenizerInstance(config, reference, configDir);

                var encoded = tokenizer.EncodeToIds(reference.text);

                if (reference.ids.Count != encoded.Count)
                {
                    _output.WriteLine($"Text: {reference.text}");
                    _output.WriteLine($"Expected IDs ({reference.ids.Count}): {string.Join(",", reference.ids)}");
                    _output.WriteLine($"Actual IDs ({encoded.Count}): {string.Join(",", encoded)}");
                }

                // Compare token IDs
                Assert.Equal(reference.ids.Count, encoded.Count);
                for (int i = 0; i < reference.ids.Count; i++)
                {
                    if (reference.ids[i] != encoded[i])
                    {
                        _output.WriteLine($"Mismatch at position {i} -> expected {reference.ids[i]}, actual {encoded[i]}");
                    }

                    Assert.Equal(reference.ids[i], encoded[i]);
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetTokenizerConfigDirectories))]
        public void TokenizerDecode_MatchesPythonImplementation(string configDir)
        {
            // Load configuration and reference outputs
            var config = LoadConfig(configDir);

            // Generate reference outputs only when necessary
            GenerateReferenceOutputs(configDir);
            
            var referenceOutputs = LoadReferenceOutputs(configDir);

            _output.WriteLine($"Testing {config.name}: {config.description}");

            // Test each case
            foreach (var reference in referenceOutputs)
            {
                var tokenizer = ResolveTokenizerInstance(config, reference, configDir);

                var decoded = tokenizer.Decode(reference.ids);
                
                // Compare decoded text
                Assert.Equal(reference.decoded, decoded);
            }
        }

        [Theory]
        [MemberData(nameof(GetTokenizerConfigDirectories))]
        public void TokenizerRoundTrip_PreservesText(string configDir)
        {
            // Load configuration and reference outputs
            var config = LoadConfig(configDir);

            // Generate reference outputs only when required
            GenerateReferenceOutputs(configDir);
            
            var referenceOutputs = LoadReferenceOutputs(configDir);

            _output.WriteLine($"Testing {config.name}: {config.description}");

            // Test each case
            foreach (var reference in referenceOutputs)
            {
                var tokenizer = ResolveTokenizerInstance(config, reference, configDir);

                var encoded = tokenizer.EncodeToIds(reference.text);
                var decoded = tokenizer.Decode(encoded);
                
                // The decoded text should match the reference decoded text
                Assert.Equal(reference.decoded, decoded);
            }
        }

        [Theory]
        [MemberData(nameof(GetTokenizerConfigDirectories))]
        public void TokenizerCountTokens_MatchesPythonImplementation(string configDir)
        {
            // Load configuration and reference outputs
            var config = LoadConfig(configDir);

            // Generate reference outputs only when needed
            GenerateReferenceOutputs(configDir);
            
            var referenceOutputs = LoadReferenceOutputs(configDir);

            _output.WriteLine($"Testing {config.name}: {config.description}");

            // Test each case
            foreach (var reference in referenceOutputs)
            {
                var tokenizer = ResolveTokenizerInstance(config, reference, configDir);

                // Count tokens using the same encoding options as the reference
                var encoded = tokenizer.EncodeToIds(reference.text);
                var tokenCount = encoded.Count;
                
                // The token count should match the number of IDs from Python
                Assert.Equal(reference.ids.Count, tokenCount);
            }
        }

        [Theory]
        [MemberData(nameof(GetTokenizerConfigDirectories))]
        public void Tokenizer_FromLocal_LoadsAndMatchesPythonImplementation(string configDir)
        {
            // Load configuration and reference outputs
            var config = LoadConfig(configDir);

            // Skip this test for remote models as FromLocal doesn't apply
            if (config.is_remote)
            {
                _output.WriteLine($"Skipping Tokenizer.FromLocal test for remote configuration: {config.name}");
                return;
            }
            
            // Generate reference outputs only when needed
            GenerateReferenceOutputs(configDir);
            
            var referenceOutputs = LoadReferenceOutputs(configDir);

            // Use Tokenizer.FromLocal to auto-detect the tokenizer
            var tokenizer = Tokenizer.FromLocal(configDir);
            Assert.NotNull(tokenizer);

            _output.WriteLine($"Testing {config.name}: {config.description}");
            _output.WriteLine($"Auto-detected tokenizer type: {tokenizer.GetType().Name}");

            // Test encoding matches Python
            foreach (var reference in referenceOutputs)
            {
                var encoded = tokenizer.EncodeToIds(reference.text);
                Assert.Equal(reference.ids.Count, encoded.Count);
                for (int i = 0; i < reference.ids.Count; i++)
                {
                    Assert.Equal(reference.ids[i], encoded[i]);
                }
            }
        }

        /// <summary>
        /// Provides test data for Theory tests - one test per tokenizer configuration directory
        /// </summary>
        public static IEnumerable<object[]> GetTokenizerConfigDirectories()
        {
            var configs = DiscoverTokenizerConfigs();
            foreach (var configDir in configs)
            {
                yield return new object[] { configDir };
            }
        }
    }
}
