using System.Collections.Generic;

namespace KitsuMate.Tokenizers.PostProcessors
{
    public interface IPostProcessor
    {
        int AddedTokens(bool isPair);
        EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens);
    }
}
