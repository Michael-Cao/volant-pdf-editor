using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
        public Canvas ComponentCanvas { get; set; }

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
