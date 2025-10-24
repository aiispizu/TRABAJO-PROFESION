using Newtonsoft.Json.Linq;

namespace AudioRecognitionApp.Services
{
    public class LyricsService : ILyricsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LyricsService> _logger;

        public LyricsService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<LyricsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> GetLyricsAsync(string songTitle, string artistName)
        {
            try
            {
                var apiKey = _configuration["ApiSettings:GeniusApiKey"];

                if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GENIUS_API_KEY_HERE")
                {
                    _logger.LogWarning("Genius API Key no configurada.");
                    return "Letras no disponibles. Configure su API key de Genius en appsettings.json.";
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var searchQuery = Uri.EscapeDataString($"{songTitle} {artistName}");
                var searchUrl = $"https://api.genius.com/search?q={searchQuery}";

                var response = await client.GetAsync(searchUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error al buscar en Genius API: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(jsonResponse);

                var hits = result["response"]?["hits"];
                if (hits != null && hits.Any())
                {
                    var firstHit = hits.First();
                    var songUrl = firstHit["result"]?["url"]?.ToString();

                    return $"Ver letras en: {songUrl}";
                }

                return "Letras no encontradas.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener letras");
                return null;
            }
        }
    }
}
