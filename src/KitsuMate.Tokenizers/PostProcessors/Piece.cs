using System;
using KitsuMate.Tokenizers.PostProcessors;

namespace KitsuMate.Tokenizers.PostProcessors
{
    /// <summary>
    /// Represents the different kind of pieces that constitute a template.
    /// </summary>
    public class Piece
    {
        public Sequence? SequenceId { get; set; }
        public string SpecialTokenId { get; set; }
        public int TypeId { get; set; }

        public static Piece FromString(string s)
        {
            var parts = s.Split(':');
            string idPart = parts[0];
            int typeId = 0;

            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], out typeId))
                {
                    throw new ArgumentException($"Invalid type_id in piece string: {s}");
                }
            }

            if (idPart.StartsWith("$"))
            {
                var seqIdPart = idPart.Substring(1);
                Sequence seqId;
                switch (seqIdPart.ToLowerInvariant())
                {
                    case "":
                    case "a":
                    case "0":
                        seqId = Sequence.A;
                        break;
                    case "b":
                    case "1":
                        seqId = Sequence.B;
                        break;
                    default:
                        // If it's a number, it's a type_id for Sequence A
                        if (int.TryParse(seqIdPart, out int parsedTypeId))
                        {
                            seqId = Sequence.A;
                            typeId = parsedTypeId;
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid sequence identifier in piece string: {s}");
                        }
                        break;
                }
                return new Piece { SequenceId = seqId, TypeId = typeId };
            }
            else
            {
                return new Piece { SpecialTokenId = idPart, TypeId = typeId };
            }
        }
    }
}
