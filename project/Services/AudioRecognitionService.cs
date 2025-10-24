using AudioRecognitionApp.Models;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AudioRecognitionApp.Services
{
    public class AudioRecognitionService : IAudioRecognitionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AudioRecognitionService> _logger;

        public AudioRecognitionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AudioRecognitionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<SongInfo?> RecognizeAudioAsync(Stream audioStream, string fileName)
        {
            try
            {
                var apiKey = _configuration["ApiSettings:AudDApiKey"];

                if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_AUDD_API_KEY_HERE")
                {
                    _logger.LogWarning("API Key no configurada. Usando datos de demostración.");
                    return GetDemoSongInfo();
                }

                var client = _httpClientFactory.CreateClient();

                using var content = new MultipartFormDataContent();

                var fileContent = new StreamContent(audioStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
                content.Add(fileContent, "file", fileName);
                content.Add(new StringContent(apiKey), "api_token");
                content.Add(new StringContent("apple_music,spotify"), "return");

                var response = await client.PostAsync("https://api.audd.io/", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error en la API: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(jsonResponse);

                if (result["status"]?.ToString() == "success" && result["result"] != null)
                {
                    var songData = result["result"];

                    return new SongInfo
                    {
                        Title = songData["title"]?.ToString() ?? "Desconocido",
                        Artist = songData["artist"]?.ToString() ?? "Desconocido",
                        Album = songData["album"]?.ToString() ?? "Desconocido",
                        ReleaseDate = songData["release_date"]?.ToString() ?? "",
                        Label = songData["label"]?.ToString() ?? "",
                        SpotifyUrl = songData["spotify"]?["external_urls"]?["spotify"]?.ToString() ?? "",
                        AppleMusicUrl = songData["apple_music"]?["url"]?.ToString() ?? "",
                        CoverArtUrl = songData["spotify"]?["album"]?["images"]?[0]?["url"]?.ToString() ?? ""
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reconocer el audio");
                return null;
            }
        }

        private SongInfo GetDemoSongInfo()
        {
            return new SongInfo
            {
                Title = "Canción de Demostración",
                Artist = "Artista Demo",
                Album = "Álbum Demo",
                ReleaseDate = "2024",
                Label = "Demo Label",
                SpotifyUrl = "",
                AppleMusicUrl = "",
                Lyrics = "Esta es una demostración. Configure su API key de AudD en appsettings.json para usar el reconocimiento real.",
                OtherVersions = new List<string> { "Versión Acústica", "Remix" }
            };
        }
    }
}
