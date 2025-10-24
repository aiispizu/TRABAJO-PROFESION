using AudioRecognitionApp.Models;
using AudioRecognitionApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioRecognitionApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiAudioController : ControllerBase
    {
        private readonly IAudioRecognitionService _audioRecognitionService;
        private readonly ILyricsService _lyricsService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiAudioController> _logger;

        public ApiAudioController(
            IAudioRecognitionService audioRecognitionService,
            ILyricsService lyricsService,
            IConfiguration configuration,
            ILogger<ApiAudioController> logger)
        {
            _audioRecognitionService = audioRecognitionService;
            _lyricsService = lyricsService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("recognize")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<SongInfo>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> RecognizeAudio([FromForm] IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest(new ApiResponse<SongInfo>
                {
                    Success = false,
                    Message = "No se proporcionó ningún archivo de audio."
                });
            }

            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>();
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new ApiResponse<SongInfo>
                {
                    Success = false,
                    Message = $"Formato no válido. Formatos permitidos: {string.Join(", ", allowedExtensions)}"
                });
            }

            try
            {
                using var stream = audioFile.OpenReadStream();
                var songInfo = await _audioRecognitionService.RecognizeAudioAsync(stream, audioFile.FileName);

                if (songInfo == null)
                {
                    return Ok(new ApiResponse<SongInfo>
                    {
                        Success = false,
                        Message = "No se pudo reconocer la canción."
                    });
                }

                var lyrics = await _lyricsService.GetLyricsAsync(songInfo.Title, songInfo.Artist);
                if (!string.IsNullOrEmpty(lyrics))
                {
                    songInfo.Lyrics = lyrics;
                }

                return Ok(new ApiResponse<SongInfo>
                {
                    Success = true,
                    Message = "Canción reconocida exitosamente.",
                    Data = songInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el archivo de audio en API");
                return StatusCode(500, new ApiResponse<SongInfo>
                {
                    Success = false,
                    Message = "Error interno del servidor al procesar el archivo."
                });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
