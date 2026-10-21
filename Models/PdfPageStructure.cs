using System;
using System.Collections.Generic;
using System.Text;

namespace pdf_editor2.Models
{
    /// <summary>
    /// A decoupled domain model representing the completely parsed 
    /// layout structure and background image data of a single PDF page.
    /// </summary>
    public class PdfPageStructure
    {
        /// <summary>
        /// Gets or sets the collection of individual text layout coordinates parsed from the page.
        /// </summary>
        public List<PdfWordData> WordsData { get; set; } = new List<PdfWordData>();

        /// <summary>
        /// Gets or sets the raw, compressed PNG image bytes of the rendered page background canvas.
        /// Storing raw bytes instead of a 'BitmapImage' keeps this class decoupled from the WPF UI thread,
        /// allowing it to be safely stored in the 'PdfPageCache' across background threads.
        /// </summary>
        public byte[] RawPngBytes { get; set; }

        /// <summary>
        /// Optional: If your code uses a secondary hierarchical info block, you can track it here.
        /// </summary>
        public PdfStructureInfo StructuralDetails { get; set; }
    }
}
