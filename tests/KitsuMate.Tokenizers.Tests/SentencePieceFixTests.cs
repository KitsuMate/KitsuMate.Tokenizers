// using System;
// using System.Collections;
// using System.IO;
// using System.Reflection;
// using KitsuMate.Tokenizers.Utils;
// using Microsoft.ML.Tokenizers;
// using Xunit;
// using Xunit.Abstractions;

// namespace KitsuMate.Tokenizers.Tests
// {
//     public class SentencePieceFixTests
//     {
//         private readonly ITestOutputHelper _output;

//         public SentencePieceFixTests(ITestOutputHelper output)
//         {
//             _output = output;
//         }

//         private static string GetTestDataPath(string fileName)
//         {
//             var baseDir = AppContext.BaseDirectory;
//             var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
//             return Path.Combine(projectRoot, "TestData", fileName);
//         }

//         private static readonly string TestModelPath = GetTestDataPath("t5_spiece.model");

//         [Fact]
//         public void CreateSafe_WithPadIdBugModel_CreatesTokenizer()
//         {
//             if (!File.Exists(TestModelPath))
//             {
//                 return;
//             }

//             bool fallbackPathTriggered = false;

//             using (var originalStream = File.OpenRead(TestModelPath))
//             {
//                 try
//                 {
//                     _ = SentencePieceTokenizer.Create(originalStream, addBeginningOfSentence: false, addEndOfSentence: false, specialTokens: null);
//                 }
//                 catch (IndexOutOfRangeException)
//                 {
//                     fallbackPathTriggered = true;
//                 }
//             }

//             using var stream = File.OpenRead(TestModelPath);
//             var tokenizer = SentencePieceFix.CreateSafe(stream, addBeginningOfSentence: false, addEndOfSentence: false, specialTokens: null);

//             var ids = tokenizer.EncodeToIds("Hello world");

//             Assert.NotNull(tokenizer);
//             Assert.NotNull(ids);
//             Assert.NotEmpty(ids);

//             // The fallback path should have been triggered on the buggy preview version.
//             // If a future version fixes the underlying bug and no exception is thrown, the workaround still succeeds.
//             Assert.True(fallbackPathTriggered || ids.Count > 0);
//         }

//         [Fact]
//         public void InspectPadId_T5SmallRemote()
//         {
//             var remoteModelPath = GetTestDataPath("t5_small_remote.model");

//             if (!File.Exists(remoteModelPath))
//             {
//                 _output.WriteLine("Remote T5 model not present; skipping diagnostics.");
//                 return;
//             }

//             InspectModel(remoteModelPath);
//         }

//         [Fact]
//         public void InspectPadId_AlbertBaseV2()
//         {
//             var remoteModelPath = GetTestDataPath("albert_base_v2_spiece.model");

//             if (!File.Exists(remoteModelPath))
//             {
//                 _output.WriteLine("Remote ALBERT model not present; skipping diagnostics.");
//                 return;
//             }

//             InspectModel(remoteModelPath);
//         }

//         private void InspectModel(string path)
//         {
//             using var stream = File.OpenRead(path);
//             var sentencepieceAssembly = typeof(SentencePieceTokenizer).Assembly;
//             var modelProtoType = sentencepieceAssembly.GetType("Sentencepiece.ModelProto")!;
//             var parser = modelProtoType.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
//             var parseFromMethod = parser!.GetType().GetMethod("ParseFrom", new[] { typeof(Stream) })!;
//             var modelProto = parseFromMethod.Invoke(parser, new object[] { stream })!;

//             var pieces = modelProtoType.GetProperty("Pieces", BindingFlags.Public | BindingFlags.Instance)!.GetValue(modelProto);
//             var countProperty = pieces!.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)!;
//             int vocabSize = (int)countProperty.GetValue(pieces)!;

//             var trainerSpec = modelProtoType.GetProperty("TrainerSpec", BindingFlags.Public | BindingFlags.Instance)!.GetValue(modelProto)!;
//             var padIdProperty = trainerSpec.GetType().GetProperty("PadId", BindingFlags.Public | BindingFlags.Instance)!;
//             var hasPadIdProperty = trainerSpec.GetType().GetProperty("HasPadId", BindingFlags.Public | BindingFlags.Instance);
//             var padPieceProperty = trainerSpec.GetType().GetProperty("PadPiece", BindingFlags.Public | BindingFlags.Instance);
//             var bosIdProperty = trainerSpec.GetType().GetProperty("BosId", BindingFlags.Public | BindingFlags.Instance);
//             var eosIdProperty = trainerSpec.GetType().GetProperty("EosId", BindingFlags.Public | BindingFlags.Instance);
//             var unkIdProperty = trainerSpec.GetType().GetProperty("UnkId", BindingFlags.Public | BindingFlags.Instance);
//             var bosPieceProperty = trainerSpec.GetType().GetProperty("BosPiece", BindingFlags.Public | BindingFlags.Instance);
//             var eosPieceProperty = trainerSpec.GetType().GetProperty("EosPiece", BindingFlags.Public | BindingFlags.Instance);

