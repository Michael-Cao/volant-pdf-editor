using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;

namespace pdf_editor2.ViewModels
{
    public class DiagnosticsViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private readonly Queue<(DateTime Timestamp, double Value)> _memoryHistory = new();
        private readonly Process _currentProcess;

        private string _currentMemoryUsage = "0 MB";
        private string _memoryUsage5MinWindow = "0 MB";
        private List<double> _memoryHistoryValues = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public string CurrentMemoryUsage
        {
            get => _currentMemoryUsage;
            set => SetProperty(ref _currentMemoryUsage, value);
        }

        public string MemoryUsage5MinWindow
        {
            get => _memoryUsage5MinWindow;
            set => SetProperty(ref _memoryUsage5MinWindow, value);
        }

        public List<double> MemoryHistoryValues
        {
            get => _memoryHistoryValues;
            private set => SetProperty(ref _memoryHistoryValues, value);
        }

        public DiagnosticsViewModel()
        {
            _currentProcess = Process.GetCurrentProcess();

            // Check and refresh memory metrics every 2 seconds
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Run an immediate initial check
            UpdateMemoryMetrics();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateMemoryMetrics();
        }

        private void UpdateMemoryMetrics()
        {
            // Refresh process details to get the latest counters
            _currentProcess.Refresh();

            // FIX: Changed from PrivateWorkingSet64 to WorkingSet64
            double currentMb = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            CurrentMemoryUsage = $"{currentMb:F1} MB";

            // Add current tracking slice to the history queue
            DateTime now = DateTime.Now;
            _memoryHistory.Enqueue((now, currentMb));

            // Purge history entries older than 5 minutes
            DateTime limit = now.AddMinutes(-5);
            while (_memoryHistory.Count > 0 && _memoryHistory.Peek().Timestamp < limit)
            {
                _memoryHistory.Dequeue();
            }

            // Extract the peak maximum value seen within this rolling window
            double maxMb = _memoryHistory.Any() ? _memoryHistory.Max(entry => entry.Value) : currentMb;
            MemoryUsage5MinWindow = $"{maxMb:F1} MB";

            MemoryHistoryValues = _memoryHistory.Select(entry => entry.Value).ToList();
        }

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
