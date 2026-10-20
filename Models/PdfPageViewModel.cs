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

        public int PageNumber { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string FilePath { get; set; }

        // Store lightweight layout data instead of heavy UI control trees
        public List<PdfWordData> WordsData { get; set; } = new List<PdfWordData>();
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
