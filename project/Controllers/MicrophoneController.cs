using AudioRecognitionApp.Models;
using AudioRecognitionApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioRecognitionApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MicrophoneController : ControllerBase
    {
        private readonly IAudioRecognitionService _audioRecognitionService;
        private readonly ILyricsService _lyricsService;
        private readonly ILogger<MicrophoneController> _logger;

        public MicrophoneController(
            IAudioRecognitionService audioRecognitionService,
            ILyricsService lyricsService,
            ILogger<MicrophoneController> logger)
        {
            _audioRecognitionService = audioRecognitionService;
            _lyricsService = lyricsService;
            _logger = logger;
        }

        [HttpPost("recognize")]
        [ProducesResponseType(typeof(ApiResponse<SongInfo>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> RecognizeFromMicrophone([FromBody] MicrophoneAudioRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.AudioData))
            {
                return BadRequest(new ApiResponse<SongInfo>
                {
                    Success = false,
                    Message = "No se proporcionaron datos de audio."
                });
            }

            try
            {
                // Convertir Base64 a bytes
                var audioData = request.AudioData;

                // Remover el prefijo "data:audio/wav;base64," si existe
                if (audioData.Contains(","))
                {
                    audioData = audioData.Split(',')[1];
                }

                byte[] audioBytes = Convert.FromBase64String(audioData);

                // Crear un stream desde los bytes
                using var memoryStream = new MemoryStream(audioBytes);

                // Reconocer la canción
                var songInfo = await _audioRecognitionService.RecognizeAudioAsync(memoryStream, "microphone-recording.wav");

                if (songInfo == null)
                {
                    return Ok(new ApiResponse<SongInfo>
                    {
                        Success = false,
                        Message = "No se pudo reconocer la canción. Intenta grabar más tiempo o acércate más a la fuente de audio."
                    });
                }

                // Obtener las letras
                var lyrics = await _lyricsService.GetLyricsAsync(songInfo.Title, songInfo.Artist);
                if (!string.IsNullOrEmpty(lyrics))
                {
                    songInfo.Lyrics = lyrics;
                }

                return Ok(new ApiResponse<SongInfo>
                {
                    Success = true,
                    Message = "Canción reconocida exitosamente desde el micrófono.",
                    Data = songInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el audio del micrófono");
                return StatusCode(500, new ApiResponse<SongInfo>
                {
                    Success = false,
                    Message = "Error al procesar el audio. Intenta nuevamente."
                });
            }
        }
    }
}