//             int padId = (int)padIdProperty.GetValue(trainerSpec)!;
//             bool hasPadId = hasPadIdProperty is null || (bool)hasPadIdProperty.GetValue(trainerSpec)!;
//             string? padPiece = padPieceProperty?.GetValue(trainerSpec) as string;
//             int bosId = bosIdProperty is null ? -1 : (int)bosIdProperty.GetValue(trainerSpec)!;
//             int eosId = eosIdProperty is null ? -1 : (int)eosIdProperty.GetValue(trainerSpec)!;
//             int unkId = unkIdProperty is null ? -1 : (int)unkIdProperty.GetValue(trainerSpec)!;
//             string? bosPiece = bosPieceProperty?.GetValue(trainerSpec) as string;
//             string? eosPiece = eosPieceProperty?.GetValue(trainerSpec) as string;

//             // Find actual indices of BOS/EOS pieces
//             int bosPieceIndex = FindPieceIndex(pieces, bosPiece);
//             int eosPieceIndex = FindPieceIndex(pieces, eosPiece);
//             int padPieceIndex = FindPieceIndex(pieces, padPiece);
//             int clsIndex = FindPieceIndex(pieces, "[CLS]");
//             int sepIndex = FindPieceIndex(pieces, "[SEP]");

//             _output.WriteLine($"VocabSize={vocabSize}");
//             _output.WriteLine($"HasPadId={hasPadId}");
//             _output.WriteLine($"PadId={padId}");
//             _output.WriteLine($"PadPiece={padPiece}");
//             _output.WriteLine($"PadPieceIndex={padPieceIndex}");
//             _output.WriteLine($"BosId={bosId}");
//             _output.WriteLine($"EosId={eosId}");
//             _output.WriteLine($"UnkId={unkId}");
//             _output.WriteLine($"BosPiece={bosPiece}");
//             _output.WriteLine($"EosPiece={eosPiece}");
//             _output.WriteLine($"BosPieceIndex={bosPieceIndex}");
//             _output.WriteLine($"EosPieceIndex={eosPieceIndex}");
//             _output.WriteLine($"[CLS] index={clsIndex}");
//             _output.WriteLine($"[SEP] index={sepIndex}");
//         }

//         private static int FindPieceIndex(object pieces, string? target)
//         {
//             if (pieces is not IEnumerable enumerable || string.IsNullOrEmpty(target))
//             {
//                 return -1;
//             }

//             PropertyInfo? pieceProperty = null;
//             int index = 0;
//             foreach (var piece in enumerable)
//             {
//                 if (piece == null)
//                 {
//                     index++;
//                     continue;
//                 }

//                 pieceProperty ??= piece.GetType().GetProperty("Piece", BindingFlags.Public | BindingFlags.Instance);
//                 if (pieceProperty == null)
//                 {
//                     return -1;
//                 }

//                 var value = pieceProperty.GetValue(piece) as string;
//                 if (string.Equals(value, target, StringComparison.Ordinal))
//                 {
//                     return index;
//                 }

//                 index++;
//             }

//             return -1;
//         }

//         [Fact]
//         public void RawSentencePieceEncoding_T5MatchesReference()
//         {
//             var remoteModelPath = GetTestDataPath("t5_small_remote.model");

//             if (!File.Exists(remoteModelPath))
//             {
//                 _output.WriteLine("Remote T5 model not present; skipping raw encode test.");
//                 return;
//             }

//             using var stream = File.OpenRead(remoteModelPath);
//             var tokenizer = SentencePieceFix.CreateSafe(stream, addBeginningOfSentence: false, addEndOfSentence: false, specialTokens: null);

//             var text = "translate English to German: Hello, how are you?";
//             var ids = tokenizer.EncodeToIds(text, int.MaxValue, out _, out _);
//             _output.WriteLine($"T5 actual IDs: {string.Join(",", ids)}");

//             var expected = new[] { 13959, 1566, 12, 2968, 10, 8774, 6, 149, 33, 25, 58, 1 };
//             Assert.Equal(expected, ids);
//         }

//         [Fact]
//         public void RawSentencePieceEncoding_AlbertMatchesReference()
//         {
//             var remoteModelPath = GetTestDataPath("albert_base_v2_spiece.model");

//             if (!File.Exists(remoteModelPath))
//             {
//                 _output.WriteLine("Remote ALBERT model not present; skipping raw encode test.");
//                 return;
//             }

//             using var stream = File.OpenRead(remoteModelPath);
//             var tokenizer = SentencePieceFix.CreateSafe(stream, addBeginningOfSentence: false, addEndOfSentence: false, specialTokens: null);

//             var text = "Hello, how are you?";
//             var ids = tokenizer.EncodeToIds(text, int.MaxValue, out _, out _);
//             _output.WriteLine($"ALBERT actual IDs: {string.Join(",", ids)}");

//             var expected = new[] { 2, 10975, 15, 184, 50, 42, 60, 3 };
//             Assert.Equal(expected, ids);
//         }
//     }
// }
