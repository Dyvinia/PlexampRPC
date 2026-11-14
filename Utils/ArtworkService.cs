using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using PlexampRPC.Data;

namespace PlexampRPC.Utils {
    internal sealed class ArtworkService(HttpClient client) {
        private static readonly JsonSerializerOptions serializerOptions = new() { WriteIndented = true };
        private readonly Dictionary<string, (string Url, DateTime CheckedAt)> checkedLinks = [];

        public async Task<string> GetThumbnailAsync(Uri server, string? token, string path, string? album, int resolution, string cacheFile) {
            Dictionary<string, ThumbnailData> thumbnails = ReadCache(cacheFile);
            if (thumbnails.TryGetValue(path, out ThumbnailData? cached) && !string.IsNullOrWhiteSpace(cached.Art)) {
                if (checkedLinks.TryGetValue(path, out var previous) && previous.Url == cached.Art &&
                    DateTime.UtcNow - previous.CheckedAt < TimeSpan.FromHours(1))
                    return cached.Art;

                using HttpResponseMessage check = await client.GetAsync(cached.Art, HttpCompletionOption.ResponseHeadersRead);
                if (check.StatusCode != HttpStatusCode.NotFound && check.StatusCode != HttpStatusCode.Gone) {
                    check.EnsureSuccessStatusCode();
                    checkedLinks[path] = (cached.Art, DateTime.UtcNow);
                    return cached.Art;
                }
                Console.WriteLine("INFO: Cached artwork is no longer available; uploading it again");
            }

            string query = $"photo/:/transcode?width={resolution}&height={resolution}&minSize=1&upscale=1&format=jpg" +
                $"&url={Uri.EscapeDataString(path)}&X-Plex-Token={Uri.EscapeDataString(token ?? string.Empty)}";
            using HttpResponseMessage image = await client.GetAsync(new Uri(server, query));
            image.EnsureSuccessStatusCode();
            string data = Convert.ToBase64String(await image.Content.ReadAsByteArrayAsync());

            using FormUrlEncodedContent content = new(new Dictionary<string, string> {
                ["image"] = data,
                ["key"] = "6d207e02198a847aa98d0a2a901485a5"
            });
            using HttpResponseMessage upload = await client.PostAsync("https://freeimage.host/api/1/upload", content);
            upload.EnsureSuccessStatusCode();
            using JsonDocument response = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            string? link = response.RootElement.GetProperty("image").GetProperty("url").GetString();
            if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                throw new InvalidDataException("Image host returned an invalid artwork URL");

            thumbnails[path] = new() { Name = album ?? "Unknown", Art = link };
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(thumbnails, serializerOptions));
            checkedLinks[path] = (link, DateTime.UtcNow);
            return link;
        }

        private static Dictionary<string, ThumbnailData> ReadCache(string cacheFile) {
            if (!File.Exists(cacheFile))
                return [];
            try {
                return JsonSerializer.Deserialize<Dictionary<string, ThumbnailData>>(File.ReadAllText(cacheFile))
                    ?? throw new JsonException("Artwork cache is empty");
            }
            catch (JsonException e) {
                Console.WriteLine($"WARN: Unable to read artwork cache; rebuilding it: {e.Message}");
                return [];
            }
        }
    }
}
