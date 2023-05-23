using System.Diagnostics;
using System.Media;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;
using Newtonsoft.Json;

namespace DyviniaUtils.Dialogs {
    /// <summary>
    /// Interaction logic for UpdateDialog.xaml
    /// </summary>
    public partial class UpdateDialog : Window {
        private bool result = false;

        public UpdateDialog(string repoAuthor, string repoName) {
            InitializeComponent();

            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "request");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.html");
            dynamic github = JsonConvert.DeserializeObject<dynamic>(client.GetStringAsync($"https://api.github.com/repos/{repoAuthor}/{repoName}/releases/latest").Result)!;
            string htmlString = $"<head><style>body{{overflow:hidden; background-color: #141414; color: rgb(230, 237, 243); font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", \"Noto Sans\", Helvetica, Arial, sans-serif, \"Apple Color Emoji\", \"Segoe UI Emoji\"}} a{{color: rgb(220, 220, 220);}}</style></head><body><h1>{github.name}</h1> {github.body_html} </body>";
            
            Browser.NavigateToString(htmlString);
            Title += $" {repoName}";
            Owner = Application.Current.MainWindow;

            InstallButton.Click += OnClose;
            IgnoreButton.Click += OnClose;

            WebpageButton.Click += (_, _) => Process.Start(new ProcessStartInfo($"https://github.com/{repoAuthor}/{repoName}/releases/latest") { UseShellExecute = true });

            SystemSounds.Exclamation.Play();
        }

        public static bool Show(string repoAuthor, string repoName) {
            bool result = false;
            Application.Current.Dispatcher.Invoke(() => {
                UpdateDialog window = new(repoAuthor, repoName);
                window.ShowDialog();
                result = window.result;
            });
            return result;
        }

        private void OnClose(object sender, RoutedEventArgs e) {
            if (sender == InstallButton)
                result = true;
            else
                result = false;
            Close();
        }

        private void Browser_Navigating(object sender, NavigatingCancelEventArgs e) {
            if (e.Uri is null) return;

            if (e.Uri.ToString().Contains("http")) {
                e.Cancel = true;
                Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
            }
        }
    }
}
