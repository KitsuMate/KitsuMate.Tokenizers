using System.Collections.Generic;
using System.Linq;

using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    public class SequencePostProcessor : IPostProcessor
    {
        private readonly List<IPostProcessor> _processors;

        public SequencePostProcessor(List<IPostProcessor> processors)
        {
            _processors = processors;
        }

        public int AddedTokens(bool isPair)
        {
            return _processors.Sum(p => p.AddedTokens(isPair));
        }

        public EncodingResult Process(List<EncodingResult> encodings, bool addSpecialTokens)
        {
            var currentEncodings = encodings;
            foreach (var processor in _processors)
            {
                currentEncodings = new List<EncodingResult> { processor.Process(currentEncodings, addSpecialTokens) };
            }
            return currentEncodings.FirstOrDefault(); // Should only be one final encoding after sequential processing
        }
    }
}
