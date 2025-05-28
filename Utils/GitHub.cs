using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using DyviniaUtils.Dialogs;

namespace DyviniaUtils {
    class GitHub {

        class Release {

            [JsonPropertyName("tag_name")]
            public required string Tag { get; set; }

            [JsonPropertyName("assets")]
            public required Asset[] Assets { get; set; }

            public class Asset {
                [JsonPropertyName("name")]
                public required string Name { get; set; }

                [JsonPropertyName("browser_download_url")]
                public required string DownloadURL { get; set; }
            }
        }

        /// <summary>
        /// Checks Github if there is a newer version and asks the user if they want to update
        /// </summary>
        public static async Task CheckAndInstall(string repoAuthor, string repoName) {
            CleanupUpdate();
            if (await CheckVersion(repoAuthor, repoName)) {
                if (UpdateDialog.Show(repoAuthor, repoName))
                    await InstallUpdate(repoAuthor, repoName);
            }
        }


        /// <summary>
        /// Checks Github if there is a newer version
        /// </summary>
        public static async Task<bool> CheckVersion(string repoAuthor, string repoName) {
            try {
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "request");
                Release releases = JsonSerializer.Deserialize<Release>(await client.GetStringAsync($"https://api.github.com/repos/{repoAuthor}/{repoName}/releases/latest"))!;

                Version latest = new(releases.Tag[1..]);
                Version local = Assembly.GetExecutingAssembly().GetName().Version!;

                if (local.CompareTo(latest) < 0)
                    return true;
                else
                    return false;
            }
            catch (Exception e) {
                ExceptionDialog.Show(e, repoName, "Unable to check for updates:");
                return false;
            }
        }

        /// <summary>
        /// Downloads and Installs newest version
        /// </summary>
        public static async Task InstallUpdate(string repoAuthor, string repoName) {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "request");
            Release json = JsonSerializer.Deserialize<Release>(await client.GetStringAsync($"https://api.github.com/repos/{repoAuthor}/{repoName}/releases/latest"))!;

            string downloadUrl = json.Assets.Where(x => x.DownloadURL.Contains(".exe")).FirstOrDefault()!.DownloadURL;

            string filePath = Environment.ProcessPath!;
            string oldPath = filePath.Replace(".exe", ".old.exe");

            try {
                File.Move(filePath, oldPath, true);

                await Downloader.DownloadWithWindow(downloadUrl, filePath);

                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception e) {
                MessageBoxDialog.Show($"Failed to install update, please update manually.\n{e.Message}", repoName, MessageBoxButton.OK, DialogSound.Error);
                Process.Start(new ProcessStartInfo($"https://github.com/{repoAuthor}/{repoName}/releases/latest") { UseShellExecute = true });
            }
        }

        /// <summary>
        /// Cleans Update Files
        /// </summary>
        public static void CleanupUpdate() {
            string old = Environment.ProcessPath!.Replace(".exe", ".old.exe");
            if (File.Exists(old))
                File.Delete(old);
        }
    }
}
