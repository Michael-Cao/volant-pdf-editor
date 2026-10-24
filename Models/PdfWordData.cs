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

        /// <summary>
        /// Gets or sets the native WPF image source used to render real visual data when ObjectType is Image.
        /// </summary>
        public byte[]? ImageDataPayload { get; set; }

        /// <summary>
        /// Gets or sets the hexadecimal color string for text rendering (e.g., "#007ACC").
        /// </summary>
        public string ForegroundColor { get; set; } = "#000000";

        /// <summary>
        /// Gets or sets the shared typographic styling asset container reference.
        /// Points to a single allocated instance memory heap shared across all matching words.
        /// </summary>
        public PdfFontMetrics? StyleMetrics { get; set; }
    }
}
