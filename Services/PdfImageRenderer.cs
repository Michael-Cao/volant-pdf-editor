using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using SkiaSharp;

namespace pdf_editor2.Services
{
    public static class PdfImageRenderer
    {
        /// <summary>
        /// On-Demand Dynamic Renderer. 
        /// Triggered exclusively when a specific page enters the UI viewport container.
        /// </summary>
        public static async Task<BitmapImage> RenderPageImageAsync(string filePath, int pageNumber, float scale = 1.5f)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        document.AddSkiaPageFactory();

                        using (SKBitmap skiaBitmap = document.GetPageAsSKBitmap(pageNumber, scale, SKColors.White))
                        using (SKImage skiaImage = SKImage.FromBitmap(skiaBitmap))
                        using (SKData encodedData = skiaImage.Encode(SKEncodedImageFormat.Png, 85))
                        using (Stream imageStream = encodedData.AsStream())
                        {
                            BitmapImage wpfBitmap = new BitmapImage();
                            wpfBitmap.BeginInit();
                            wpfBitmap.StreamSource = imageStream;
                            wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
                            wpfBitmap.EndInit();
                            wpfBitmap.Freeze(); // Freeze grants cross-thread UI consumption safety rules
                            return wpfBitmap;
                        }
                    }
                }
                catch
                {
                    return null; // Fallback placeholder asset can be returned here if execution fails
                }
            });
        }
    }
}
