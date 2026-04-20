using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// Sequence decoder implementation.
    /// </summary>
    public class SequenceDecoder : IDecoder
    {
        private readonly List<IDecoder> _decoders;

        public SequenceDecoder(List<DecoderConfig> decoderConfigs, Func<DecoderConfig, IDecoder> decoderFactory)
        {
            _decoders = new List<IDecoder>();
            if (decoderConfigs != null && decoderFactory != null)
            {
                foreach (var config in decoderConfigs)
                {
                    _decoders.Add(decoderFactory(config)); // Use the factory to create inner decoders
                }
            }
        }

        public string Decode(IEnumerable<string> tokens)
        {
            var currentText = string.Join("", tokens);

            foreach (var decoder in _decoders)
            {
                currentText = decoder.Decode(new[] { currentText });
            }

            return currentText;
        }
    }
}
