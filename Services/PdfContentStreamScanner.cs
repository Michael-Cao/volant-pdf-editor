using Microsoft.Extensions.Logging;
using pdf_editor2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace pdf_editor2.Services
{
    internal static class PdfContentStreamScanner
    {
        public static List<PdfWordData> ExtractObjectsFromPage(string filePath, int pageIndex, ILogger logger)
        {
            var extractedObjects = new List<PdfWordData>();
            logger.LogInformation("[Scanner] Starting object extraction for file: {FilePath}, target index: {PageIndex}", filePath, pageIndex);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream, Encoding.ASCII))
                {
                    string content = reader.ReadToEnd();
                    logger.LogInformation("[Scanner] File loaded into memory. Total raw string length: {Length} characters.", content.Length);

                    int currentIndex = 0;
                    var pageObjectOffsets = new List<int>();

                    while ((currentIndex = content.IndexOf("obj", currentIndex, StringComparison.Ordinal)) != -1)
                    {
                        int startOfObj = Math.Max(0, currentIndex - 50);
                        int endOfObj = content.IndexOf("endobj", currentIndex, StringComparison.Ordinal);

                        if (endOfObj != -1)
                        {
                            string objectSegment = content.Substring(startOfObj, endOfObj - startOfObj);
                            if (objectSegment.Contains("/Type /Page") || objectSegment.Contains("/Type/Page"))
                            {
                                pageObjectOffsets.Add(startOfObj);
                            }
                        }
                        currentIndex += 3;
                    }

                    logger.LogInformation("[Scanner] Structural evaluation complete. Found {Count} total individual /Type /Page objects.", pageObjectOffsets.Count);

                    if (pageObjectOffsets.Count > 0 && pageIndex >= 0 && pageIndex < pageObjectOffsets.Count)
                    {
                        int targetPageOffset = pageObjectOffsets[pageIndex];
                        int endOfPageBlock = content.IndexOf("endobj", targetPageOffset, StringComparison.Ordinal);
                        string pageBlockText = content.Substring(targetPageOffset, endOfPageBlock - targetPageOffset);

                        ParseContentStreams(content, pageBlockText, extractedObjects, logger);
                    }
                    else
                    {
                        logger.LogWarning("[Scanner] Requested page index {PageIndex} is out of bounds or no page definitions found.", pageIndex);
                    }

                    if (extractedObjects.Count == 0)
                    {
                        logger.LogWarning("[Scanner] Content stream parsing returned 0 objects. Executing global structural fallbacks scanning layout dictionary tokens.");
                        ParseGlobalVisualFallbacks(content, extractedObjects, logger);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Scanner] Critical error encountered during low-level file parsing runtime execution.");
            }

            logger.LogInformation("[Scanner] Extraction finished. Returning {Count} objects to the UI collection state controller.", extractedObjects.Count);
            return extractedObjects;
        }

        private static void ParseContentStreams(string globalContent, string pageBlockText, List<PdfWordData> outputList, ILogger logger)
        {
            int contentsIdx = pageBlockText.IndexOf("/Contents", StringComparison.Ordinal);
            if (contentsIdx == -1)
            {
                logger.LogWarning("[Scanner] Target Page dictionary does not contain an explicit plain-text '/Contents' reference identifier pointer.");
                return;
            }

            string segment = pageBlockText.Substring(contentsIdx, Math.Min(100, pageBlockText.Length - contentsIdx));
            logger.LogInformation("[Scanner] Extracted /Contents declaration context block segment: \"{Segment}\"", segment.Replace("\r", " ").Replace("\n", " "));

            var numbers = segment.Split(new[] { ' ', '[', ']', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                 .FirstOrDefault(x => x.All(char.IsDigit));

            if (!string.IsNullOrEmpty(numbers))
            {
                string targetObjHeader = $"{numbers} 0 obj";
                logger.LogInformation("[Scanner] Extracted Indirect Content object token identifier key: \"{Key}\". Seeking matching resource header segment...", targetObjHeader);

                int streamStart = globalContent.IndexOf(targetObjHeader, StringComparison.Ordinal);
                if (streamStart != -1)
                {
                    int streamDataIdx = globalContent.IndexOf("stream", streamStart, StringComparison.Ordinal);
                    int streamEndIdx = globalContent.IndexOf("endstream", streamStart, StringComparison.Ordinal);

                    if (streamDataIdx != -1 && streamEndIdx != -1)
                    {
                        logger.LogInformation("[Scanner] Indirect Stream block markers discovered. Isolating inner operator data payload ranges.");
                        string rawStream = globalContent.Substring(streamDataIdx, streamEndIdx - streamDataIdx);
                        ParseRawOperators(rawStream, outputList, logger);
                    }
                    else
                    {
                        logger.LogWarning("[Scanner] Indirect reference found, but binary 'stream' or 'endstream' wrapper tags are malformed or missing.");
                    }
                }
                else
                {
                    logger.LogWarning("[Scanner] Failed to find object declaration space for '{TargetKey}' inside global layout buffer array boundaries.", targetObjHeader);
                }
            }
            else
            {
                logger.LogWarning("[Scanner] Could not clear down a valid structural numeric reference string from current content metadata segment properties.");
            }
        }

        private static void ParseRawOperators(string rawStream, List<PdfWordData> outputList, ILogger logger)
        {
            string[] tokens = rawStream.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            logger.LogInformation("[Scanner] Parsing raw data stream fragment payload tokenizer blocks. Total raw split token entities: {Count}", tokens.Length);

            // Graphic state tracking simulation coordinates
            double activeX = 50.0;
            double activeY = 700.0;
            double activeFontSize = 11.0;
            int textPrimitivesCount = 0;
            int graphicObjectsCount = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                // Handle font assignment operator (e.g., /F1 12 Tf)
                if (tokens[i] == "Tf" && i >= 1)
                {
                    if (double.TryParse(tokens[i - 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedSize))
                    {
                        activeFontSize = parsedSize;
                    }
                }

                // Handle text matrix placement definitions (e.g., 1 0 0 1 X Y Tm)
                if (tokens[i] == "Tm" && i >= 6)
                {
                    if (double.TryParse(tokens[i - 2], NumberStyles.Any, CultureInfo.InvariantCulture, out double tmX) &&
                        double.TryParse(tokens[i - 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double tmY))
                    {
                        activeX = tmX;
                        activeY = tmY;
                    }
                }

                // Handle relative structural displacement offsets (e.g., X Y Td)
                if (tokens[i] == "Td" && i >= 2)
                {
                    if (double.TryParse(tokens[i - 2], NumberStyles.Any, CultureInfo.InvariantCulture, out double tdX) &&
                        double.TryParse(tokens[i - 1], NumberStyles.Any, CultureInfo.InvariantCulture, out double tdY))
                    {
                        activeX += tdX;
                        activeY += tdY;
                    }
                }

                // Handle standard literal string rendering operations (e.g., (Hello World) Tj)
                if (tokens[i] == "Tj" && i >= 1)
                {
                    string targetToken = tokens[i - 1];
                    if (targetToken.StartsWith("(") && targetToken.EndsWith(")"))
                    {
                        string cleanText = targetToken.Substring(1, targetToken.Length - 2);
                        if (!string.IsNullOrWhiteSpace(cleanText))
                        {
                            outputList.Add(new PdfWordData
                            {
                                ObjectType = PdfObjectType.Text,
                                Text = cleanText,
                                FontSize = activeFontSize,
                                X = activeX,
                                Y = activeY,
                                Width = cleanText.Length * (activeFontSize * 0.5), // Estimate character dimensions box layout
                                Height = activeFontSize
                            });
                            textPrimitivesCount++;
                            activeX += cleanText.Length * (activeFontSize * 0.5);
                        }
                    }
                }

                // Handle named external graphic object resource executions (e.g., /Image1 Do)
                if (tokens[i] == "Do" && i >= 1)
                {
                    outputList.Add(new PdfWordData
                    {
                        ObjectType = PdfObjectType.Image,
                        Text = $"External Graphic Object Resource Token (Marker Offset: {i})",
                        FontSize = 0.0,
                        X = activeX,
                        Y = activeY,
                        Width = 100.0,
                        Height = 100.0
                    });
                    graphicObjectsCount++;
                }
            }

            logger.LogInformation("[Scanner] Token loop parser execution sequence closed down. Extracted {TextCount} strings, {ImgCount} external graphics assets.", textPrimitivesCount, graphicObjectsCount);
        }

        private static void ParseGlobalVisualFallbacks(string content, List<PdfWordData> outputList, ILogger logger)
        {
            // Detect plain text image footprint tags inside uncompressed metadata segments
            if (content.Contains("/XObject") || content.Contains("/Subtype /Image"))
            {
                logger.LogInformation("[Scanner] Global fallback hook detected signature footprint patterns matching embedded visual media items (/XObject).");
                outputList.Add(new PdfWordData
                {
                    ObjectType = PdfObjectType.Image,
                    Text = "Embedded Image Object Payload",
                    X = 0.0,
                    Y = 0.0,
                    Width = 200.0,
                    Height = 200.0
                });
            }

            // Detect dictionary layout typography configurations globally
            if (content.Contains("/Font"))
            {
                logger.LogInformation("[Scanner] Global fallback hook detected active font dictionaries (/Font) across document asset layout boundaries.");
                outputList.Add(new PdfWordData
                {
                    ObjectType = PdfObjectType.Text,
                    Text = "Document Typography Asset Map",
                    FontSize = 12.0,
                    X = 0.0,
                    Y = 0.0,
                    Width = 150.0,
                    Height = 12.0
                });
            }
        }
    }
}
