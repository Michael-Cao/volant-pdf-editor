using pdf_editor2.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace pdf_editor2.Services
{
    public class PdfPageCache
    {
        // Thread-safe composite key tracking: "FilePath_PageNumber" -> PdfPageStructure
        private readonly ConcurrentDictionary<string, PdfPageStructure> _cacheMap = new();

        /// <summary>
        /// Attempts to fetch a cached page structure. Returns null if not found.
        /// </summary>
        public PdfPageStructure Get(string filePath, int pageNumber)
        {
            string cacheKey = GenerateKey(filePath, pageNumber);
            _cacheMap.TryGetValue(cacheKey, out var structure);
            return structure;
        }

        /// <summary>
        /// Inserts or updates a page structure in the thread-safe map.
        /// </summary>
        public void Put(string filePath, int pageNumber, PdfPageStructure structure)
        {
            if (structure == null) return;

            string cacheKey = GenerateKey(filePath, pageNumber);
            _cacheMap[cacheKey] = structure; // Atomic overwrite or add
        }

        /// <summary>
        /// Clears out memory mappings for closed documents.
        /// </summary>
        public void ClearDocumentCache(string filePath)
        {
            foreach (var key in _cacheMap.Keys)
            {
                if (key.StartsWith(filePath + "||", StringComparison.OrdinalIgnoreCase))
                {
                    _cacheMap.TryRemove(key, out _);
                }
            }
        }

        private string GenerateKey(string filePath, int pageNumber)
        {
            // Use a clean separator unlikely to exist inside file naming conventions
            return $"{filePath.ToLowerInvariant()}||{pageNumber}";
        }
    }
}
