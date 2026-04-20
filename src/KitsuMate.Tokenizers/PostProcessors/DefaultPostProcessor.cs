using System.Collections.Generic;

using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    public class DefaultPostProcessor : IPostProcessor
    {
        public int AddedTokens(bool isPair) => 0;

        public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
        {
            if (encodings.Count == 0) return new EncodingResult();
            if (encodings.Count == 1) return encodings[0];
            return EncodingResult.Merge(encodings, false);
        }
    }
}
