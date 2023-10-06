using System;
using System.Text.Json.Serialization;

namespace PlexampRPC {
    public class SessionData {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("originalTitle")]
        public string? TrackArtist { private get; set; }

        [JsonPropertyName("grandparentTitle")]
        public string? AlbumArtist { private get; set; }

        public string? Artists {
            get {
                string artists = TrackArtist ?? AlbumArtist ?? "Artist";
                if (artists.Contains(';')) {
                    string[] artistList = artists.Split(';', StringSplitOptions.TrimEntries);
                    if (artistList.Length > 2)
                        artists = String.Join(", ", artistList);
                    else artists = String.Join(" & ", artistList);
                }
                return artists;
            }
        }

        [JsonPropertyName("parentTitle")]
        public string? Album { get; set; }

        [JsonPropertyName("thumb")]
        public string? ArtPath { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        public UserData? User { get; set; }
        public class UserData {
            [JsonPropertyName("title")]
            public string? Name { get; set; }
        }

        [JsonPropertyName("guid")]
        public string? Guid { get; set; }

        public PlayerData? Player { get; set; }
        public class PlayerData {
            [JsonPropertyName("state")]
            public string? State { get; set; }
        }


        [JsonPropertyName("viewOffset")]
        public int ViewOffset { get; set; }
    }
}
