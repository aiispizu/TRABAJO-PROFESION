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

            if (!allowedExtensions.Contains(fileExtension))
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
                using var stream = audioFile.OpenReadStream();
                var songInfo = await _audioRecognitionService.RecognizeAudioAsync(stream, audioFile.FileName);

                if (songInfo == null)
                {
                    viewModel.ErrorMessage = "No se pudo reconocer la canción. Intente con otro archivo.";
                    return View("Index", viewModel);
                }

                var lyrics = await _lyricsService.GetLyricsAsync(songInfo.Title, songInfo.Artist);
                if (!string.IsNullOrEmpty(lyrics))
                {
                    songInfo.Lyrics = lyrics;
                }

                viewModel.Result = songInfo;
                viewModel.IsSuccess = true;
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
