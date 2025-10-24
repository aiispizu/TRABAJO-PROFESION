using AudioRecognitionApp.Models;

namespace AudioRecognitionApp.Services
{
    public interface IAudioRecognitionService
    {
        Task<SongInfo?> RecognizeAudioAsync(Stream audioStream, string fileName);
    }
}
