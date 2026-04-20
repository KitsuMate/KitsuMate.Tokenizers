using System.Collections.Generic;
using System.Linq;
using KitsuMate.Tokenizers.PostProcessors; // Add this using statement

namespace KitsuMate.Tokenizers.PostProcessors
{
    public class ByteLevelPostProcessor : IPostProcessor
    {
        private readonly bool _addPrefixSpace;
        private readonly bool _trimOffsets;
        private readonly bool _useRegex;

        public ByteLevelPostProcessor(bool addPrefixSpace, bool trimOffsets, bool useRegex)
        {
            _addPrefixSpace = addPrefixSpace;
            _trimOffsets = trimOffsets;
            _useRegex = useRegex;
        }

        public int AddedTokens(bool isPair)
        {
            return 0;
        }

        public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
        {
            if (_trimOffsets)
            {
                foreach (var encoding in encodings)
                {
                    PostProcessorUtils.ProcessOffsets(encoding, _addPrefixSpace);
                    foreach (var overflowingEncoding in encoding.Overflowing)
                    {
                        PostProcessorUtils.ProcessOffsets(overflowingEncoding, _addPrefixSpace);
                    }
                }
            }

            // Set sequence IDs
            for (int i = 0; i < encodings.Count; i++)
            {
                encodings[i].SetSequenceId(i);
            }

            if (!addSpecialTokens)
            {
                return EncodingResult.Merge(encodings, false);
            }

            // ByteLevel post-processor doesn't add special tokens itself,
            // it just handles offset trimming and sequence IDs.
            // So, if addSpecialTokens is true, it means some other processor
            // (like TemplateProcessing) will handle it.
            // For now, we'll just merge them.
            return EncodingResult.Merge(encodings, false);
        }
    }
}
