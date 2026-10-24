using System;
using System.Collections.Generic;
using System.Text;

namespace pdf_editor2.Models
{
    /// <summary>
    /// Reusable flyweight metadata container representing shared typographic metrics 
    /// and structural descriptor constraints extracted from a PDF font resource.
    /// </summary>
    public class PdfFontMetrics
    {
        /// <summary>
        /// Gets or sets the exact normalized PostScript descriptor identifier key (e.g., "Helvetica-Bold").
        /// </summary>
        public string FontName { get; set; } = "Segoe UI";

        /// <summary>
        /// Gets or sets the high-level typographic family name designator (e.g., "Helvetica").
        /// </summary>
        public string FontFamily { get; set; } = "Segoe UI";

        /// <summary>
        /// Gets or sets the numeric typographic weight scale value (typically 100 to 900).
        /// </summary>
        public int FontWeight { get; set; } = 400;

        /// <summary>
        /// Gets or sets the maximum distance above the baseline achieved by glyphs in this font.
        /// </summary>
        public double FontAscent { get; set; }

        /// <summary>
        /// Gets or sets the maximum distance below the baseline achieved by glyphs in this font.
        /// </summary>
        public double FontDescent { get; set; }

        /// <summary>
        /// Gets or sets the distance from the baseline to the top of flat capital characters.
        /// </summary>
        public double FontCapHeight { get; set; }

        /// <summary>
        /// Gets or sets the raw integer bitmask flags defining typeface structural traits.
        /// </summary>
        public int FontFlags { get; set; }

        /// <summary>
        /// Gets or sets the original unstripped subset font descriptor string (e.g., "AAAAAA+Helvetica-Bold").
        /// </summary>
        public string RawPostScriptName { get; set; } = string.Empty;

        public bool IsBold { get; set; }

        public bool IsItalic { get; set; }
    }
}
