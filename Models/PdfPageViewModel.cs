using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using pdf_editor2.Services;

namespace pdf_editor2.Models
{
    public class PdfPageViewModel : INotifyPropertyChanged
    {
        private BitmapImage _backgroundImage;
        private bool _isRendered;
        private List<PdfWordData> _wordsData = new List<PdfWordData>();
        private List<PdfWordData> _pathsData = new List<PdfWordData>();
        private List<PdfWordData> _imagesData = new List<PdfWordData>();

        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the collection of structural text words or paragraph blocks extracted on this page.
        /// </summary>
        public List<PdfWordData> WordsData
        {
            get => _wordsData;
            set { _wordsData = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the collection of raw vector graphics paths and geometric lines on this page.
        /// </summary>
        public List<PdfWordData> PathsData
        {
            get => _pathsData;
            set { _pathsData = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the collection of embedded bitmap images extracted inline from the page structure.
        /// </summary>
        public List<PdfWordData> ImagesData
        {
            get => _imagesData;
            set { _imagesData = value; OnPropertyChanged(); }
        }

        public PdfStructureInfo Structure { get; set; }
        public PdfPageStructure PageStructure { get; set; }

        public bool IsRendered
        {
            get => _isRendered;
            set { _isRendered = value; OnPropertyChanged(); }
        }

        public BitmapImage BackgroundImage
        {
            get => _backgroundImage;
            set { _backgroundImage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
