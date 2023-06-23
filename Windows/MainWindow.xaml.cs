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
using Newtonsoft.Json.Linq;
using Plex.ServerApi.PlexModels.Account;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        static readonly HttpClient Client = new();

        public static dynamic? Session { get; set; }

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
            UserServerComboBox.ItemsSource = App.ServerContainer?.Servers;
            UserServerComboBox.SelectedIndex = 0;

            UserInfoPanel.Visibility = Visibility.Visible;

            LoadingImage.Visibility = Visibility.Collapsed;
        }

        public async void StartPolling() {
            while (true) {
                dynamic? currentSession = await GetCurrentSession();
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

        private async Task<dynamic?> GetCurrentSession() {
            try {
                AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;

                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{selected?.Uri}status/sessions?X-Plex-Token={App.Token}");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await Client.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                dynamic? metadata = JsonConvert.DeserializeObject<dynamic>(await sendResponse.Content.ReadAsStringAsync())?.MediaContainer.Metadata;
                if (metadata != null) {
                    dynamic[] sessions = ((JArray)metadata).ToArray();
                    return sessions.FirstOrDefault(session => session.type == "track" && session.User.title == App.Account?.Username);
                }
                else return null;
            }
            catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get current session: {e.Message} {e.InnerException}");
                return null;
            }
        }

        private async Task<PresenceData> BuildPresence(dynamic session) {
            string title = session.title;
            string artist = session.originalTitle ?? session.grandparentTitle;
            string album = session.parentTitle;

            if (artist.Contains(';')) {
                string[] artists = artist.Split(';', StringSplitOptions.TrimEntries);
                if (artists.Length > 2)
                    artist = String.Join(", ", artists);
                else artist = String.Join(" & ", artists);
            }

            string L1 = Config.Settings.TemplateL1
                .Replace("{title}", title)
                .Replace("{artist}", artist)
                .Replace("{album}", album);

            string L2 = Config.Settings.TemplateL2
                .Replace("{title}", title)
                .Replace("{artist}", artist)
                .Replace("{album}", album);

            return new PresenceData() {
                Line1 = L1,
                Line2 = L2,
                ImageTooltip = album,
                ArtLink = await GetThumbnail((string)session.thumb),
                State = session.Player.state,
                TimeOffset = session.viewOffset
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

        private async Task<string> GetThumbnail(string thumb) {
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
            if (thumbnails.TryGetValue(thumb, out string? value)) {
                thumbnailLink = value;
            }
            else {
                try { thumbnailLink = await UploadImage(thumb); }
                catch {
                    Console.WriteLine($"WARN: Unable to upload thumbnail for current session, using Plex Icon as thumbnail instead");
                    return "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png"; 
                }
                thumbnails.Add(thumb, thumbnailLink);
            }

            string newThumbnailsJson = JsonConvert.SerializeObject(thumbnails);
            if (newThumbnailsJson != thumbnailsJson)
                File.WriteAllText(cacheFile, newThumbnailsJson);

            return thumbnailLink;
        }

        private async Task<string> UploadImage(string thumb) {
            AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;

            HttpResponseMessage getResponse = await Client.GetAsync($"{selected?.Uri}photo/:/transcode?width={Config.Settings.ArtResolution}&height={Config.Settings.ArtResolution}&minSize=1&upscale=1&format=png&url={thumb}&X-Plex-Token={App.Token}");

            string dataString = Uri.EscapeDataString(Convert.ToBase64String(await getResponse.Content.ReadAsByteArrayAsync()));
            HttpResponseMessage sendResponse = await Client.SendAsync(new() {
                Method = HttpMethod.Post,
                RequestUri = new("https://freeimage.host/api/1/upload"),
                Content = new StringContent($"image={dataString}&key=6d207e02198a847aa98d0a2a901485a5", Encoding.UTF8, "application/x-www-form-urlencoded")
            });
            sendResponse.EnsureSuccessStatusCode();
            return JsonConvert.DeserializeObject<dynamic>(await sendResponse.Content.ReadAsStringAsync())!.image.url;
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
