using AudioRecognitionApp.Models;
using AudioRecognitionApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioRecognitionApp.Controllers
{
    public class AudioController : Controller
    {
        private readonly IAudioRecognitionService _audioRecognitionService;
        private readonly ILyricsService _lyricsService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AudioController> _logger;

        public AudioController(
            IAudioRecognitionService audioRecognitionService,
            ILyricsService lyricsService,
            IConfiguration configuration,
            ILogger<AudioController> logger)
        {
            _audioRecognitionService = audioRecognitionService;
            _lyricsService = lyricsService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new AudioUploadViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile audioFile)
        {
            var viewModel = new AudioUploadViewModel();

            if (audioFile == null || audioFile.Length == 0)
            {
                viewModel.ErrorMessage = "Por favor, seleccione un archivo de audio.";
                return View("Index", viewModel);
            }

            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>();
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();

            if (allowedExtensions != null && !allowedExtensions.Contains(fileExtension))
            {
                viewModel.ErrorMessage = $"Formato de archivo no válido. Solo se permiten: {string.Join(", ", allowedExtensions)}";
                return View("Index", viewModel);
            }

            var maxFileSizeInMB = _configuration.GetValue<int>("FileUpload:MaxFileSizeInMB");
            if (audioFile.Length > maxFileSizeInMB * 1024 * 1024)
            {
                viewModel.ErrorMessage = $"El archivo es demasiado grande. Tamaño máximo: {maxFileSizeInMB}MB";
                return View("Index", viewModel);
            }

            try
            {
                _logger.LogInformation($"Procesando archivo: {audioFile.FileName}");

                using var stream = audioFile.OpenReadStream();
                var songInfo = await _audioRecognitionService.RecognizeAudioAsync(stream, audioFile.FileName);

                if (songInfo == null)
                {
                    viewModel.ErrorMessage = "No se pudo reconocer la canción. Intente con otro archivo.";
                    return View("Index", viewModel);
                }

                _logger.LogInformation($"Canción reconocida: {songInfo.Title} - {songInfo.Artist}");
                _logger.LogInformation($"CoverArtUrl en controller: {songInfo.CoverArtUrl}");

                // Obtener letras
                try
                {
                    _logger.LogInformation($"Buscando letras para: {songInfo.Title} - {songInfo.Artist}");
                    songInfo.Lyrics = await _lyricsService.GetLyricsAsync(songInfo.Title, songInfo.Artist);

                    if (!string.IsNullOrEmpty(songInfo.Lyrics))
                    {
                        try
                        {
                            _logger.LogInformation($"Buscando letras para: {songInfo.Title} - {songInfo.Artist}");
                            var lyrics = await _lyricsService.GetLyricsAsync(songInfo.Title, songInfo.Artist);

                            if (!string.IsNullOrEmpty(lyrics))
                            {
                                songInfo.Lyrics = lyrics;
                                _logger.LogInformation("Letras obtenidas exitosamente");
                            }
                            else
                            {
                                _logger.LogWarning("No se encontraron letras para esta canción");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al obtener letras");
                            // Continuar sin letras
                        }

                        // ↓↓↓ AÑADIR ESTAS LÍNEAS AQUÍ ↓↓↓
                        // Generar URL de Amazon
                        // Generar URL de Amazon (solo álbum)
                        if (!string.IsNullOrEmpty(songInfo.Album))
                        {
                            songInfo.AmazonUrl = $"https://www.amazon.es/s?k={Uri.EscapeDataString($"{songInfo.Artist} {songInfo.Album}")}&i=popular";
                            _logger.LogInformation($"Amazon URL generada: {songInfo.AmazonUrl}");
                        }
                        else
                        {
                            // Si no hay álbum, usar artista + título
                            songInfo.AmazonUrl = $"https://www.amazon.es/s?k={Uri.EscapeDataString($"{songInfo.Artist} {songInfo.Title}")}&i=popular";
                            _logger.LogInformation($"Amazon URL generada (sin álbum): {songInfo.AmazonUrl}");
                        }
                        // ↑↑↑ HASTA AQUÍ ↑↑↑

                        viewModel.Result = songInfo;
                        viewModel.IsSuccess = true;
                        _logger.LogInformation("Letras obtenidas exitosamente");
                    }
                    else
                    {
                        _logger.LogWarning("No se encontraron letras para esta canción");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al obtener letras");
                    // Continuar sin letras
                }

                viewModel.Result = songInfo;
                viewModel.IsSuccess = true;

                _logger.LogInformation($"ViewModel Result - CoverArtUrl: {viewModel.Result?.CoverArtUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el archivo de audio");
                viewModel.ErrorMessage = "Ocurrió un error al procesar el archivo. Intente nuevamente.";
            }

            return View("Index", viewModel);
        }
    }
}