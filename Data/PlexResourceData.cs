using System.Text.Json.Serialization;

namespace PlexampRPC.Data {
    public class PlexResourceData {
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
        public IList<PlexConnectionData>? Connections { get; set; }
        public Uri? LocalUri { get; set; }
        public Uri? Uri { get; set; }
    }

    public class PlexConnectionData {
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
}
