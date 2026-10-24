using pdf_editor2.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

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


    public class PdfWordData : ViewModelBase
    {
        private string _text = string.Empty;
        private double _fontSize;
        private double _x;
        private double _y;
        private double _width;
        private double _height;
        private PdfObjectType _objectType = PdfObjectType.Text;

        // --- Core Parsing Structural Fields (Matching UglyToad.PdfPig) ---

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set { if (SetProperty(ref _fontSize, value)) OnPropertyChanged(nameof(Bounds)); }
        }

        public double X
        {
            get => _x;
            set { if (SetProperty(ref _x, value)) OnPropertyChanged(nameof(Bounds)); }
        }

        public double Y
        {
            get => _y;
            set { if (SetProperty(ref _y, value)) OnPropertyChanged(nameof(Bounds)); }
        }

        // Added missing dimension properties to resolve compiler definitions
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public PdfObjectType ObjectType
        {
            get => _objectType;
            set
            {
                if (SetProperty(ref _objectType, value))
                {
                    OnPropertyChanged(nameof(Icon));
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(Bounds));
                }
            }
        }

        // --- UI Dynamic Computed Auto Properties ---

        public string Icon => ObjectType switch
        {
            PdfObjectType.Image => "🖼️",
            PdfObjectType.Path => "📐",
            _ => "🔤"
        };

        public string Type => ObjectType switch
        {
            PdfObjectType.Image => "Raster Image",
            PdfObjectType.Path => "Vector Path",
            _ => "Text Primitive"
        };

        public string Bounds => $"[{Math.Round(X, 1)}, {Math.Round(Y, 1)}, Pt: {Math.Round(FontSize, 1)}]";
    }

    public class PdfObjectItem
    {
        public string Icon { get; set; } = "📄";
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Bounds { get; set; } = string.Empty;
    }
}
