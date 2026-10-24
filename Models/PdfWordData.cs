using System;

namespace pdf_editor2.Models
{
    /// <summary>
    /// Specifies the high-level visual primitive classification layout type.
    /// </summary>
    public enum PdfObjectType
    {
        Text,
        Image,
        Path
    }

    /// <summary>
    /// Lightweight, high-performance data model representing a single primitive item extracted from a PDF page stream.
    /// </summary>
    public class PdfWordData
    {
        // --- Core Parsing Structural Fields (Populated directly via UglyToad.PdfPig) ---
        public string Text { get; set; } = string.Empty;
        public double FontSize { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        /// <summary>
        /// Gets or sets the classification layout variant of the parsed primitive asset.
        /// </summary>
        public PdfObjectType ObjectType { get; set; } = PdfObjectType.Text;
    }
}
