using PlexampRPC.Utils;

namespace PlexampRPC.Data {
    public class PresenceData {
        public string Line1 { get => field.TrimUTF8String().PadRight(2); set; }
        public string Line2 { get => field.TrimUTF8String().PadRight(2); set; }
        public string Line3 { get => field.TrimUTF8String().PadRight(2); set; }
        public string ArtLink { get; set; } = "https://raw.githubusercontent.com/Dyvinia/PlexampRPC/master/Resources/PlexIconSquare.png";
        public string? State { get; set; }
        public int TimeOffset { get; set; }
        public int Duration { get; set; }
        public string? Url { get; set; }
    }
}
