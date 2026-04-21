# KitsuMate.Tokenizers

KitsuMate.Tokenizers is a native C# runtime for Hugging Face tokenizer artifacts. It loads `tokenizer.json`, local vocab/model files, or Hugging Face Hub models and exposes one concrete `Tokenizer` facade for encoding, decoding, batching, padding, and truncation.

## Installation

```bash
dotnet add package KitsuMate.Tokenizers
```

## Highlights

- Native C# runtime, not a thin compatibility wrapper.
- One public entry point: `Tokenizer`.
- Loads local folders, `tokenizer.json`, or Hugging Face Hub models.
- Supports path-, byte[]-, and Stream-based loading for explicit tokenizer creation.
- Supports WordPiece, BPE, Tiktoken, SentencePiece BPE, and SentencePiece Unigram.
- Supports tokenizer-level defaults plus per-call padding and truncation.
- Targets .NET 8.0 and .NET Standard 2.0.

## Quick Start

```csharp
using KitsuMate.Tokenizers;

var tokenizer = Tokenizer.Load("bert-base-uncased");

var encoding = tokenizer.Encode("Hello world!");

Console.WriteLine(string.Join(", ", encoding.Ids));
Console.WriteLine(string.Join(" | ", encoding.Tokens));
Console.WriteLine(tokenizer.Decode(encoding.Ids));
```

## Loading Tokenizers

```csharp
using KitsuMate.Tokenizers;
using System.IO;
using System.Text;

var fromDirectory = Tokenizer.FromLocal("path/to/model-directory");
var fromTokenizerJson = Tokenizer.FromTokenizerJson("path/to/tokenizer.json");
var fromTokenizerJsonBytes = Tokenizer.FromTokenizerJson(Encoding.UTF8.GetBytes(tokenizerJsonText));

using var tokenizerJsonStream = File.OpenRead("path/to/tokenizer.json");
var fromTokenizerJsonStream = Tokenizer.FromTokenizerJson(tokenizerJsonStream);

var fromHub = Tokenizer.FromPretrained("intfloat/e5-small-v2");
var fromUrl = Tokenizer.Load("https://huggingface.co/bert-base-uncased");
```

In-memory `tokenizer.json` loading currently supports self-contained WordPiece and BPE payloads. If your `tokenizer.json` depends on sibling files such as `*.model`, keep using the file-based `Tokenizer.FromTokenizerJson("path/to/tokenizer.json")` or `Tokenizer.FromLocal(...)` entry points.

Downloaded Hub models are cached under `~/.cache/kitsumate-tokenizers/hub/`.

If you need a custom client for auth, proxies, or timeouts:

```csharp
using KitsuMate.Tokenizers;
using System.Net.Http;

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer YOUR_TOKEN");

var tokenizer = Tokenizer.Load("private-org/private-model", httpClient);
```

## Creating Tokenizers Explicitly

Use the `Tokenizer.Create*` helpers when you already know the backend and have the raw files.

Each explicit `Create*` family supports the same three input shapes when applicable:

- file paths for local artifacts
- `byte[]` when you already have the full payload in memory
- `Stream` when you want to avoid a temp file or you are already working with streams

```csharp
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;
using System.IO;

var wordPiece = Tokenizer.CreateWordPiece("vocab.txt");
var wordPieceFromBytes = Tokenizer.CreateWordPiece(File.ReadAllBytes("vocab.txt"));
using var wordPieceStream = File.OpenRead("vocab.txt");
var wordPieceFromStream = Tokenizer.CreateWordPiece(wordPieceStream);

var bpe = Tokenizer.CreateBpe(
  "vocab.json",
  "merges.txt",
  new BpeTokenizerOptions
  {
    UseByteLevel = true,
    AddPrefixSpace = true,
  });

var bpeFromBytes = Tokenizer.CreateBpe(
  File.ReadAllBytes("vocab.json"),
  File.ReadAllBytes("merges.txt"));

using var bpeVocabStream = File.OpenRead("vocab.json");
using var bpeMergesStream = File.OpenRead("merges.txt");
var bpeFromStreams = Tokenizer.CreateBpe(
  bpeVocabStream,
  bpeMergesStream);

var sentencePiece = Tokenizer.CreateSentencePiece(
  "sentencepiece.model",
  TokenizerBackendType.SentencePieceUnigram);

var sentencePieceFromBytes = Tokenizer.CreateSentencePiece(
  File.ReadAllBytes("sentencepiece.model"),
  TokenizerBackendType.SentencePieceUnigram);

using var sentencePieceStream = File.OpenRead("sentencepiece.model");
var sentencePieceFromStream = Tokenizer.CreateSentencePiece(
  sentencePieceStream,
  TokenizerBackendType.SentencePieceUnigram);

var tiktoken = Tokenizer.CreateTiktoken("gpt2.tiktoken", encodingName: "gpt2");
var tiktokenFromBytes = Tokenizer.CreateTiktoken(File.ReadAllBytes("gpt2.tiktoken"), encodingName: "gpt2");
using var tiktokenStream = File.OpenRead("gpt2.tiktoken");
var tiktokenFromStream = Tokenizer.CreateTiktoken(tiktokenStream, encodingName: "gpt2");
```

