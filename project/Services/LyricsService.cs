using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace AudioRecognitionApp.Services
{
    public class LyricsService : ILyricsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<LyricsService> _logger;

        public LyricsService(
            IHttpClientFactory httpClientFactory,
            ILogger<LyricsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> GetLyricsAsync(string songTitle, string artistName)
        {
            try
            {
                var cleanTitle = CleanString(songTitle);
                var cleanArtist = CleanString(artistName);

                if (string.IsNullOrWhiteSpace(cleanTitle) || string.IsNullOrWhiteSpace(cleanArtist))
                {
                    _logger.LogWarning("Título o artista vacío después de limpiar");
                    return null;
                }

                _logger.LogInformation($"Buscando letras para: {cleanTitle} - {cleanArtist}");

                // Obtener las letras
                string? lyrics = null;

                lyrics = await GetLyricsFromLrclib(cleanArtist, cleanTitle);
                if (string.IsNullOrEmpty(lyrics))
                {
                    lyrics = await GetLyricsFromLyricsOvh(cleanArtist, cleanTitle);
                }
                if (string.IsNullOrEmpty(lyrics))
                {
                    lyrics = await GetLyricsFromChartLyrics(cleanArtist, cleanTitle);
                }

                if (string.IsNullOrEmpty(lyrics))
                {
                    _logger.LogWarning("No se pudieron obtener las letras de ninguna API");
                    return null;
                }

                // Detectar el idioma de las letras
                var language = DetectLanguage(lyrics);
                _logger.LogInformation($"Idioma detectado: {language}");

                // Si no es español, traducir
                if (language != "es")
                {
                    _logger.LogInformation($"Traduciendo letras del {GetLanguageName(language)} al español...");
                    var translatedLyrics = await TranslateToSpanish(lyrics, language);

                    if (!string.IsNullOrEmpty(translatedLyrics))
                    {
                        // Devolver ambas versiones
                        var result = $"📝 LETRA ORIGINAL ({GetLanguageName(language)}):\n\n{lyrics}\n\n" +
                                   $"═══════════════════════════════════════\n\n" +
                                   $"🇪🇸 TRADUCCIÓN AL ESPAÑOL:\n\n{translatedLyrics}";

                        _logger.LogInformation("Letras traducidas exitosamente");
                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("No se pudo traducir, devolviendo letra original");
                        return lyrics;
                    }
                }

                _logger.LogInformation("Letras ya en español");
                return lyrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener letras");
                return null;
            }
        }

        private string DetectLanguage(string text)
        {
            var lowerText = text.ToLower();

            // Palabras comunes en español
            var spanishWords = new[] { "que", "el", "la", "de", "en", "y", "un", "por", "con", "no", "una", "su", "para", "es", "al", "lo", "como", "más", "pero", "sus", "se", "yo", "mi", "tu", "donde", "cuando", "soy", "eres", "muy", "bien", "qué", "café", "solo", "hasta", "todo" };

            // Palabras comunes en inglés
            var englishWords = new[] { "the", "be", "to", "of", "and", "a", "in", "that", "have", "i", "it", "for", "not", "on", "with", "he", "as", "you", "do", "at", "my", "your", "me", "is", "are", "love", "till", "way", "will", "through", "only", "till", "fuck", "this" };

            // Palabras comunes en alemán
            var germanWords = new[] { "ist", "sein", "der", "die", "das", "ich", "du", "er", "sie", "es", "und", "in", "zu", "den", "mein", "dein", "mit", "auf", "ein", "eine", "nicht", "sehr", "meine", "wie", "durch", "angst", "wut", "liebe", "blut", "flamme", "gehirn", "herz", "weise", "deshalb", "bewahre" };

            // Palabras comunes en francés
            var frenchWords = new[] { "le", "de", "un", "être", "et", "à", "il", "avoir", "ne", "je", "son", "que", "se", "qui", "ce", "dans", "en", "du", "elle", "au", "pour", "pas", "tu", "mon", "les", "des", "une", "sur" };

            int spanishCount = spanishWords.Count(word => Regex.IsMatch(lowerText, $@"\b{word}\b"));
            int englishCount = englishWords.Count(word => Regex.IsMatch(lowerText, $@"\b{word}\b"));
            int germanCount = germanWords.Count(word => Regex.IsMatch(lowerText, $@"\b{word}\b"));
            int frenchCount = frenchWords.Count(word => Regex.IsMatch(lowerText, $@"\b{word}\b"));

            _logger.LogInformation($"Conteo de palabras - ES: {spanishCount}, EN: {englishCount}, DE: {germanCount}, FR: {frenchCount}");

            // Encontrar el máximo
            var counts = new[] {
                ("es", spanishCount),
                ("en", englishCount),
                ("de", germanCount),
                ("fr", frenchCount)
            };

            var maxLang = counts.OrderByDescending(x => x.Item2).First();

            // Si el conteo máximo es muy bajo, asumir inglés por defecto
            if (maxLang.Item2 < 2)
            {
                _logger.LogInformation("Conteo muy bajo, asumiendo inglés");
                return "en";
            }

            return maxLang.Item1;
        }

        private async Task<string?> TranslateToSpanish(string text, string sourceLanguage)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Dividir el texto en chunks si es muy largo (MyMemory tiene límite de 500 caracteres)
                var chunks = SplitTextIntoChunks(text, 400);
                var translatedChunks = new List<string>();

                int chunkNumber = 0;
                foreach (var chunk in chunks)
                {
                    chunkNumber++;
                    _logger.LogInformation($"Traduciendo chunk {chunkNumber}/{chunks.Count}");

                    var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(chunk)}&langpair={sourceLanguage}|es";

                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var json = JObject.Parse(jsonResponse);

                        var translatedText = json["responseData"]?["translatedText"]?.ToString();

                        if (!string.IsNullOrEmpty(translatedText))
                        {
                            translatedChunks.Add(translatedText);
                        }
                        else
                        {
                            _logger.LogWarning($"Chunk {chunkNumber} no pudo traducirse, usando original");
                            translatedChunks.Add(chunk);
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Error al traducir chunk {chunkNumber}: {response.StatusCode}");
                        translatedChunks.Add(chunk);
                    }

                    // Pequeña pausa para no saturar la API
                    await Task.Delay(100);
                }

                return string.Join("\n", translatedChunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al traducir letras");
                return null;
            }
        }

        private List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            var lines = text.Split('\n');
            var currentChunk = "";

            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length + 1 > maxChunkSize)
                {
                    if (!string.IsNullOrWhiteSpace(currentChunk))
                    {
                        chunks.Add(currentChunk.Trim());
                    }
                    currentChunk = line;
                }
                else
                {
                    currentChunk += (string.IsNullOrEmpty(currentChunk) ? "" : "\n") + line;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(currentChunk.Trim());
            }

            return chunks;
        }

        private string GetLanguageName(string langCode)
        {
            return langCode switch
            {
                "en" => "Inglés",
                "de" => "Alemán",
                "fr" => "Francés",
                "it" => "Italiano",
                "pt" => "Portugués",
                "ja" => "Japonés",
                "ko" => "Coreano",
                "zh" => "Chino",
                "ru" => "Ruso",
                "ar" => "Árabe",
                _ => langCode.ToUpper()
            };
        }

        private async Task<string?> GetLyricsFromLrclib(string artist, string title)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";

                _logger.LogInformation($"Llamando a LRCLIB API");

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"LRCLIB respondió con: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonResponse);

                var plainLyrics = json["plainLyrics"]?.ToString();

                if (!string.IsNullOrWhiteSpace(plainLyrics))
                {
                    return plainLyrics.Trim();
                }

                var syncedLyrics = json["syncedLyrics"]?.ToString();
                if (!string.IsNullOrWhiteSpace(syncedLyrics))
                {
                    var cleaned = Regex.Replace(syncedLyrics, @"\[\d{2}:\d{2}\.\d{2}\]", "");
                    return cleaned.Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener letras de LRCLIB");
                return null;
            }
        }

        private async Task<string?> GetLyricsFromLyricsOvh(string artist, string title)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";

                _logger.LogInformation($"Llamando a Lyrics.ovh");
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Lyrics.ovh respondió con: {response.StatusCode}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonResponse);

                var lyrics = json["lyrics"]?.ToString();

                if (!string.IsNullOrWhiteSpace(lyrics))
                {
                    return lyrics.Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener letras de Lyrics.ovh");
                return null;
            }
        }

        private async Task<string?> GetLyricsFromChartLyrics(string artist, string title)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var url = $"http://api.chartlyrics.com/apiv1.asmx/SearchLyricDirect?artist={Uri.EscapeDataString(artist)}&song={Uri.EscapeDataString(title)}";

                _logger.LogInformation($"Llamando a ChartLyrics");
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"ChartLyrics respondió con: {response.StatusCode}");
                    return null;
                }

                var xmlResponse = await response.Content.ReadAsStringAsync();

                var lyricsMatch = Regex.Match(xmlResponse, @"<Lyric>(.*?)</Lyric>", RegexOptions.Singleline);

                if (lyricsMatch.Success)
                {
                    var lyrics = lyricsMatch.Groups[1].Value;
                    lyrics = System.Net.WebUtility.HtmlDecode(lyrics);

                    if (!string.IsNullOrWhiteSpace(lyrics))
                    {
                        return lyrics.Trim();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener letras de ChartLyrics");
                return null;
            }
        }

        private string CleanString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var clean = input.Trim();

            clean = Regex.Replace(clean, @"\([^)]*\)", "");
            clean = Regex.Replace(clean, @"\[[^\]]*\]", "");
            clean = Regex.Replace(clean, @"\b(feat|ft|featuring)\.?\s+.*", "", RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\s+", " ").Trim();

            return clean;
        }
    }
}