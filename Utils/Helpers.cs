using System.Globalization;
using PlexampRPC.Data;

namespace PlexampRPC.Utils {
    public static class Helpers {
        public static string ApplyPlaceholders(this string inString, SessionData? session) {
            return inString
                .Replace("{title}", session?.Title ?? "Title")
                .Replace("{artist}", session?.Artists ?? "Artist")
                .Replace("{album}", session?.Album ?? "Album")
                .Replace("{year}", session?.Year.ToString() ?? "Year")
                .Replace("{player}", session?.Player?.PlayerName ?? "Player")
                .Replace("{listens}", session?.ListenCount.ToString() ?? "Listens")
                .Replace("{codec}", session?.Media?.Codec?.ToUpper() ?? "Codec")
                .Replace("{container}", session?.Media?.Container?.ToUpper() ?? "Container")
                .Replace("{bitrate}", session?.Media?.Bitrate.ToString() ?? "Bitrate")
                .Replace("{channel}", CultureInfo.InvariantCulture.TextInfo.ToTitleCase(session?.Media?.Part?.Stream?.ChannelLayout ?? "") ?? "Channel")
                .Replace("{bitdepth}", session?.Media?.Part?.Stream?.BitDepth.ToString() ?? "Depth")
                .Replace("{samplerate}", session?.Media?.Part?.Stream?.SampleRateKHz.ToString() ?? "Sample Rate");
        }

        public static string ApplyPlaceholders(this string inString) => inString.ApplyPlaceholders(null);
    }
}
