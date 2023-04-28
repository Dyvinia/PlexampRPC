using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DiscordRPC;
using Newtonsoft.Json;
using Plex.ServerApi.PlexModels.Account;
using Plex.ServerApi.PlexModels.Server.Sessions;

namespace PlexampRPC {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        public static SessionMetadata? Session { get; set; }

        public class PresenceData {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public string ArtLink { get; set; }
            public string State { get; set; }
            public int TimeOffset { get; set; }
        }

        public MainWindow() {
            InitializeComponent();

            MouseDown += (_, _) => FocusManager.SetFocusedElement(this, this);
        }

        public void GetAccountInfo() {
            UserIcon.Source = new BitmapImage(new Uri(App.Account?.Thumb ?? "/Resources/PlexIcon.png"));
            UserNameText.Text = App.Account?.Title ?? App.Account?.Username ?? "Name";
            UserServerComboBox.ItemsSource = App.ServerContainer?.Servers;
            UserServerComboBox.SelectedIndex = 0;

            LoadingImage.Visibility = Visibility.Collapsed;
        }

        public async Task StartPolling() {
            double pollingTime = 2.5;
            while (true) {
                SessionMetadata? currentSession = await GetCurrentSession();
                if (currentSession != null) {
                    if (JsonConvert.SerializeObject(currentSession) != JsonConvert.SerializeObject(Session)) {
                        SetPresence(await BuildPresence(currentSession));
                        Session = currentSession;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(pollingTime));
                }
                else await Task.Delay(TimeSpan.FromSeconds(pollingTime*2));
            }
        }

        private async Task<SessionMetadata?> GetCurrentSession() {
            AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;
            SessionContainer sessions = await App.ServerClient.GetSessionsAsync(App.Token, selected?.Uri.ToString());
            return sessions.Metadata.FirstOrDefault(session => session.Type == "track" && session.User.Title == App.Account?.Username);
        }

        private async Task<PresenceData> BuildPresence(SessionMetadata session) {
            return new PresenceData() {
                Title = session.Title,
                Artist = $"by {session.GrandparentTitle}",
                Album = session.ParentTitle,
                ArtLink = await GetThumbnail(session),
                State = session.Player.State,
                TimeOffset = (int)session.ViewOffset
            };
        }

        private void SetPresence(PresenceData data) {
            if (data.State == "playing") {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = data.Title,
                    State = data.Artist,
                    Timestamps = new(DateTime.UtcNow.AddMilliseconds(-(double)data.TimeOffset)),
                    Assets = new() {
                        LargeImageKey = data.ArtLink,
                        LargeImageText = data.Album
                    }
                });
            }
            else {
                App.DiscordClient.SetPresence(new RichPresence() {
                    Details = data.Title,
                    State = data.Artist,
                    Assets = new() {
                        LargeImageKey = data.ArtLink,
                        LargeImageText = data.Album,
                        SmallImageKey = "paused2",
                        SmallImageText = "Paused",
                    }
                });
            }
        }

        private async Task<string> GetThumbnail(SessionMetadata session) {
            Dictionary<string, string> thumbnails;
            string thumbnailLink;

            if (File.Exists("cache.json"))
                thumbnails = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("cache.json"));
            else
                thumbnails = new();


            if (thumbnails.TryGetValue(session.Thumb, out string value)) {
                thumbnailLink = value;
            }
            else {
                thumbnailLink = await UploadImage(session);
                thumbnails.Add(session.Thumb, thumbnailLink);
            }

            File.WriteAllText("cache.json", JsonConvert.SerializeObject(thumbnails));

            return thumbnailLink;
        }

        private async Task<string> UploadImage(SessionMetadata session) {
            AccountServer? selected = UserServerComboBox.SelectedItem as AccountServer;
            int artRes = 256;
            string url = $"{selected?.Uri.ToString()}photo/:/transcode?width={artRes}&height={artRes}&minSize=1&upscale=1&format=png&url={session.Thumb}&X-Plex-Token={App.Token}";

            var client = new HttpClient();
            var response = await client.GetAsync(url);
            var stream = await response.Content.ReadAsStreamAsync();

            byte[] imageData = new byte[stream.Length];
            stream.Read(imageData, 0, imageData.Length);
            stream.Close();

            const int MAX_URI_LENGTH = 32766;
            string base64img = System.Convert.ToBase64String(imageData);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < base64img.Length; i += MAX_URI_LENGTH) {
                sb.Append(Uri.EscapeDataString(base64img.Substring(i, Math.Min(MAX_URI_LENGTH, base64img.Length - i))));
            }

            string uploadRequestString = "image=" + sb.ToString() + "&key=6d207e02198a847aa98d0a2a901485a5";

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create("https://freeimage.host/api/1/upload");
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";

            StreamWriter streamWriter = new StreamWriter(webRequest.GetRequestStream());
            streamWriter.Write(uploadRequestString);
            streamWriter.Close();

            WebResponse response2 = webRequest.GetResponse();
            System.IO.Stream responseStream = response2.GetResponseStream();
            StreamReader responseReader = new StreamReader(responseStream);

            return JsonConvert.DeserializeObject<dynamic>(responseReader.ReadToEnd()).image.url;
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            App.DiscordClient.Dispose();
        }
    }
}
