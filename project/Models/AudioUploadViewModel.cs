namespace AudioRecognitionApp.Models
{
    public class AudioUploadViewModel
    {
        public IFormFile? AudioFile { get; set; }
        public SongInfo? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
    }
}
