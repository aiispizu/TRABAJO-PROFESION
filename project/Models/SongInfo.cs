namespace AudioRecognitionApp.Models
{
    public class SongInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string SpotifyUrl { get; set; } = string.Empty;
        public string AppleMusicUrl { get; set; } = string.Empty;
        public string Lyrics { get; set; } = string.Empty;
        public List<string> OtherVersions { get; set; } = new List<string>();
        public string CoverArtUrl { get; set; } = string.Empty;
        public string? AmazonUrl { get; set; }

    }
}