For `.tiktoken` bytes or streams, pass `encodingName` explicitly because there is no filename to infer it from.

## Creating From Public Model Types

If you want to own the model object directly, create the model and pass it to `Tokenizer.Create(model)`.

```csharp
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;

var model = WordPieceModel.FromVocab(
  "vocab.txt",
  new WordPieceTokenizerOptions
  {
    LowerCaseBeforeTokenization = true,
    ClassificationToken = "[CLS]",
    SeparatorToken = "[SEP]",
  });

var tokenizer = Tokenizer.Create(model);
```

The public model types also expose in-memory helpers such as `WordPieceModel.FromBytes(...)`, `WordPieceModel.FromStream(...)`, `BpeModel.FromBytes(...)`, `BpeModel.FromStreams(...)`, and `TiktokenModel.FromBytes(...)`.

SentencePiece model types also expose `FromBytes(...)` and `FromStream(...)` helpers when you want to build the model first and then pass it to `Tokenizer.Create(model)`.

Other public model entry points include `BpeModel`, `TiktokenModel`, `SentencePieceUnigramModel`, and `SentencePieceBpeModel`.

## Encoding API

The main API surface now lives directly on `Tokenizer`. There is no separate legacy extension layer to learn.

```csharp
using KitsuMate.Tokenizers;

var tokenizer = Tokenizer.Load("bert-base-uncased");

var ids = tokenizer.EncodeToIds("Tokenize this.");
var pairIds = tokenizer.EncodePairToIds("Question", "Answer");
var encoding = tokenizer.Encode("Tokenize this.");
var batch = tokenizer.EncodeBatch(new[] { "one", "two" });
var tokenCount = tokenizer.CountTokens("Tokenize this.");
var decoded = tokenizer.Decode(ids, skipSpecialTokens: true);
```

`EncodingResult` exposes the usual tokenizer output fields:

- `Ids`
- `Tokens`
- `Offsets`
- `TypeIds`
- `AttentionMask`
- `SpecialTokensMask`

## Padding And Truncation

There are two ways to control padding and truncation.

### Per-call options

Use `TokenizerEncodeOptions` when the behavior should apply only to one encode call.

```csharp
using KitsuMate.Tokenizers;

var tokenizer = Tokenizer.Load("bert-base-uncased");

var encoding = tokenizer.Encode(
  "A longer piece of text than I want to keep.",
  new TokenizerEncodeOptions
  {
    MaxLength = 12,
    Truncation = TokenizerTruncationMode.LongestFirst,
    Padding = TokenizerPaddingMode.MaxLength,
    PaddingSide = TokenizerSide.Right,
  });
```

For batch encoding, `TokenizerPaddingMode.Longest` pads to the longest encoded item in the batch.

```csharp
var batch = tokenizer.EncodeBatch(
  new[] { "short", "a much longer example" },
  new TokenizerEncodeOptions
  {
    Padding = TokenizerPaddingMode.Longest,
    Truncation = TokenizerTruncationMode.LongestFirst,
    MaxLength = 16,
  });
```

### Tokenizer-level defaults

Use `Tokenizer.Padding` and `Tokenizer.Truncation` when you want the tokenizer instance to carry defaults for subsequent calls.

```csharp
using KitsuMate.Tokenizers;
using KitsuMate.Tokenizers.Core;

var tokenizer = Tokenizer.Load("bert-base-uncased");

tokenizer.Truncation = new Truncation(
  direction: "right",
  maxLength: 16,
  strategy: "longest_first",
  stride: 0);

tokenizer.Padding = new Padding(
  strategy: "fixed",
  direction: "right",
  length: 16,
  padId: 0,
  padTypeId: 0,
  padToken: "[PAD]",
  padToMultipleOf: null);

var encoding = tokenizer.Encode("Defaults now apply here.");
```

Per-call `TokenizerEncodeOptions` still override direction and max-length behavior when you need a one-off policy.

## Supported Artifact Shapes

`Tokenizer.Load(...)` and `Tokenizer.FromLocal(...)` can discover tokenizers from common local artifact layouts:

- `tokenizer.json`
- `vocab.txt`
- `vocab.json` + `merges.txt`
- `*.model`
- `.tiktoken`

If `tokenizer.json` is present but unsupported or malformed, loading falls back to sibling artifacts by default. To make `tokenizer.json` authoritative instead, disable that behavior:

```csharp
var tokenizer = Tokenizer.FromLocal(
  "path/to/model-directory",
  new TokenizerLoadOptions { FallbackToOtherVariants = false });
```

## Notes

- Current package version: `1.0.0`
- Repository: `https://github.com/KitsuMate/Tokenizers`
- License: Apache License 2.0

## Contributing

Issues and pull requests are welcome.
