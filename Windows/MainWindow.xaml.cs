using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DiscordRPC;
using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json;
using Plex.ServerApi.PlexModels.Account;
using Plex.ServerApi.PlexModels.Server.Playlists;
using Plex.ServerApi.PlexModels.Server.Sessions;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        static readonly HttpClient Client = new();

        public static SessionMetadata? Session { get; set; }

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
            SetupContextMenu();
        }

        public TaskbarIcon TrayIcon = new() {
            ToolTipText = "PlexampRPC",
            MenuActivation = PopupActivationMode.LeftOrRightClick
        };

        private void SetupContextMenu() {
            TrayIcon.IconSource = Icon;
            ContextMenu contextMenu = new();
            MenuItem menuShow = new() { Header = "Show PlexampRPC" };
            MenuItem menuExit = new() { Header = "Exit PlexampRPC" };
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
            UserServerComboBox.ItemsSource = App.ServerContainer?.Servers;
            UserServerComboBox.SelectedIndex = 0;

            UserInfoPanel.Visibility = Visibility.Visible;

            LoadingImage.Visibility = Visibility.Collapsed;
        }

        public async void StartPolling() {
            while (true) {
                SessionMetadata? currentSession = await GetCurrentSession();
                if (currentSession != null) {
                    if (JsonConvert.SerializeObject(currentSession) != JsonConvert.SerializeObject(Session)) {
                        SetPresence(await BuildPresence(currentSession));
                        Session = currentSession;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval));
                }
                else {
                    ResetPresence();
                    await Task.Delay(TimeSpan.FromSeconds(Config.Settings.RefreshInterval * 2));
                }
            }
        }

        private async Task<SessionMetadata?> GetCurrentSession() {
            try {
                AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;
                SessionContainer sessions = await App.ServerClient.GetSessionsAsync(App.Token, selected?.Uri.ToString());
                return sessions?.Metadata?.FirstOrDefault(session => session.Type == "track" && session.User.Title == App.Account?.Username);
            }
            catch { return null; }
        }

        private async Task<PresenceData> BuildPresence(SessionMetadata session) {
            string L1 = Config.Settings.TemplateL1
                .Replace("{title}", session.Title)
                .Replace("{artist}", session.GrandparentTitle)
                .Replace("{album}", session.ParentTitle);

            string L2 = Config.Settings.TemplateL2
                .Replace("{title}", session.Title)
                .Replace("{artist}", session.GrandparentTitle)
                .Replace("{album}", session.ParentTitle);

            return new PresenceData() {
                Line1 = L1,
                Line2 = L2,
                ImageTooltip = session.ParentTitle,
                ArtLink = await GetThumbnail(session),
                State = session.Player.State,
                TimeOffset = (int)session.ViewOffset
            };
        }

        private void SetPresence(PresenceData presence) {
            if (presence.State == "playing") {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = presence.Line1,
                    State = presence.Line2,
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
                    Details = presence.Line1,
                    State = presence.Line2,
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

        private async Task<string> GetThumbnail(SessionMetadata session) {
            string cacheFile = Path.Combine(Path.GetDirectoryName(Config.FilePath)!, "cache.json");

            Dictionary<string, string> thumbnails;
            string thumbnailsJson = "";

            if (File.Exists(cacheFile)) {
                thumbnailsJson = File.ReadAllText(cacheFile);
                try { thumbnails = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cacheFile))!; }
                catch { thumbnails = new(); }
            }
            else
                thumbnails = new();

            string thumbnailLink;
            if (thumbnails.TryGetValue(session.Thumb, out string? value)) {
                thumbnailLink = value;
            }
            else {
                try { thumbnailLink = await UploadImage(session); }
                catch { return "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png"; }
                thumbnails.Add(session.Thumb, thumbnailLink);
            }

            string newThumbnailsJson = JsonConvert.SerializeObject(thumbnails);
            if (newThumbnailsJson != thumbnailsJson)
                File.WriteAllText(cacheFile, newThumbnailsJson);

            return thumbnailLink;
        }

        private async Task<string> UploadImage(SessionMetadata session) {
            AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;

            HttpResponseMessage response = await Client.GetAsync($"{selected?.Uri}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={session.Thumb}&X-Plex-Token={App.Token}");
            System.IO.Stream stream = await response.Content.ReadAsStreamAsync();

            byte[] imageData = new byte[stream.Length];
            await stream.ReadAsync(imageData);
            stream.Close();

            HttpRequestMessage httpRequest = new() {
                Method = HttpMethod.Post,
                RequestUri = new("https://freeimage.host/api/1/upload"),
                Content = new StringContent($"image={Uri.EscapeDataString(Convert.ToBase64String(imageData))}&key=6d207e02198a847aa98d0a2a901485a5", Encoding.UTF8, "application/x-www-form-urlencoded")
            };

            HttpResponseMessage webRequest = await Client.SendAsync(httpRequest);
            webRequest.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<dynamic>(await webRequest.Content.ReadAsStringAsync())!.image.url;
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
        }
    }
}
