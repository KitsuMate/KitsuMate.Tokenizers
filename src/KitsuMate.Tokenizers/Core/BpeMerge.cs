namespace KitsuMate.Tokenizers.Core
{
    /// <summary>
    /// Represents a single ordered BPE merge rule.
    /// </summary>
    public sealed class BpeMerge
    {
        public BpeMerge(string left, string right, int rank)
        {
            Left = left;
            Right = right;
            Rank = rank;
        }

        public string Left { get; }

        public string Right { get; }

        public int Rank { get; }
    }
}