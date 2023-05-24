using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window {

        public LogWindow(LogWriter logWriter) {
            InitializeComponent();
            LogBox.ItemsSource = logWriter.Log;
            LogBox.ScrollIntoView(logWriter.Log.Last());
        }
    }

    public class LogWriter : TextWriter {
        public class LogItem {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }

            public LogItem(string message) {
                Timestamp = DateTime.Now;
                Message = message;
            }
        }

        public ObservableCollection<LogItem> Log = new();

        public LogWriter() {
            Console.SetOut(this);
        }

        public override void WriteLine(string? value) {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                if (Log.Count > 50) Log.RemoveAt(0);
                Log.Add(new LogItem(value!.Trim()));
            }));
        }


        private string line = String.Empty;
        public override void Write(char value) {
            if (!value.Equals('\r') && !value.Equals('\n'))
                line += value;
            else {
                if (String.IsNullOrWhiteSpace(line)) {
                    line = String.Empty;
                    return;
                }
                if (line.Contains("Plex.ServerApi.Api.ApiService")) {
                    line = String.Empty;
                    return;
                }
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (Log.Count > 50) Log.RemoveAt(0);
                    Log.Add(new LogItem(line.Trim()));
                }));
                line = String.Empty;
            }
        }

        public override Encoding Encoding {
            get { return Encoding.UTF8; }
        }
    }
}
