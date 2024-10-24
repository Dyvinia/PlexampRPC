﻿using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DiscordRPC;
using Hardcodet.Wpf.TaskbarNotification;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        public static readonly HttpClient httpClient = new();

        public Uri? Address {
            get {
                if (!string.IsNullOrEmpty(Config.Settings.PlexAddress))
                    return new UriBuilder(Config.Settings.PlexAddress).Uri;
                else if (UserServerComboBox.SelectedItem != null) {
                    if (Config.Settings.LocalAddress)
                        return ((PlexResource)UserServerComboBox.SelectedItem).LocalUri;
                    else
                        return ((PlexResource)UserServerComboBox.SelectedItem).Uri;
                }
                return null;
            }
        }

        public string? Token {
            get {
                if (UserServerComboBox.SelectedItem != null)
                    return ((PlexResource)UserServerComboBox.SelectedItem).AccessToken;
                return null;
            }
        }

        public static string UserNameText {
            get { return _userNameText; }
            set {
                if (_userNameText != value) {
                    _userNameText = value;
                    UpdateUserNameTextBlock(value);
                }
            }
        }

        private static string _userNameText = "Logging in...";

        private static void UpdateUserNameTextBlock(string newText) {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mainWindow) {
                    TextBlock textBlock = (TextBlock)mainWindow.FindName("UserNameTextBox");
                    if (textBlock != null) {
                        textBlock.Text = newText;
                    }
                }
            });
        }

        public class PresenceData {
            public string? Line1 { get; set; }
            public string? Line2 { get; set; }
            public string? ImageTooltip { get; set; }
            public string ArtLink { get; set; } = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png";
            public string? State { get; set; }
            public int TimeOffset { get; set; }
            public int Duration { get; set; }
            public string? Url { get; set; }
        }

        public MainWindow() {
            InitializeComponent();

            httpClient.Timeout = TimeSpan.FromSeconds(2);

            UserNameTextBox.Text = UserNameText;
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

            PreviewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
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
            UserNameText = App.Account?.Title ?? App.Account?.Username ?? "Name";

            if (string.IsNullOrEmpty(Config.Settings.PlexAddress)) {
                if (App.PlexResources != null)
                    UserServerComboBox.ItemsSource = App.PlexResources;
            }
            else {
                dynamic customItem = new ExpandoObject();
                customItem.Name = Config.Settings.PlexAddress;
                UserServerComboBox.ItemsSource = new List<dynamic>() { customItem };
                UserServerComboBox.IsEnabled = false;
            }
            UserServerComboBox.SelectedIndex = 0;

            UserInfoPanel.Visibility = Visibility.Visible;

            UserServerComboBox.Visibility = Visibility.Visible;

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
                if (UserServerComboBox.SelectedItem == null) return null;
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{Address}status/sessions?X-Plex-Token={Token}");
                requestMessage.Headers.Add("Accept", "application/json");
                Console.WriteLine(requestMessage.RequestUri);

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                if (!responseJson.RootElement.GetProperty("MediaContainer").TryGetProperty("Metadata", out _))
                    return null;

                SessionData[]? sessions = JsonSerializer.Deserialize<SessionData[]>(responseJson.RootElement.GetProperty("MediaContainer").GetProperty("Metadata"));

                return sessions?.FirstOrDefault(session => session.Type == "track" && session.User?.Name == App.Account?.Username);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {Address}status/sessions?X-Plex-Token={Token?[..3]}... {e.Message} {e.InnerException}");
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
                TimeOffset = session.ViewOffset,
                Duration = session.Duration,
                Url = (session.Guid != null && session.Guid.StartsWith("plex://")) ? $"https://listen.plex.tv/{session.Guid?[7..]}" : null
            };
        }

        private void SetPresence(PresenceData presence) {
            if (presence.State == "playing") {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = TrimUTF8String(presence.Line1!), // theres probably a better way to trim strings but idk
                    State = TrimUTF8String(presence.Line2!),
                    Timestamps = new(DateTime.UtcNow.AddMilliseconds(-(double)presence.TimeOffset), DateTime.UtcNow.AddMilliseconds((double)presence.Duration-(double)presence.TimeOffset)),
                    Type = ActivityType.Listening,
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = presence.ImageTooltip
                    }
                });

                PreviewArt.Source = new BitmapImage(new Uri(presence.ArtLink));
                PreviewL1.Text = presence.Line1;
                PreviewL2.Text = presence.Line2;
                PreviewL3.Text = presence.ImageTooltip;

                PreviewTime.Visibility = Visibility.Visible;

                TimeSpan timeStart = TimeSpan.FromMilliseconds(presence.TimeOffset);
                PreviewTimeStart.Text = $"{string.Format("{0:D2}:{1:D2}", timeStart.Minutes, timeStart.Seconds)}";

                TimeSpan timeEnd = TimeSpan.FromMilliseconds(presence.Duration);
                PreviewTimeEnd.Text = $"{string.Format("{0:D2}:{1:D2}", timeEnd.Minutes, timeEnd.Seconds)}";

                PreviewTimeProgress.Value = 100d * presence.TimeOffset / presence.Duration;

                PreviewPaused.Visibility = Visibility.Collapsed;
            }
            else {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = TrimUTF8String(presence.Line1!),
                    State = TrimUTF8String(presence.Line2!),
                    Type = ActivityType.Listening,
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
                PreviewL3.Text = presence.ImageTooltip;

                PreviewTime.Visibility = Visibility.Collapsed;
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

            PreviewL3.Text = Config.Settings.TemplateL3
                .Replace("{title}", "Title")
                .Replace("{artist}", "Artist")
                .Replace("{album}", "Album");

            PreviewTime.Visibility = Visibility.Collapsed;
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
                catch { thumbnails = []; }
            }
            else
                thumbnails = [];

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
                thumbnails.Add(thumb!, thumbnailLink);
            }

            string newThumbnailsJson = JsonSerializer.Serialize(thumbnails);
            if (newThumbnailsJson != thumbnailsJson)
                File.WriteAllText(cacheFile, newThumbnailsJson);

            return thumbnailLink;
        }

        private async Task<string> UploadImage(string thumb) {
            HttpResponseMessage getResponse = await httpClient.GetAsync($"{Address}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={thumb}&X-Plex-Token={Token}");

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

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                App.Log?.SaveAs();
        }
    }
}
