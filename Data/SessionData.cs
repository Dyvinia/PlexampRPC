using System;
using System.Text.Json.Serialization;

namespace PlexampRPC.Data {
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
                        artists = string.Join(", ", artistList);
                    else artists = string.Join(" & ", artistList);
                }
                return artists;
            }
        }

        [JsonPropertyName("parentTitle")]
        public string? Album { get; set; }

        [JsonPropertyName("parentYear")]
        public int Year { get; set; }

        [JsonPropertyName("thumb")]
        public string? ArtPath { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }


        [JsonPropertyName("viewCount")]
        public int ListenCount { get; set; }

        [JsonPropertyName("viewOffset")]
        public int ProgressOffset { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonIgnore]
        public MediaData? Media => MediaArray?.Length > 0 ? MediaArray[0] : null;

        [JsonPropertyName("Media")]
        public MediaData[]? MediaArray { get; set; }
        public class MediaData {
            [JsonPropertyName("audioCodec")]
            public string? Codec { get; set; }

            [JsonPropertyName("bitrate")]
            public int Bitrate { get; set; }


            [JsonPropertyName("container")]
            public string? Container { get; set; }


            [JsonIgnore]
            public PartData? Part => PartArray?.Length > 0 ? PartArray[0] : null;

            [JsonPropertyName("Part")]
            public PartData[]? PartArray { get; set; }
            public class PartData {
                [JsonIgnore]
                public StreamData? Stream => StreamArray?.Length > 0 ? StreamArray[0] : null;

                [JsonPropertyName("Stream")]
                public StreamData[]? StreamArray { get; set; }
                public class StreamData {
                    [JsonPropertyName("audioChannelLayout")]
                    public string? ChannelLayout { get; set; }

                    [JsonPropertyName("bitDepth")]
                    public int BitDepth { get; set; }

                    [JsonPropertyName("samplingRate")]
                    public int SampleRate { get; set; }
                    public float SampleRateKHz => SampleRate / 1000f;
                }
            }
        }

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

            [JsonPropertyName("title")]
            public string? PlayerName { get; set; }
        }

        [JsonPropertyName("key")]
        public string? Key { get; set; }
    }
}
