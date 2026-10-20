using pdf_editor2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace pdf_editor2.Services
{
    public static class PdfStructureParser
    {
        /// <summary>
        /// Reads raw structural primitives from the PDF file byte boundaries (Header, XRef Table offset, and Trailer).
        /// </summary>
        public static PdfStructureInfo ParseLowLevelStructure(string filePath)
        {
            var info = new PdfStructureInfo
            {
                Header = "%PDF-1.7", // Safe structural baseline fallback
                XRefType = "Standard XRef Table",
                XRefOffset = 0,
                TrailerSize = 0
            };

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Extract Header (First line of document)
                    stream.Seek(0, SeekOrigin.Begin);
                    byte[] headerBytes = reader.ReadBytes(10);
                    string headerString = Encoding.ASCII.GetString(headerBytes);
                    if (headerString.StartsWith("%PDF"))
                    {
                        int endOfLine = headerString.IndexOf('\r');
                        if (endOfLine == -1) endOfLine = headerString.IndexOf('\n');
                        info.Header = endOfLine > 0 ? headerString.Substring(0, endOfLine) : headerString.Trim();
                    }

                    // Extract XRef and Trailer from the tail boundaries of the file allocation table
                    long length = stream.Length;
                    long seekOffset = Math.Max(0, length - 1024); // Look at last 1KB
                    stream.Seek(seekOffset, SeekOrigin.Begin);

                    byte[] tailBytes = reader.ReadBytes((int)(length - seekOffset));
                    string tailText = Encoding.ASCII.GetString(tailBytes);

                    // Track down the last structural offset mapping (startXREF)
                    int startXrefIdx = tailText.LastIndexOf("startXREF", StringComparison.OrdinalIgnoreCase);
                    if (startXrefIdx == -1) startXrefIdx = tailText.LastIndexOf("startxref", StringComparison.OrdinalIgnoreCase);

                    if (startXrefIdx != -1)
                    {
                        string remainder = tailText.Substring(startXrefIdx);
                        string[] lines = remainder.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 1 && int.TryParse(lines[1].Trim(), out int offsetValue))
                        {
                            info.XRefOffset = offsetValue;
                        }
                    }

                    // Identify if modern compressed cross-reference streams are used instead of tables
                    if (tailText.Contains("/XRef") || tailText.Contains("ObjStm"))
                    {
                        info.XRefType = "Compressed Cross-Reference Stream (ObjStm)";
                    }

                    // Extract Trailer sizing parameter footprints if visible in plain-text tail layout
                    int trailerIdx = tailText.LastIndexOf("trailer", StringComparison.OrdinalIgnoreCase);
                    if (trailerIdx != -1)
                    {
                        int sizeIdx = tailText.IndexOf("/Size", trailerIdx, StringComparison.OrdinalIgnoreCase);
                        if (sizeIdx != -1)
                        {
                            var sizeSegment = tailText.Substring(sizeIdx, Math.Min(30, tailText.Length - sizeIdx));
                            var numbers = new string(sizeSegment.Where(char.IsDigit).ToArray());
                            if (int.TryParse(numbers, out int parsedSize))
                            {
                                info.TrailerSize = parsedSize;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Soft structural safety recovery: leave defaults untouched
            }

            return info;
        }
    }
}
