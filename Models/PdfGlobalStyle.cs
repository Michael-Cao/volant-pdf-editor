using System;
using System.Collections.Generic;
using System.Text;

namespace pdf_editor2.Models
{
    /// <summary>
    /// Represents a unique combination of typography metrics detected across the document context.
    /// </summary>
    public class PdfGlobalStyle : IEquatable<PdfGlobalStyle>
    {
        public string FontName { get; set; } = "Unknown";
        public double FontSize { get; set; }
        public string HexColor { get; set; } = "#333333";
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }

        // Formatted identifier string for UI binding drop-downs or selector panels
        public string DisplayName => $"{FontName}, {FontSize}pt ({HexColor}) " +
                                     $"{(IsBold ? "[Bold]" : "")} {(IsItalic ? "[Italic]" : "")}".Trim();

        public bool Equals(PdfGlobalStyle other)
        {
            if (other == null) return false;
            return FontName == other.FontName &&
                   Math.Abs(FontSize - other.FontSize) < 0.1 &&
                   HexColor == other.HexColor &&
                   IsBold == other.IsBold &&
                   IsItalic == other.IsItalic;
        }

        public override int GetHashCode()
        {
            // Grouping hashing elements cleanly to generate unified unique key indicators
            return HashCode.Combine(FontName, Math.Round(FontSize, 1), HexColor, IsBold, IsItalic);
        }

        public override bool Equals(object obj) => Equals(obj as PdfGlobalStyle);
    }
}
