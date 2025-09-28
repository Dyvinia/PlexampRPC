using System.Globalization;
using System.Text;
using PlexampRPC.Data;

namespace PlexampRPC.Utils {
    public static class Util {
        public static string ApplyPlaceholders(this string? input) => input.ApplyPlaceholders(null);
        public static string ApplyPlaceholders(this string? input, SessionData? session) {
            return (input ?? string.Empty)
                .Replace("{title}", session?.Title ?? "Title")
                .Replace("{artist}", session?.Artists ?? "Artist")
                .Replace("{album}", session?.Album ?? "Album")
                .Replace("{year}", session?.Year.ToString() ?? "Year")
                .Replace("{player}", session?.Player?.PlayerName ?? "Player")
                .Replace("{listens}", session?.ListenCount.ToString() ?? "Listen Count")
                .Replace("{codec}", session?.Media?.Codec?.ToUpper() ?? "Codec")
                .Replace("{container}", session?.Media?.Container?.ToUpper() ?? "Container")
                .Replace("{bitrate}", session?.Media?.Bitrate.ToString() ?? "Bitrate")
                .Replace("{channel}", session?.Media?.Part?.Stream?.ChannelLayout.ToTitleCase() ?? "Channel Layout")
                .Replace("{bitdepth}", session?.Media?.Part?.Stream?.BitDepth.ToString() ?? "Bit Depth")
                .Replace("{samplerate}", session?.Media?.Part?.Stream?.SampleRateKHz.ToString() ?? "Sample Rate");
        }

        public static string TrimUTF8String(this string? input) => input.TrimUTF8String(128);
        public static string TrimUTF8String(this string? input, int bytes) {
            Encoding.UTF8.GetEncoder().Convert(input.AsSpan(), new byte[bytes], true, out int charsUsed, out _, out _);
            return input?[..charsUsed] ?? string.Empty;
        }

        public static string ToTitleCase(this string? input) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input ?? string.Empty);
    }
}
