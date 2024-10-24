﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window {
        private LogWriter writer;

        public LogWindow(LogWriter logWriter) {
            InitializeComponent();

            writer = logWriter;

            LogBox.ItemsSource = writer.Log;
            //if (writer.Log.Count > 0)
                LogBox.ScrollIntoView(writer.Log.Last());

            ((INotifyCollectionChanged)LogBox.ItemsSource).CollectionChanged += (_, _) => LogBox.ScrollIntoView(writer.Log.Last());
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                CopyToClipboard();

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                writer.SaveAs();
        }

        private void CopyToClipboard() {
            StringBuilder sb = new();
            foreach (LogWriter.LogItem item in LogBox.Items) {
                if (item.Message.StartsWith("{"))
                    continue;
                sb.AppendLine($"[{item.Timestamp.ToString("HH:mm:ss")}] {item.Message}");
            }
            Clipboard.SetDataObject(sb.ToString());
        }
    }

    public class LogWriter : TextWriter {
        public class LogItem {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; }

            public LogItem(string message) {
                Timestamp = DateTime.Now;
                Message = RemoveTags(message).Trim();
            }
        }

        public ObservableCollection<LogItem> Log = new();

        public LogWriter() {
            Console.SetOut(this);
        }

        public override void WriteLine(string? value) {
            Application.Current.Dispatcher.Invoke(new Action(() => {
                if (Log.Count > 200) Log.RemoveAt(0);
                Log.Add(new LogItem(value!));
            }));
        }


        private string line = string.Empty;
        public override void Write(char value) {
            if (!value.Equals('\r') && !value.Equals('\n'))
                line += value;
            else {
                if (string.IsNullOrWhiteSpace(line)) {
                    line = string.Empty;
                    return;
                }
                if (line.Contains("Plex.ServerApi.Api.ApiService")) {
                    line = string.Empty;
                    return;
                }
                Application.Current.Dispatcher.Invoke(new Action(() => {
                    if (Log.Count > 200) 
                        Log.RemoveAt(0);

                    if (!string.IsNullOrWhiteSpace(line))
                        Log.Add(new LogItem(line));
                }));
                line = string.Empty;
            }
        }

        public override Encoding Encoding {
            get { return Encoding.UTF8; }
        }

        private static string RemoveTags(string text) {
            string[] hiddenTags = { "\"id\"", "uuid", "token", "identifier", "secret", "address", "host", "port" };

            if (hiddenTags.Any(c => text.Contains(c, StringComparison.OrdinalIgnoreCase))) {
                if (text.Trim().StartsWith('{')) {
                    List<string> splitText = text.Replace("{", "{,").Split(',').ToList();
                    splitText.RemoveAll(u => hiddenTags.Any(c => u.Contains(c, StringComparison.OrdinalIgnoreCase)));
                    text = string.Join(',', splitText).Replace("{,", "{");
                }
                else if (text.Trim().StartsWith('<')) {
                    List<string> splitText = text.Split().ToList();
                    splitText.RemoveAll(u => hiddenTags.Any(c => u.Contains(c, StringComparison.OrdinalIgnoreCase)));
                    text = string.Join(' ', splitText);
                }
            }
            return text;
        }

        public void SaveAs() {
            SaveFileDialog dlg = new() {
                FileName = "log",
                DefaultExt = ".txt",
                Filter = "Text (.txt)|*.txt"
            };

            if (dlg.ShowDialog() == true)
                File.WriteAllText(dlg.FileName, ToString());
        }

        public override string ToString() {
            StringBuilder sb = new();
            foreach (LogItem item in Log) {
                sb.AppendLine($"[{item.Timestamp.ToString("HH:mm:ss")}] {item.Message}");
            }
            return sb.ToString();
        }
    }
}
