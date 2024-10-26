using System.Diagnostics;
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
using PlexampRPC.Data;

namespace PlexampRPC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private static readonly HttpClient httpClient = new();

        private PlexResourceData? SelectedResource => (PlexResourceData)UserServerComboBox.SelectedItem;

        public Uri? SelectedAddress {
            get {
                if (!string.IsNullOrWhiteSpace(Config.Settings.PlexAddress))
                    return new UriBuilder(Config.Settings.PlexAddress).Uri;

                if (Config.Settings.LocalAddress)
                    return SelectedResource?.LocalUri;
                else
                    return SelectedResource?.Uri;
            }
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

        public void UpdateAccountIcon() {
            Uri uri = new(App.Account?.Thumb ?? "/Resources/PlexIcon.png");
            if (UserIcon.Source?.ToString() != uri.ToString())
                UserIcon.Source = new BitmapImage(uri);
        }

        public void GetAccountInfo() {
            UpdateAccountIcon();
            StatusTextBox.Text = App.Account?.Title ?? App.Account?.Username ?? "Name";

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
            UserServerComboBox.SelectedIndex = App.PlexResources?.ToList().FindIndex(r => r.Name == Config.Settings.SelectedServer) ?? 0;
            if (UserServerComboBox.SelectedIndex == -1)
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
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken}");
                requestMessage.Headers.Add("Accept", "application/json");
                //Console.WriteLine(requestMessage.RequestUri);

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                if (!responseJson.RootElement.GetProperty("MediaContainer").TryGetProperty("Metadata", out _))
                    return null;

                SessionData[]? sessions = JsonSerializer.Deserialize<SessionData[]>(responseJson.RootElement.GetProperty("MediaContainer").GetProperty("Metadata"));

                return sessions?.FirstOrDefault(session => session.Type == "track" && session.User?.Name == App.Account?.Username);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken?[..3]}... {e.Message} {e.InnerException}");
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
            PreviewArt.Source = new BitmapImage(new Uri("https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png")) { 
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };

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

        public async Task<PlexResourceData[]?> GetAccountResources() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"https://plex.tv/api/v2/resources?includeHttps=1&includeIPv6=1&X-Plex-Token={App.Token}&X-Plex-Client-Identifier=PlexampRPC");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                PlexResourceData[]? serverResources = JsonSerializer.Deserialize<PlexResourceData[]>(responseJson.RootElement)?.Where(r => r.Provides?.Split(",").Contains("server") == true).ToArray();

                if (serverResources is null || serverResources.Length == 0) {
                    Console.WriteLine("WARN: No servers found");
                    return null;
                }

                List<PlexResourceData>? finalResources = [];
                for (int i = 0; i < serverResources.Length; i++) {
                    PlexResourceData resource = serverResources[i];

                    if (Config.Settings.OwnedOnly && !resource.Owned)
                        continue;
                    if (serverResources.Length > 1)
                        StatusTextBox.Text = $"Loading Servers...\n[{i}/{serverResources.Length}]";
                    else
                        StatusTextBox.Text = "Loading Servers...";
                    await TestResource(resource);
                    if (resource is not null)
                        finalResources.Add(resource);
                }
                // Show it being complete before replacing the text with username/etc
                if (serverResources.Length > 1)
                    StatusTextBox.Text = $"Loading Servers...\n[{serverResources.Length}/{serverResources.Length}]";
                await Task.Delay(200);

                return [.. finalResources];
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                return null;
            }
        }

        private static async Task TestResource(PlexResourceData resource) {
            foreach (PlexConnectionData connection in resource.Connections!) {
                Uri uri;
                if (connection.Local)
                    uri = new UriBuilder("http", connection.Address, connection.Port).Uri;
                else if (!Config.Settings.LocalAddress)
                    uri = new UriBuilder(connection.Uri!).Uri;
                else
                    continue;

                try {
                    Console.WriteLine($"INFO: Testing {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{uri}status/sessions?X-Plex-Token={resource.AccessToken}");
                    requestMessage.Headers.Add("Accept", "application/json");

                    HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                    sendResponse.EnsureSuccessStatusCode();
                    Console.WriteLine($"INFO: Success {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                    if (connection.Local)
                        resource.LocalUri ??= uri;
                    else
                        resource.Uri ??= uri;
                    if (resource.LocalUri is not null && resource.Uri is not null) {
                        break;
                    }
                }
                catch (TaskCanceledException) {
                    Console.WriteLine($"WARN: Timeout {(connection.Local ? "Local" : "Remote")} {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}...");
                }
                catch (HttpRequestException e) { // Unreachable server, skip for now
                    Console.WriteLine($"WARN: Unable to access {uri}status/sessions?X-Plex-Token={resource.AccessToken?[..3]}: {e.Message}");
                }
                catch (Exception e) {
                    Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                }
            }
        }

        private async Task<string> UploadImage(string thumb) {
            HttpResponseMessage getResponse = await httpClient.GetAsync($"{SelectedAddress}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={thumb}&X-Plex-Token={SelectedResource?.AccessToken}");

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

        private void LogsButton_Click(object sender, RoutedEventArgs e) => new LogWindow(App.Log!).Show();


        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            App.DiscordClient.Dispose();

            Config.Settings.SelectedServer = SelectedResource?.Name ?? string.Empty;

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
