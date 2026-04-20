using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace KitsuMate.Tokenizers.Decoders
{
    /// <summary>
    /// Metaspace decoder implementation.
    /// </summary>
    public class MetaspaceDecoder : IDecoder
    {
        private readonly string _replacement;
        private readonly string _prependScheme;

        public MetaspaceDecoder(string replacement, string prependScheme)
        {
            _replacement = replacement;
            _prependScheme = prependScheme;
        }

        public string Decode(IEnumerable<string> tokens)
        {
            var result = string.Join("", tokens);

            // Replace metaspace character with actual spaces
            result = result.Replace(_replacement, " ");

            // Handle prepend scheme
            var prependSchemeLower = _prependScheme?.ToLowerInvariant() ?? "always";
            if (prependSchemeLower == "always" && result.StartsWith(" "))
            {
                result = result.Substring(1);
            }

            return result;
        }
    }
}
