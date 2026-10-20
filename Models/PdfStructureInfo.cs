using System;
using System.Collections.Generic;
using System.Text;

namespace pdf_editor2.Models
{
    public record PdfIndirectRef(int ObjectNumber, int Generation);

    // Added explicit public modifier
    public record PdfPageGeometry(double[] MediaBox, double[] CropBox);

    public class PdfPageStructure
    {
        public PdfPageGeometry Geometry { get; set; }
        public byte[] CombinedContentStream { get; set; }
        public Dictionary<string, object> ResolvedResources { get; set; } = new();
    }

    public class PdfStructureInfo
    {
        public string Header { get; set; }           // e.g., "%PDF-1.7"
        public int TotalObjectsCount { get; set; }    // Number of indirect objects in Document
        public string XRefType { get; set; }          // e.g., "Table" or "Stream"
        public int XRefOffset { get; set; }          // Byte offset of the cross-reference section
        public int TrailerSize { get; set; }          // Size value extracted from the trailer dictionary
    }
}
