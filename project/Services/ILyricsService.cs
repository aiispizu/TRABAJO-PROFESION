namespace AudioRecognitionApp.Services
{
    public interface ILyricsService
    {
        Task<string?> GetLyricsAsync(string songTitle, string artistName);
    }
}
