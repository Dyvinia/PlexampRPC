using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PlexampRPC {
    public class Connection {
        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("local")]
        public bool Local { get; set; }

        [JsonPropertyName("relay")]
        public bool Relay { get; set; }

        [JsonPropertyName("IPv6")]
        public bool IPv6 { get; set; }
    }

    public class Resource {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("product")]
        public string? Product { get; set; }

        [JsonPropertyName("productVersion")]
        public string? ProductVersion { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("platformVersion")]
        public string? PlatformVersion { get; set; }

        [JsonPropertyName("device")]
        public string? Device { get; set; }

        [JsonPropertyName("clientIdentifier")]
        public string? ClientIdentifier { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("lastSeenAt")]
        public DateTime LastSeenAt { get; set; }

        [JsonPropertyName("provides")]
        public string? Provides { get; set; }

        [JsonPropertyName("ownerId")]
        public int? OwnerId { get; set; }

        [JsonPropertyName("sourceTitle")]
        public string? SourceTitle { get; set; }

        [JsonPropertyName("publicAddress")]
        public string? PublicAddress { get; set; }

        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("owned")]
        public bool Owned { get; set; }

        [JsonPropertyName("home")]
        public bool Home { get; set; }

        [JsonPropertyName("synced")]
        public bool Synced { get; set; }

        [JsonPropertyName("relay")]
        public bool Relay { get; set; }

        [JsonPropertyName("presence")]
        public bool Presence { get; set; }

        [JsonPropertyName("httpsRequired")]
        public bool HttpsRequired { get; set; }

        [JsonPropertyName("publicAddressMatches")]
        public bool PublicAddressMatches { get; set; }

        [JsonPropertyName("dnsRebindingProtection")]
        public bool DnsRebindingProtection { get; set; }

        [JsonPropertyName("natLoopbackSupported")]
        public bool NatLoopbackSupported { get; set; }

        [JsonPropertyName("connections")]
        public IList<Connection>? Connections { get; set; }
        public Uri? LocalUri { get; set; }
        public Uri? Uri { get; set; }

        public static async Task<Resource[]?> GetAccountResources() {
            try {
                HttpRequestMessage requestMessage = new(HttpMethod.Get, $"https://plex.tv/api/v2/resources?includeHttps=1&includeIPv6=1&X-Plex-Token={App.Token}&X-Plex-Client-Identifier=PlexampRPC");
                requestMessage.Headers.Add("Accept", "application/json");

                HttpResponseMessage sendResponse = await MainWindow.httpClient.SendAsync(requestMessage);
                sendResponse.EnsureSuccessStatusCode();

                JsonDocument responseJson = JsonDocument.Parse(await sendResponse.Content.ReadAsStringAsync());
                Resource[]? source_resources = JsonSerializer.Deserialize<Resource[]>(responseJson.RootElement);
                List<Resource>? filtered_resources = new List<Resource>();

                for (int i = 0; i < source_resources?.Length; i++) {
                    Resource? r = await TestResource(source_resources[i]);
                    if (r != null)
                        filtered_resources.Add(r);
                }

                return filtered_resources.ToArray();
            } catch (Exception e) {
                Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                return null;
            }
        }

        public static async Task<Resource?> TestResource(Resource resource) {
            if (!(resource.Provides ?? "").Split(",").Contains("server")) {
                Console.WriteLine($"INFO: Skipping {resource.Name}/{resource.Product}, not a server");
                return null;
            }
            TimeSpan TimeoutBackup = MainWindow.httpClient.Timeout;
            if (Config.Settings.LocalAddress)
                MainWindow.httpClient.Timeout = TimeSpan.FromSeconds(1);
            foreach (Connection connection in resource.Connections ?? Enumerable.Empty<Connection>()) {
                Uri Uri;
                if (connection.Local)
                    Uri = new UriBuilder("http", connection.Address, connection.Port).Uri;
                else
                    Uri = new UriBuilder(connection.Uri).Uri;
                try {
                    Console.WriteLine($"INFO: Testing {(connection.Local ? 'L' : 'R')} {Uri}status/sessions?X-Plex-Token={resource.AccessToken?.Substring(0, 3)}...");
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, $"{Uri}status/sessions?X-Plex-Token={resource.AccessToken}");
                    requestMessage.Headers.Add("Accept", "application/json");

                    HttpResponseMessage sendResponse = await MainWindow.httpClient.SendAsync(requestMessage);
                    sendResponse.EnsureSuccessStatusCode();
                    Console.WriteLine($"INFO: Success {(connection.Local ? 'L' : 'R')} {Uri}status/sessions?X-Plex-Token={resource.AccessToken?.Substring(0, 3)}...");
                    if (connection.Local)
                        resource.LocalUri ??= Uri;
                    else
                        resource.Uri ??= Uri;
                    if (resource.LocalUri != null && resource.Uri != null) {

                    }
                } catch (TaskCanceledException) {
                    Console.WriteLine($"WARN: Timeout {(connection.Local ? 'L' : 'R')} {Uri}status/sessions?X-Plex-Token={resource.AccessToken?.Substring(0, 3)}...");
                    // Unreachable local server, skip
                } catch (HttpRequestException e) {
                    Console.WriteLine($"WARN: Unable to access {Uri}status/sessions: {e.Message}");
                    if (e.StatusCode == HttpStatusCode.Forbidden) break;
                } catch (Exception e) {
                    Console.WriteLine($"WARN: Unable to get resource: {e.Message} {e.InnerException}");
                }
            }
            MainWindow.httpClient.Timeout = TimeoutBackup;
            if (resource.LocalUri == null && resource.Uri == null)
                return null;
            return resource;
        }
    }
}
