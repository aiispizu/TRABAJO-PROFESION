namespace AudioRecognitionApp.Models
{
    public class MicrophoneAudioRequest
    {
        public string AudioData { get; set; } = string.Empty;
        public string MimeType { get; set; } = "audio/wav";
    }
}