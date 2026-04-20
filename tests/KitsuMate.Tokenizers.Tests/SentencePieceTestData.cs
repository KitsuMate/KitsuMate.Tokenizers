using Google.Protobuf;
using Sentencepiece;
using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using KitsuMate.Tokenizers.Core;
using KitsuMate.Tokenizers.Normalizers;

namespace KitsuMate.Tokenizers.Tests
{
    internal static class SentencePieceTestData
    {
        public static byte[] CreateSimpleUnigramModel()
        {
            return CreateSimpleUnigramModel(addDummyPrefix: false, removeExtraWhitespaces: false, escapeWhitespaces: false, treatWhitespaceAsSuffix: false);
        }

        public static byte[] CreateSimpleUnigramModel(
            bool addDummyPrefix,
            bool removeExtraWhitespaces,
            bool escapeWhitespaces,
            bool treatWhitespaceAsSuffix)
        {
            var model = new ModelProto
            {
                TrainerSpec = new TrainerSpec
                {
                    ModelType = TrainerSpec.Types.ModelType.Unigram,
                    UnkId = 0,
                    BosId = 1,
                    EosId = 2,
                    PadId = -1,
                    UnkPiece = "<unk>",
                    BosPiece = "<s>",
                    EosPiece = "</s>",
                    ByteFallback = false,
                    TreatWhitespaceAsSuffix = treatWhitespaceAsSuffix,
                },
                NormalizerSpec = new NormalizerSpec
                {
                    AddDummyPrefix = addDummyPrefix,
                    EscapeWhitespaces = escapeWhitespaces,
                    RemoveExtraWhitespaces = removeExtraWhitespaces,
                },
            };

            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<unk>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Unknown });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "</s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "he", Score = 4f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "llo", Score = 3f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "world", Score = 2f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });

            return model.ToByteArray();
        }

        public static byte[] CreateDummyPrefixUnigramModel()
        {
            var model = new ModelProto
            {
                TrainerSpec = new TrainerSpec
                {
                    ModelType = TrainerSpec.Types.ModelType.Unigram,
                    UnkId = 0,
                    BosId = 1,
                    EosId = 2,
                    PadId = -1,
                    UnkPiece = "<unk>",
                    BosPiece = "<s>",
                    EosPiece = "</s>",
                    ByteFallback = false,
                    TreatWhitespaceAsSuffix = false,
                },
                NormalizerSpec = new NormalizerSpec
                {
                    AddDummyPrefix = true,
                    EscapeWhitespaces = true,
                    RemoveExtraWhitespaces = true,
                },
            };

            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<unk>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Unknown });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "</s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "▁he", Score = 4f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "llo", Score = 3f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });

            return model.ToByteArray();
        }

        public static byte[] CreateCharsMapUnigramModel()
        {
            var model = new ModelProto
            {
                TrainerSpec = new TrainerSpec
                {
                    ModelType = TrainerSpec.Types.ModelType.Unigram,
                    UnkId = 0,
                    BosId = 1,
                    EosId = 2,
                    PadId = -1,
                    UnkPiece = "<unk>",
                    BosPiece = "<s>",
                    EosPiece = "</s>",
                    ByteFallback = false,
                    TreatWhitespaceAsSuffix = false,
                },
                NormalizerSpec = new NormalizerSpec
                {
                    AddDummyPrefix = false,
                    EscapeWhitespaces = false,
                    RemoveExtraWhitespaces = false,
                    PrecompiledCharsmap = ByteString.CopyFrom(CreatePrecompiledCharsMap(("é", "e"))),
                },
            };

            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<unk>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Unknown });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "</s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "he", Score = 4f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "llo", Score = 3f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });

            return model.ToByteArray();
        }

        public static byte[] CreateSimpleBpeModel()
        {
            var model = new ModelProto
            {
                TrainerSpec = new TrainerSpec
                {
                    ModelType = TrainerSpec.Types.ModelType.Bpe,
                    UnkId = 0,
                    BosId = 1,
                    EosId = 2,
                    PadId = -1,
                    UnkPiece = "<unk>",
                    BosPiece = "<s>",
                    EosPiece = "</s>",
                },
                NormalizerSpec = new NormalizerSpec(),
            };

            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<unk>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Unknown });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "<s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "</s>", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Control });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "▁", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "h", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "e", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "l", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "o", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "he", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "ll", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "llo", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "▁he", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "hello", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });
            model.Pieces.Add(new ModelProto.Types.SentencePiece { Piece = "▁hello", Score = 0f, Type = ModelProto.Types.SentencePiece.Types.Type.Normal });

            return model.ToByteArray();
        }

        internal static byte[] CreateAccentCharMapBlob()
        {
            return CreatePrecompiledCharsMap(("é", "e"));
        }

        private static byte[] CreatePrecompiledCharsMap(params (string Source, string Target)[] mappings)
        {
            if (mappings.Length != 1 || mappings[0].Source != "é" || mappings[0].Target != "e")
            {
                throw new NotSupportedException("Synthetic charsmap test data currently supports only the 'é' -> 'e' mapping.");
            }

            var units = new PrecompiledCharsMap.DoubleArrayUnit[4];
            units[0].Offset = 194;
            units[1].Label = 0xC3;
            units[1].Offset = 170;
            units[2].Label = 0xA9;
            units[2].HasLeaf = true;
            units[2].Offset = 1;
            units[3].Value = 0;

            var trieBytes = MemoryMarshal.AsBytes(units.AsSpan()).ToArray();
            var normalizedBytes = new byte[] { (byte)'e', 0 };
            var blob = new byte[sizeof(uint) + trieBytes.Length + normalizedBytes.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(0, sizeof(uint)), (uint)trieBytes.Length);
            trieBytes.CopyTo(blob.AsSpan(sizeof(uint)));
            normalizedBytes.CopyTo(blob.AsSpan(sizeof(uint) + trieBytes.Length));
            return blob;
        }
    }
}