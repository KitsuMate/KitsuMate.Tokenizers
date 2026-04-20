using System;
using System.Collections.Generic;
using System.Linq;

namespace KitsuMate.Tokenizers
{
    /// <summary>
    /// Configuration for special tokens used in tokenization.
    /// Matches HuggingFace tokenizers special tokens management.
    /// </summary>
    public class SpecialTokensConfig
    {
        /// <summary>
        /// Beginning of sequence token
        /// </summary>
        public AddedToken? BosToken { get; set; }

        /// <summary>
        /// End of sequence token
        /// </summary>
        public AddedToken? EosToken { get; set; }

        /// <summary>
        /// Padding token
        /// </summary>
        public AddedToken? PadToken { get; set; }

        /// <summary>
        /// Classification token (used in BERT)
        /// </summary>
        public AddedToken? ClsToken { get; set; }

        /// <summary>
        /// Separator token (used in BERT)
        /// </summary>
        public AddedToken? SepToken { get; set; }

        /// <summary>
        /// Mask token (used for masked language modeling)
        /// </summary>
        public AddedToken? MaskToken { get; set; }

        /// <summary>
        /// Unknown token
        /// </summary>
        public AddedToken? UnkToken { get; set; }

        /// <summary>
        /// Additional special tokens beyond the standard set
        /// </summary>
        public List<AddedToken> AdditionalSpecialTokens { get; set; } = new List<AddedToken>();

        /// <summary>
        /// Creates an empty special tokens configuration
        /// </summary>
        public SpecialTokensConfig() { }

