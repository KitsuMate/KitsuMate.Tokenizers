using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using KitsuMate.Tokenizers;

namespace KitsuMate.Tokenizers.Normalizers
{
    /// <summary>
    /// Shared precompiled charsmap engine used by tokenizer.json Precompiled normalizers and
    /// SentencePiece protobuf precompiled charsmap blobs.
    /// </summary>
    public sealed class PrecompiledNormalizer : INormalizer
    {
        private readonly PrecompiledCharsMap? _charsMap;

        public PrecompiledNormalizer(NormalizerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _charsMap = LoadCharsMap(config.PrecompiledCharsmap);
        }

        private static PrecompiledCharsMap? LoadCharsMap(string? charsMap)
        {
            if (string.IsNullOrWhiteSpace(charsMap))
            {
                return null;
            }

            return PrecompiledCharsMap.FromBlob(Convert.FromBase64String(charsMap));
        }

        public string Normalize(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return original;
            }

            return _charsMap?.Apply(original) ?? original;
        }

        public string Normalize(ReadOnlySpan<char> original)
        {
            if (original.IsEmpty)
            {
                return string.Empty;
            }

            return _charsMap?.Apply(original.ToString()) ?? original.ToString();
        }
    }

    internal sealed class PrecompiledCharsMap
    {
        private const int MaxTrieResultsSize = 32;

        private readonly DoubleArrayTrie _trie;
        private readonly byte[] _normalized;

        private PrecompiledCharsMap(DoubleArrayTrie trie, byte[] normalized)
        {
            _trie = trie;
            _normalized = normalized;
        }

        public static PrecompiledCharsMap? FromBlob(ReadOnlySpan<byte> blob)
        {
            if (blob.IsEmpty)
            {
                return null;
            }

            if (blob.Length <= sizeof(uint))
            {
                throw new ArgumentException("Blob for normalization rule is broken.", nameof(blob));
            }

            var trieBlobSize = BinaryPrimitives.ReadUInt32LittleEndian(blob);
            if (trieBlobSize >= blob.Length - sizeof(uint))
            {
                throw new ArgumentException("Trie data size exceeds the input blob size.", nameof(blob));
            }

            blob = blob.Slice(sizeof(uint));
            var trieByteLength = checked((int)trieBlobSize);
            if (trieByteLength % sizeof(uint) != 0)
            {
                throw new ArgumentException("Trie data size must be divisible by 4 bytes.", nameof(blob));
            }

            var trieBlob = blob.Slice(0, trieByteLength);
            var normalized = blob.Slice(trieByteLength).ToArray();
            var trieUnits = MemoryMarshal.Cast<byte, DoubleArrayUnit>(trieBlob).ToArray();

            return new PrecompiledCharsMap(new DoubleArrayTrie(trieUnits), normalized);
        }

        public string Apply(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var input = Encoding.UTF8.GetBytes(text);
            var output = new byte[input.Length * 2];
            var outputIndex = 0;
            var inputIndex = 0;

            while (inputIndex < input.Length)
            {
                var consumed = NormalizePrefix(input.AsSpan(inputIndex), out var replacement, out var matched);
                var bytesToAppend = matched ? replacement : input.AsSpan(inputIndex, consumed);

                if (output.Length < outputIndex + bytesToAppend.Length)
                {
                    Array.Resize(ref output, Math.Max(output.Length * 2, outputIndex + bytesToAppend.Length));
                }

                bytesToAppend.CopyTo(output.AsSpan(outputIndex));
                outputIndex += bytesToAppend.Length;
                inputIndex += consumed;
            }

            return Encoding.UTF8.GetString(output, 0, outputIndex);
        }

        private int NormalizePrefix(ReadOnlySpan<byte> input, out ReadOnlySpan<byte> replacement, out bool matched)
        {
            replacement = default;
            matched = false;

            Span<DoubleArrayResultPair> trieResults = stackalloc DoubleArrayResultPair[MaxTrieResultsSize];
            var numNodes = _trie.CommonPrefixSearch(input, trieResults);

            var longestLength = 0;
            var longestValue = 0;
            for (var index = 0; index < numNodes; index++)
            {
                if (trieResults[index].Length <= longestLength)
                {
                    continue;
                }

                longestLength = trieResults[index].Length;
                longestValue = trieResults[index].Value;
            }

            if (longestLength > 0)
            {
                matched = true;
                var normalizedLength = longestValue;
                while (normalizedLength < _normalized.Length && _normalized[normalizedLength] != 0)
                {
                    normalizedLength++;
                }

                replacement = _normalized.AsSpan(longestValue, normalizedLength - longestValue);
                return longestLength;
            }

            return GetUtf8SequenceLength(input[0]);
        }

        private static int GetUtf8SequenceLength(byte leadingByte)
        {
            if ((leadingByte & 0x80) == 0)
            {
                return 1;
            }

            if ((leadingByte & 0xE0) == 0xC0)
            {
                return 2;
            }

            if ((leadingByte & 0xF0) == 0xE0)
            {
                return 3;
            }

            if ((leadingByte & 0xF8) == 0xF0)
            {
                return 4;
            }

            return 1;
        }

        internal struct DoubleArrayUnit
        {
            private uint _unit;

            public bool HasLeaf
            {
                readonly get => ((_unit >> 8) & 1) == 1;
                set
                {
                    if (value)
                    {
                        _unit |= 1U << 8;
                    }
                    else
                    {
                        _unit &= ~(1U << 8);
                    }
                }
            }

            public uint Value
            {
                readonly get => _unit & ((1U << 31) - 1);
                set => _unit = value | (1U << 31);
            }

            public uint Label
            {
                readonly get => _unit & ((1U << 31) | 0xFF);
                set => _unit = (_unit & ~0xFFU) | value;
            }

            public uint Offset
            {
                readonly get => (_unit >> 10) << (int)((_unit & (1U << 9)) >> 6);
                set
                {
                    if (value >= 1U << 29)
                    {
                        throw new InvalidOperationException("failed to modify unit: too large offset");
                    }

                    _unit &= (1U << 31) | (1U << 8) | 0xFF;

                    if (value < 1U << 21)
                    {
                        _unit |= value << 10;
                    }
                    else
                    {
                        _unit |= (value << 2) | (1U << 9);
                    }
                }
            }
        }

        internal struct DoubleArrayResultPair
        {
            public int Value { readonly get; set; }

            public int Length { readonly get; set; }
        }

        private sealed class DoubleArrayTrie
        {
            private readonly DoubleArrayUnit[] _array;

            public DoubleArrayTrie(DoubleArrayUnit[] preCompiledData)
            {
                _array = preCompiledData ?? throw new ArgumentNullException(nameof(preCompiledData));
            }

            public int CommonPrefixSearch(ReadOnlySpan<byte> key, Span<DoubleArrayResultPair> results, int nodePos = 0)
            {
                var numResults = 0;

                if ((uint)nodePos >= (uint)_array.Length)
                {
                    return 0;
                }

                var unit = _array[nodePos];
                nodePos ^= (int)unit.Offset;

                for (var index = 0; index < key.Length; index++)
                {
                    nodePos ^= key[index];
                    if ((uint)nodePos >= (uint)_array.Length)
                    {
                        return numResults;
                    }

                    unit = _array[nodePos];

                    if (unit.Label != key[index])
                    {
                        return numResults;
                    }

                    nodePos ^= (int)unit.Offset;

                    if (!unit.HasLeaf)
                    {
                        continue;
                    }

                    if (numResults < results.Length)
                    {
                        if ((uint)nodePos >= (uint)_array.Length)
                        {
                            return numResults;
                        }

                        results[numResults].Value = (int)_array[nodePos].Value;
                        results[numResults].Length = index + 1;
                    }

                    numResults++;
                }

                return numResults;
            }
        }
    }
}
