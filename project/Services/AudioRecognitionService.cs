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

        // Método que espera la interfaz
        public async Task<SongInfo?> RecognizeAudioAsync(Stream audioStream, string fileName)
        {
            using var memoryStream = new MemoryStream();
            await audioStream.CopyToAsync(memoryStream);
            var audioData = memoryStream.ToArray();

            return await RecognizeSongAsync(audioData);
        }

        private async Task<SongInfo?> RecognizeSongAsync(byte[] audioData)
        {
            SongInfo? result = null;

            // Intentar con AudD (principal)
            result = await RecognizeWithAudD(audioData);
            if (result != null)
            {
                _logger.LogInformation("Canción reconocida con AudD");
                return result;
            }

            // Intentar con Shazam (backup)
            result = await RecognizeWithShazam(audioData);
            if (result != null)
            {
                _logger.LogInformation("Canción reconocida con Shazam");
                return result;
            }

            _logger.LogWarning("No se pudo reconocer la canción con ninguna API");
            return null;
        }

        private async Task<SongInfo?> RecognizeWithAudD(byte[] audioData)
        {
            try
            {
                var apiKey = _configuration["ApiSettings:AudDApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("API Key de AudD no configurada");
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var base64Audio = Convert.ToBase64String(audioData);

                var content = new MultipartFormDataContent();
                content.Add(new StringContent(apiKey), "api_token");
                content.Add(new StringContent(base64Audio), "audio");
                content.Add(new StringContent("apple_music,spotify"), "return");

                _logger.LogInformation("Enviando solicitud a AudD API...");
                var response = await client.PostAsync("https://api.audd.io/", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"AudD respondió con: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Respuesta recibida de AudD");

                var json = JObject.Parse(jsonResponse);

                if (json["status"]?.ToString() != "success" || json["result"] == null)
                {
                    _logger.LogWarning("AudD no pudo reconocer la canción");
                    return null;
                }

                var result = json["result"];
                var title = result["title"]?.ToString();
                var artist = result["artist"]?.ToString();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
                {
                    _logger.LogWarning("Respuesta de AudD incompleta");
                    return null;
                }

                var songInfo = new SongInfo
                {
                    Title = title,
                    Artist = artist,
                    Album = result["album"]?.ToString(),
                    ReleaseDate = result["release_date"]?.ToString(),
                    Label = result["label"]?.ToString()
                };

                // Obtener URLs de streaming
                var appleMusic = result["apple_music"];
                if (appleMusic != null)
                {
                    songInfo.AppleMusicUrl = appleMusic["url"]?.ToString();
                }

                var spotify = result["spotify"];
                if (spotify != null)
                {
                    songInfo.SpotifyUrl = spotify["external_urls"]?["spotify"]?.ToString();

                    var album = spotify["album"];
                    if (album != null && string.IsNullOrEmpty(songInfo.CoverArtUrl))
                    {
                        var images = album["images"] as JArray;
                        if (images != null && images.Count > 0)
                        {
                            songInfo.CoverArtUrl = images[0]?["url"]?.ToString();
                            _logger.LogInformation($"Portada obtenida de Spotify: {songInfo.CoverArtUrl}");
                        }
                    }
                }

                _logger.LogInformation($"Canción reconocida: {songInfo.Title} - {songInfo.Artist}");
                _logger.LogInformation($"SongInfo creado - CoverArtUrl: {songInfo.CoverArtUrl}");

                return songInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reconocer con AudD");
                return null;
            }
        }

        private async Task<SongInfo?> RecognizeWithShazam(byte[] audioData)
        {
            try
            {
                var rapidApiKey = _configuration["ApiSettings:RapidApiKey"];

                if (string.IsNullOrWhiteSpace(rapidApiKey))
                {
                    _logger.LogWarning("RapidAPI Key no configurada para Shazam");
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", rapidApiKey);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", "shazam.p.rapidapi.com");

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(audioData), "upload_file", "audio.mp3");

                _logger.LogInformation("Enviando solicitud a Shazam API...");
                var response = await client.PostAsync("https://shazam.p.rapidapi.com/songs/v2/detect", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Shazam respondió con: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Respuesta recibida de Shazam");

                var json = JObject.Parse(jsonResponse);

                var track = json["track"];
                if (track == null)
                {
                    _logger.LogWarning("Shazam no pudo reconocer la canción");
                    return null;
                }

                var title = track["title"]?.ToString();
                var artist = track["subtitle"]?.ToString();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(artist))
                {
                    _logger.LogWarning("Respuesta de Shazam incompleta");
                    return null;
                }

                var songInfo = new SongInfo
                {
                    Title = title,
                    Artist = artist
                };

                // Obtener imagen
                var images = track["images"];
                if (images != null)
                {
                    songInfo.CoverArtUrl = images["coverart"]?.ToString();
                }

                // Obtener URLs de streaming
                var hub = track["hub"];
                if (hub != null)
                {
                    var actions = hub["actions"] as JArray;
                    if (actions != null)
                    {
                        foreach (var action in actions)
                        {
                            var uri = action["uri"]?.ToString();
                            if (!string.IsNullOrEmpty(uri))
                            {
                                if (uri.Contains("spotify"))
                                {
                                    songInfo.SpotifyUrl = uri;
                                }
                                else if (uri.Contains("apple"))
                                {
                                    songInfo.AppleMusicUrl = uri;
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation($"Canción reconocida: {songInfo.Title} - {songInfo.Artist}");
                return songInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reconocer con Shazam");
                return null;
            }
        }
    }
}