        /// <summary>
        /// Adds special tokens from a dictionary mapping token names to token content.
        /// Supported keys: bos_token, eos_token, pad_token, cls_token, sep_token, mask_token, unk_token, additional_special_tokens
        /// </summary>
        /// <param name="specialTokens">Dictionary of special token names to values</param>
        public void AddSpecialTokens(Dictionary<string, object> specialTokens)
        {
            if (specialTokens == null) return;

            foreach (var kvp in specialTokens)
            {
                var key = kvp.Key.ToLowerInvariant();
                var value = kvp.Value;

                // Handle the value which could be a string or an AddedToken object
                AddedToken? token = null;
                if (value is string str)
                {
                    token = new AddedToken { Content = str, Special = true };
                }
                else if (value is AddedToken addedToken)
                {
                    token = addedToken;
                }

                if (token == null) continue;

                switch (key)
                {
                    case "bos_token":
                        BosToken = token;
                        break;
                    case "eos_token":
                        EosToken = token;
                        break;
                    case "pad_token":
                        PadToken = token;
                        break;
                    case "cls_token":
                        ClsToken = token;
                        break;
                    case "sep_token":
                        SepToken = token;
                        break;
                    case "mask_token":
                        MaskToken = token;
                        break;
                    case "unk_token":
                        UnkToken = token;
                        break;
                    case "additional_special_tokens":
                        if (value is IEnumerable<string> strList)
                        {
                            foreach (var item in strList)
                            {
                                AdditionalSpecialTokens.Add(new AddedToken { Content = item, Special = true });
                            }
                        }
                        else if (value is IEnumerable<AddedToken> tokenList)
                        {
                            AdditionalSpecialTokens.AddRange(tokenList);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Adds regular tokens (not special tokens) to the additional tokens list
        /// </summary>
        /// <param name="tokens">List of token strings to add</param>
        public void AddTokens(List<string> tokens)
        {
            if (tokens == null) return;

            foreach (var token in tokens)
            {
                AdditionalSpecialTokens.Add(new AddedToken 
                { 
                    Content = token, 
                    Special = false 
                });
            }
        }

        /// <summary>
        /// Gets all special tokens as a dictionary (token ID to content)
        /// </summary>
        public Dictionary<int, string> GetAllSpecialTokens()
        {
            var tokens = new Dictionary<int, string>();
            
            void AddIfPresent(AddedToken? token)
            {
                if (token != null && token.Id >= 0)
                {
                    tokens[token.Id] = token.Content;
                }
            }

            AddIfPresent(BosToken);
            AddIfPresent(EosToken);
            AddIfPresent(PadToken);
            AddIfPresent(ClsToken);
            AddIfPresent(SepToken);
            AddIfPresent(MaskToken);
            AddIfPresent(UnkToken);

            foreach (var token in AdditionalSpecialTokens)
            {
                if (token.Id >= 0)
                {
                    tokens[token.Id] = token.Content;
                }
            }

            return tokens;
        }

        /// <summary>
        /// Gets all special token contents as a list
        /// </summary>
        public List<string> GetAllSpecialTokenContents()
        {
            var contents = new List<string>();
            
            void AddIfPresent(AddedToken? token)
            {
                if (token != null && !string.IsNullOrEmpty(token.Content))
                {
                    contents.Add(token.Content);
                }
            }

            AddIfPresent(BosToken);
            AddIfPresent(EosToken);
            AddIfPresent(PadToken);
            AddIfPresent(ClsToken);
            AddIfPresent(SepToken);
            AddIfPresent(MaskToken);
            AddIfPresent(UnkToken);

            contents.AddRange(AdditionalSpecialTokens
                .Where(t => !string.IsNullOrEmpty(t.Content))
                .Select(t => t.Content));

            return contents.Distinct().ToList();
        }

        /// <summary>
        /// Creates a special tokens config from tokenizer.json added tokens
        /// </summary>
        public static SpecialTokensConfig FromAddedTokens(List<AddedToken> addedTokens, TokenizerConfig config)
        {
            var specialTokens = new SpecialTokensConfig();

            if (addedTokens == null) return specialTokens;

            // Helper to find token by content
            AddedToken? FindToken(string? content)
            {
                if (string.IsNullOrEmpty(content)) return null;
                return addedTokens.FirstOrDefault(t => t.Content == content);
            }

            // Map standard tokens from config
            specialTokens.BosToken = FindToken(config?.BosToken);
            specialTokens.EosToken = FindToken(config?.EosToken);
            specialTokens.PadToken = FindToken(config?.PadToken);
            specialTokens.ClsToken = FindToken(config?.ClsToken);
            specialTokens.SepToken = FindToken(config?.SepToken);
            specialTokens.MaskToken = FindToken(config?.MaskToken);
            specialTokens.UnkToken = FindToken(config?.UnkToken);

            // If config didn't specify certain tokens, try to infer from common patterns
            // RoBERTa/XLM-RoBERTa style: <s> is BOS, </s> is EOS
            if (specialTokens.BosToken == null)
            {
                specialTokens.BosToken = FindToken("<s>");
            }
            if (specialTokens.EosToken == null)
            {
                specialTokens.EosToken = FindToken("</s>");
            }
            // BERT style: [CLS] is CLS, [SEP] is SEP
            if (specialTokens.ClsToken == null)
            {
                specialTokens.ClsToken = FindToken("[CLS]");
            }
            if (specialTokens.SepToken == null)
            {
                specialTokens.SepToken = FindToken("[SEP]");
            }
            // Common patterns for other tokens
            if (specialTokens.PadToken == null)
            {
                specialTokens.PadToken = FindToken("<pad>") ?? FindToken("[PAD]");
            }
            if (specialTokens.MaskToken == null)
            {
                specialTokens.MaskToken = FindToken("<mask>") ?? FindToken("[MASK]");
            }
            if (specialTokens.UnkToken == null)
            {
                specialTokens.UnkToken = FindToken("<unk>") ?? FindToken("[UNK]");
            }

            // Add remaining special tokens to additional list
            var standardTokenContents = new HashSet<string>();
            void AddContent(string? content)
            {
                if (!string.IsNullOrEmpty(content)) standardTokenContents.Add(content);
            }

            AddContent(config?.BosToken);
            AddContent(config?.EosToken);
            AddContent(config?.PadToken);
            AddContent(config?.ClsToken);
            AddContent(config?.SepToken);
            AddContent(config?.MaskToken);
            AddContent(config?.UnkToken);

            specialTokens.AdditionalSpecialTokens = addedTokens
                .Where(t => t.Special && !standardTokenContents.Contains(t.Content))
                .ToList();

            return specialTokens;
        }
    }
}
