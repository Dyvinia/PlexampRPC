using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DiscordRPC;
using Hardcodet.Wpf.TaskbarNotification;
using Plex.ServerApi.PlexModels.Account;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private static readonly HttpClient httpClient = new();

        public Uri Address {  
            get {
                if (!String.IsNullOrEmpty(Config.Settings.PlexAddress))
                    return new UriBuilder(Config.Settings.PlexAddress).Uri;
                else
                    return ((AccountServer)UserServerComboBox.SelectedItem).Uri;
            }
        }

        public class PresenceData {
            public string? Line1 { get; set; }
            public string? Line2 { get; set; }
            public string? ImageTooltip { get; set; }
            public string ArtLink { get; set; } = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png";
            public string? State { get; set; }
            public int TimeOffset { get; set; }
        }

        public MainWindow() {
            InitializeComponent();

            DataContext = Config.Settings;
            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);

            StateChanged += (_, _) => {
                if (WindowState == WindowState.Minimized) {
                    Hide();
                    TrayIcon.ShowBalloonTip(null, "Minimized to Tray", BalloonIcon.None);
                }
            };

            ResetPresence();
            SetupTray();
        }

        public TaskbarIcon TrayIcon = new() {
            ToolTipText = "PlexampRPC",
            MenuActivation = PopupActivationMode.LeftOrRightClick
        };

        private void SetupTray() {
            TrayIcon.IconSource = Icon;
            ContextMenu contextMenu = new();
            MenuItem menuShow = new() { Header = "Show PlexampRPC" };
            MenuItem menuExit = new() { Header = "Exit PlexampRPC" };

            TrayIcon.TrayMouseDoubleClick += (_, _) => {
                Show();
                WindowState = WindowState.Normal;
            };
            menuShow.Click += (_, _) => {
                Show();
                WindowState = WindowState.Normal;
            };
            menuExit.Click += (_, _) => {
                Application.Current.Shutdown();
            };

            contextMenu.Items.Add(menuShow);
            contextMenu.Items.Add(menuExit);
            TrayIcon.ContextMenu = contextMenu;
        }

        public void GetAccountInfo() {
            UserIcon.Source = new BitmapImage(new Uri(App.Account?.Thumb ?? "/Resources/PlexIcon.png"));
            UserNameText.Text = App.Account?.Title ?? App.Account?.Username ?? "Name";

            if (String.IsNullOrEmpty(Config.Settings.PlexAddress)) {
                UserServerComboBox.ItemsSource = App.ServerContainer?.Servers;
            }
            else {
                dynamic customItem = new ExpandoObject();
                customItem.Name = Config.Settings.PlexAddress;
                UserServerComboBox.ItemsSource = new List<dynamic>() { customItem };
                UserServerComboBox.IsEnabled = false;
            }
            UserServerComboBox.SelectedIndex = 0;

            UserInfoPanel.Visibility = Visibility.Visible;

            LoadingImage.Visibility = Visibility.Collapsed;
        }

        public async void StartPolling() {
            SessionData? lastSession = null;
            DateTime lastUpdated = DateTime.Now;

            while (true) {
                SessionData? currentSession = await GetCurrentSession();
                if (currentSession != null) {
                    if (JsonSerializer.Serialize(currentSession) != JsonSerializer.Serialize(lastSession)) {
                        SetPresence(await BuildPresence(currentSession));
                        lastSession = currentSession;
                        lastUpdated = DateTime.Now;
                    }
                    else if (DateTime.Now - lastUpdated > TimeSpan.FromSeconds(Config.Settings.SessionTimeout)) {
                        ResetPresence();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval));
                }
                else {
                    ResetPresence();
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval * 2));
                }
            }
        }

        private async Task<SessionData?> GetCurrentSession() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{Address}status/sessions?X-Plex-Token={App.Token}");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());
                SessionData[]? sessions = JsonSerializer.Deserialize<SessionData[]>(responseJson.RootElement.GetProperty("MediaContainer").GetProperty("Metadata"));

                return sessions?.FirstOrDefault(session => session.Type == "track" && session.User?.Name == App.Account?.Username);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<PresenceData> BuildPresence(SessionData session) {
            string L1 = Config.Settings.TemplateL1
                .Replace("{title}", session.Title)
                .Replace("{artist}", session.Artists)
                .Replace("{album}", session.Album);

            string L2 = Config.Settings.TemplateL2
                .Replace("{title}", session.Title)
                .Replace("{artist}", session.Artists)
                .Replace("{album}", session.Album);

            return new PresenceData() {
                Line1 = L1.Length > 2 ? L1 : L1 + "  ",
                Line2 = L2.Length > 2 ? L2 : L2 + "  ",
                ImageTooltip = session.Album?.Length > 2 ? session.Album : session.Album + "  ",
                ArtLink = await GetThumbnail(session.ArtPath),
                State = session.Player?.State,
                TimeOffset = session.ViewOffset
            };
        }

        private void SetPresence(PresenceData presence) {
            if (presence.State == "playing") {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = TrimUTF8String(presence.Line1!), // theres probably a better way to trim strings but idk
                    State = TrimUTF8String(presence.Line2!),
                    Timestamps = new(DateTime.UtcNow.AddMilliseconds(-(double)presence.TimeOffset)),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = presence.ImageTooltip
                    }
                });

                PreviewArt.Source = new BitmapImage(new Uri(presence.ArtLink));
                PreviewL1.Text = presence.Line1;
                PreviewL2.Text = presence.Line2;

                TimeSpan t = TimeSpan.FromMilliseconds(presence.TimeOffset);
                PreviewL3.Text = $"{String.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds)} elapsed";
                PreviewL3.Visibility = Visibility.Visible;

                PreviewPaused.Visibility = Visibility.Collapsed;
            }
            else {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = TrimUTF8String(presence.Line1!),
                    State = TrimUTF8String(presence.Line2!),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = presence.ImageTooltip,
                        SmallImageKey = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexPaused.png",
                        SmallImageText = "Paused",
                    }
                });

                PreviewArt.Source = new BitmapImage(new Uri(presence.ArtLink));
                PreviewL1.Text = presence.Line1;
                PreviewL2.Text = presence.Line2;

                PreviewL3.Text = "";
                PreviewL3.Visibility = Visibility.Collapsed;

                PreviewPaused.Visibility = Visibility.Visible;
            }
        }

        private void ResetPresence() {
            PreviewArt.Source = new BitmapImage(new Uri("https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png"));

            PreviewL1.Text = Config.Settings.TemplateL1
                .Replace("{title}", "Title")
                .Replace("{artist}", "Artist")
                .Replace("{album}", "Album");
            PreviewL2.Text = Config.Settings.TemplateL2
                .Replace("{title}", "Title")
                .Replace("{artist}", "Artist")
                .Replace("{album}", "Album");

            PreviewL3.Text = "";
            PreviewL3.Visibility = Visibility.Collapsed;

            PreviewPaused.Visibility = Visibility.Collapsed;

            App.DiscordClient.ClearPresence();
        }

        private async Task<string> GetThumbnail(string? thumb) {
            string cacheFile = Path.Combine(Path.GetDirectoryName(Config.FilePath)!, "cache.json");

            Dictionary<string, string> thumbnails;
            string thumbnailsJson = "";

            if (File.Exists(cacheFile)) {
                thumbnailsJson = File.ReadAllText(cacheFile);
                try { thumbnails = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cacheFile))!; }
                catch { thumbnails = new(); }
            }
            else
                thumbnails = new();

            string thumbnailLink;
            if (thumb is not null && thumbnails.TryGetValue(thumb, out string? value)) {
                thumbnailLink = value;
            }
            else {
                try { thumbnailLink = await UploadImage(thumb!); }
                catch {
                    Console.WriteLine($"WARN: Unable to upload thumbnail for current session, using Plex Icon as thumbnail instead");
                    return "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png"; 
                }
                thumbnails.Add(thumb, thumbnailLink);
            }

            string newThumbnailsJson = JsonSerializer.Serialize(thumbnails);
            if (newThumbnailsJson != thumbnailsJson)
                File.WriteAllText(cacheFile, newThumbnailsJson);

            return thumbnailLink;
        }

        private async Task<string> UploadImage(string thumb) {
            HttpResponseMessage getResponse = await httpClient.GetAsync($"{Address}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={thumb}&X-Plex-Token={App.Token}");

            string dataString = Uri.EscapeDataString(Convert.ToBase64String(await getResponse.Content.ReadAsByteArrayAsync()));
            HttpResponseMessage sendResponse = await httpClient.SendAsync(new() {
                Method = HttpMethod.Post,
                RequestUri = new("https://freeimage.host/api/1/upload"),
                Content = new StringContent($"image={dataString}&key=6d207e02198a847aa98d0a2a901485a5", Encoding.UTF8, "application/x-www-form-urlencoded")
            });
            sendResponse.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("image").GetProperty("url").GetString()!;
        }

        private static string TrimUTF8String(string input) {
            string trimmed = string.Empty;
            foreach (char c in input) {
                if (Encoding.UTF8.GetByteCount(trimmed + c) > 128) break;
                trimmed += c;
            }
            return trimmed;
        }

        private void Template_LostFocus(object sender, RoutedEventArgs e) => Config.Save();

        private void SettingsButton_Click(object sender, RoutedEventArgs e) {
            SettingsWindow settingsWindow = new() { Owner = this };
            settingsWindow.ShowDialog();
            Config.Save();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            App.DiscordClient.Dispose();
            Config.Save();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.Key == Key.F12)
                Process.Start("explorer.exe", $"/select, {Config.FilePath}");

            if (e.Key == Key.F5)
                new LogWindow(App.Log!).Show();
        }
    }
}
