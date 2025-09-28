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
using System.Xml;
using DiscordRPC;
using Hardcodet.Wpf.TaskbarNotification;
using PlexampRPC.Data;
using PlexampRPC.Utils;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private static readonly HttpClient httpClient = new();

        private static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

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
            public string? Line3 { get; set; }
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
            if (UserIcon.Source?.ToString() != uri.ToString()) {
                UserIcon.Source = new BitmapImage(uri) {
                    CreateOptions = BitmapCreateOptions.IgnoreImageCache
                };
            }

            DiscordUsername.Text = App.DiscordClient.CurrentUser.DisplayName;
            DiscordAvatar.Source = new BitmapImage(new(App.DiscordClient.CurrentUser.GetAvatarURL())) {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };
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
                SessionData? currentSession = Config.Settings.LocalPlayer ? await GetLocalSession() : await GetServerSession();
                if (currentSession != null) {
                    if (JsonSerializer.Serialize(currentSession) != JsonSerializer.Serialize(lastSession)) {
                        SetPresence(await BuildPresence(currentSession));
                        if (currentSession?.Key != lastSession?.Key)
                            Console.WriteLine("Title: {title}\nArtist: {artist}\nAlbum: {album}\nYear: {year}\nPlayer: {player}\nListen Count: {listens}\nCodec: {codec}\nContainer: {container}\nBitrate (Kbps): {bitrate}\nChannel Layout: {channel}\nBit Depth: {bitdepth}\nSamplerate (kHz): {samplerate}".ApplyPlaceholders(currentSession));

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

        private static async Task<SessionData?> GetLocalSession() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, "http://localhost:32500/player/timeline/poll?wait=0&includeMetadata=1&commandID=1");
                requestMessage.Headers.Add("Accept", "application/xml");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                XmlDocument responseXml = new();
                responseXml.LoadXml(await sendResponse.Content.ReadAsStringAsync());

                // Find the active music timeline with a Track element
                XmlNode? timelineNode = responseXml.SelectSingleNode("/MediaContainer/Timeline[Track]");
                if (timelineNode is null)
                    return null;

                XmlNode? trackNode = timelineNode.SelectSingleNode("Track");
                if (trackNode is null)
                    return null;

                static string? GetAttr(XmlNode node, string name) => node.Attributes?[name]?.Value;

                SessionData sessionData = new() {
                    Title = GetAttr(trackNode, "title"),
                    Album = GetAttr(trackNode, "parentTitle"),
                    ArtPath = GetAttr(trackNode, "thumb"),
                    Type = GetAttr(trackNode, "type"),
                    Guid = GetAttr(trackNode, "guid"),
                    TrackArtist = GetAttr(trackNode, "originalTitle"),
                    AlbumArtist = GetAttr(trackNode, "grandparentTitle"),
                    Player = new SessionData.PlayerData { State = GetAttr(timelineNode, "state") },
                    ProgressOffset = int.TryParse(GetAttr(timelineNode, "time"), out int vo) ? vo : 0,
                    Duration = int.TryParse(GetAttr(trackNode, "duration") ?? GetAttr(timelineNode, "duration"), out int dur) ? dur : 0,
                    User = App.Account?.Username is not null ? new SessionData.UserData { Name = App.Account.Username } : null
                };

                return sessionData;
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: local timeline poll\n{e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<SessionData?> GetServerSession() {
            try {
                if (UserServerComboBox.SelectedItem is null)
                    return null;
                if (!Uri.IsWellFormedUriString(SelectedAddress?.ToString(), UriKind.Absolute)) {
                    Console.WriteLine("WARN: No server selected or address is invalid");
                    return null;
                }
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken}");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());

                if (!responseJson.RootElement.GetProperty("MediaContainer").TryGetProperty("Metadata", out _))
                    return null;

                SessionData[]? sessions = JsonSerializer.Deserialize<SessionData[]>(responseJson.RootElement.GetProperty("MediaContainer").GetProperty("Metadata"));

                return sessions?.FirstOrDefault(session => session.Type == "track" && session.User?.Name == App.Account?.Username);
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {SelectedAddress}status/sessions?X-Plex-Token={SelectedResource?.AccessToken?[..3]}...\n{e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<PresenceData> BuildPresence(SessionData session) {
            string L1 = Config.Settings.TemplateL1.ApplyPlaceholders(session);
            string L2 = Config.Settings.TemplateL2.ApplyPlaceholders(session);
            string L3 = Config.Settings.TemplateL3.ApplyPlaceholders(session);

            return new PresenceData() {
                Line1 = L1.Length > 2 ? L1 : L1 + "  ",
                Line2 = L2.Length > 2 ? L2 : L2 + "  ",
                Line3 = L3.Length > 2 ? L3 : L3 + "  ",
                ArtLink = Config.Settings.LocalPlayer ? "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png" : await GetThumbnail(session.ArtPath, session.Album),
                State = session.Player?.State,
                TimeOffset = session.ProgressOffset,
                Duration = session.Duration,
                Url = session?.Guid?.StartsWith("plex://") == true ? $"https://listen.plex.tv/{session.Guid?[7..]}" : null
            };
        }

        private void SetPresence(PresenceData presence) {
            if (presence.State == "playing") {
                App.DiscordClient.SetPresence(new() {
                    Details = TrimUTF8String(presence.Line1!), // theres probably a better way to trim strings but idk
                    State = TrimUTF8String(presence.Line2!),
                    Timestamps = new(DateTime.UtcNow.AddMilliseconds(-(double)presence.TimeOffset), DateTime.UtcNow.AddMilliseconds((double)presence.Duration - (double)presence.TimeOffset)),
                    Type = ActivityType.Listening,
                    StatusDisplay = Enum.Parse<StatusDisplayType>(Config.Settings.StatusDisplayType),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = TrimUTF8String(presence.Line3!)
                    }
                });

                PreviewArt.Source = new BitmapImage(new Uri(presence.ArtLink));
                PreviewL1.Text = TrimUTF8String(presence.Line1!);
                PreviewL2.Text = TrimUTF8String(presence.Line2!);
                PreviewL3.Text = TrimUTF8String(presence.Line3!);

                PreviewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
                PreviewStatusListeningTo.Text = Config.Settings.StatusDisplayType switch {
                    "State" => PreviewL2.Text,
                    "Details" => PreviewL1.Text,
                    _ => Config.Settings.DiscordListeningTo,
                };
                DiscordStatus.Visibility = Visibility.Visible;

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
                    Timestamps = new(DateTime.UtcNow, DateTime.UtcNow), // this is the least broken option to avoid any timer from showing I think
                    Type = ActivityType.Listening,
                    StatusDisplay = Enum.Parse<StatusDisplayType>(Config.Settings.StatusDisplayType),
                    Assets = new() {
                        LargeImageKey = presence.ArtLink,
                        LargeImageText = TrimUTF8String(presence.Line3!),
                        SmallImageKey = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexPaused.png",
                        SmallImageText = "Paused",
                    }
                });

                PreviewArt.Source = new BitmapImage(new Uri(presence.ArtLink));
                PreviewL1.Text = TrimUTF8String(presence.Line1!);
                PreviewL2.Text = TrimUTF8String(presence.Line2!);
                PreviewL3.Text = TrimUTF8String(presence.Line3!);

                PreviewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
                PreviewStatusListeningTo.Text = Config.Settings.StatusDisplayType switch {
                    "State" => PreviewL2.Text,
                    "Details" => PreviewL1.Text,
                    _ => Config.Settings.DiscordListeningTo,
                };
                DiscordStatus.Visibility = Visibility.Visible;

                PreviewTime.Visibility = Visibility.Collapsed;
                PreviewPaused.Visibility = Visibility.Visible;
            }
        }

        private void ResetPresence() {
            PreviewArt.Source = new BitmapImage(new Uri("https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png")) {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };

            PreviewL1.Text = Config.Settings.TemplateL1.ApplyPlaceholders();
            PreviewL2.Text = Config.Settings.TemplateL2.ApplyPlaceholders();
            PreviewL3.Text = Config.Settings.TemplateL3.ApplyPlaceholders();

            PreviewListeningTo.Text = $"Listening to {Config.Settings.DiscordListeningTo}";
            PreviewStatusListeningTo.Text = Config.Settings.DiscordListeningTo;
            DiscordStatus.Visibility = Visibility.Collapsed;

            PreviewTime.Visibility = Visibility.Collapsed;
            PreviewPaused.Visibility = Visibility.Collapsed;

            App.DiscordClient.ClearPresence();
        }

        private async Task<string> GetThumbnail(string? path, string? album) {
            string cacheFile = Path.Combine(Path.GetDirectoryName(Config.FilePath)!, "cache.json");

            Dictionary<string, ThumbnailData> thumbnails;
            string thumbnailsJson = "";

            if (File.Exists(cacheFile)) {
                thumbnailsJson = File.ReadAllText(cacheFile);
                try { thumbnails = JsonSerializer.Deserialize<Dictionary<string, ThumbnailData>>(File.ReadAllText(cacheFile))!; }
                catch { thumbnails = []; }

            }
            else
                thumbnails = [];

            string thumbnailLink;
            if (path is not null && thumbnails.TryGetValue(path, out ThumbnailData? value)) {
                thumbnailLink = value.Art;
            }
            else {
                try { thumbnailLink = await UploadImage(path!); }
                catch (Exception e) {
                    Console.WriteLine($"WARN: Unable to upload thumbnail for current session, using Plex Icon as thumbnail instead\n{e.Message} {e.InnerException}");
                    return "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIcon.png";
                }
                thumbnails.Add(path!, new() { Name = album ?? "Unknown", Art = thumbnailLink });
            }

            string newThumbnailsJson = JsonSerializer.Serialize(thumbnails, serializerOptions);
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
