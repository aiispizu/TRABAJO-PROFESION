// Clase para manejar la grabación de audio desde el micrófono
class MicrophoneRecorder {
    constructor() {
        this.mediaRecorder = null;
        this.audioChunks = [];
        this.stream = null;
        this.isRecording = false;
    }

    // Solicitar permisos y configurar el micrófono
    async initialize() {
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({ 
                audio: {
                    echoCancellation: true,
                    noiseSuppression: true,
                    sampleRate: 44100
                } 
            });
            return true;
        } catch (error) {
            console.error('Error al acceder al micrófono:', error);
            throw new Error('No se pudo acceder al micrófono. Verifica los permisos.');
        }
    }

    // Iniciar la grabación
    async startRecording() {
        if (this.isRecording) {
            return;
        }

        if (!this.stream) {
            await this.initialize();
        }

        this.audioChunks = [];
        
        // Configurar MediaRecorder
        const options = { mimeType: 'audio/webm' };
        
        // Intentar con diferentes tipos MIME según el navegador
        if (!MediaRecorder.isTypeSupported(options.mimeType)) {
            options.mimeType = 'audio/ogg; codecs=opus';
            if (!MediaRecorder.isTypeSupported(options.mimeType)) {
                options.mimeType = 'audio/wav';
            }
        }

        this.mediaRecorder = new MediaRecorder(this.stream, options);

        this.mediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                this.audioChunks.push(event.data);
            }
        };

        this.mediaRecorder.start();
        this.isRecording = true;
    }

    // Detener la grabación
    stopRecording() {
        return new Promise((resolve, reject) => {
            if (!this.isRecording || !this.mediaRecorder) {
                reject(new Error('No hay grabación en curso'));
                return;
            }

            this.mediaRecorder.onstop = () => {
                const audioBlob = new Blob(this.audioChunks, { type: 'audio/wav' });
                this.isRecording = false;
                resolve(audioBlob);
            };

            this.mediaRecorder.onerror = (error) => {
                reject(error);
            };

            this.mediaRecorder.stop();
        });
    }

    // Convertir Blob a Base64
    async blobToBase64(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onloadend = () => resolve(reader.result);
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });
    }

    // Limpiar recursos
    cleanup() {
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }
        this.isRecording = false;
        this.audioChunks = [];
    }
}

// Funciones globales para la interfaz
let recorder = null;
let recordingStartTime = null;
let timerInterval = null;

async function initializeMicrophone() {
    try {
        if (!recorder) {
            recorder = new MicrophoneRecorder();
        }
        await recorder.initialize();
        return true;
    } catch (error) {
        showError('Error al inicializar el micrófono: ' + error.message);
        return false;
    }
}

async function startMicrophoneRecording() {
    const startBtn = document.getElementById('startRecordBtn');
    const stopBtn = document.getElementById('stopRecordBtn');
    const statusText = document.getElementById('recordingStatus');
    const timerDisplay = document.getElementById('recordingTimer');

    try {
        // Inicializar si es necesario
        if (!recorder) {
            const initialized = await initializeMicrophone();
            if (!initialized) return;
        }

        // Iniciar grabación
        await recorder.startRecording();

        // Actualizar UI
        startBtn.classList.add('d-none');
        stopBtn.classList.remove('d-none');
        statusText.textContent = 'Grabando...';
        statusText.classList.add('text-danger');
        
        // Iniciar temporizador
        recordingStartTime = Date.now();
        updateTimer();
        timerInterval = setInterval(updateTimer, 100);

    } catch (error) {
        showError('Error al iniciar la grabación: ' + error.message);
    }
}

async function stopMicrophoneRecording() {
    const startBtn = document.getElementById('startRecordBtn');
    const stopBtn = document.getElementById('stopRecordBtn');
    const statusText = document.getElementById('recordingStatus');
    const analyzeBtn = document.getElementById('analyzeMicBtn');

    try {
        // Detener temporizador
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }

        // Detener grabación
        const audioBlob = await recorder.stopRecording();

        // Actualizar UI
        stopBtn.classList.add('d-none');
        analyzeBtn.classList.remove('d-none');
        statusText.textContent = 'Grabación completada';
        statusText.classList.remove('text-danger');
        statusText.classList.add('text-success');

        // Guardar el blob para análisis posterior
        window.currentAudioBlob = audioBlob;

    } catch (error) {
        showError('Error al detener la grabación: ' + error.message);
        startBtn.classList.remove('d-none');
        stopBtn.classList.add('d-none');
        statusText.textContent = '';
    }
}

async function analyzeMicrophoneRecording() {
    const analyzeBtn = document.getElementById('analyzeMicBtn');
    const statusText = document.getElementById('recordingStatus');
    const resultsDiv = document.getElementById('microphoneResults');

    if (!window.currentAudioBlob) {
        showError('No hay grabación para analizar');
        return;
    }

    try {
        // Mostrar estado de carga
        analyzeBtn.disabled = true;
        analyzeBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Analizando...';
        statusText.textContent = 'Analizando audio...';

        // Convertir a Base64
        const base64Audio = await recorder.blobToBase64(window.currentAudioBlob);

        // Enviar al servidor
        const response = await fetch('/api/Microphone/recognize', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                audioData: base64Audio,
                mimeType: 'audio/wav'
            })
        });

        const result = await response.json();

        if (result.success && result.data) {
            displayMicrophoneResults(result.data);
            statusText.textContent = 'Análisis completado';
        } else {
            showError(result.message || 'No se pudo reconocer la canción');
            statusText.textContent = '';
        }

    } catch (error) {
        showError('Error al analizar el audio: ' + error.message);
        statusText.textContent = '';
    } finally {
        analyzeBtn.disabled = false;
        analyzeBtn.innerHTML = '<i class="bi bi-search me-2"></i>Analizar Audio';
        resetMicrophoneRecording();
    }
}

function updateTimer() {
    const timerDisplay = document.getElementById('recordingTimer');
    if (!recordingStartTime) return;

    const elapsed = Date.now() - recordingStartTime;
    const seconds = Math.floor(elapsed / 1000);
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;

    timerDisplay.textContent = `${minutes.toString().padStart(2, '0')}:${remainingSeconds.toString().padStart(2, '0')}`;
}

function resetMicrophoneRecording() {
    const startBtn = document.getElementById('startRecordBtn');
    const stopBtn = document.getElementById('stopRecordBtn');
    const analyzeBtn = document.getElementById('analyzeMicBtn');
    const timerDisplay = document.getElementById('recordingTimer');

    startBtn.classList.remove('d-none');
    stopBtn.classList.add('d-none');
    analyzeBtn.classList.add('d-none');
    timerDisplay.textContent = '00:00';
    
    window.currentAudioBlob = null;
    recordingStartTime = null;
}

function displayMicrophoneResults(songInfo) {
    const resultsDiv = document.getElementById('microphoneResults');
    
    let html = `
        <div class="result-card animate__animated animate__fadeIn">
            <h3 class="mb-4">
                <i class="bi bi-check-circle-fill text-success me-2"></i>
                Canción Identificada desde Micrófono
            </h3>
    `;

    if (songInfo.coverArtUrl) {
        html += `
            <div class="text-center mb-4">
                <img src="${songInfo.coverArtUrl}" 
                     alt="Portada" 
                     class="img-fluid rounded shadow" 
                     style="max-width: 300px;" />
            </div>
        `;
    }

    html += `
        <div class="song-info-item">
            <strong><i class="bi bi-music-note me-2"></i>Título:</strong>
            <span class="float-end">${songInfo.title}</span>
        </div>
        <div class="song-info-item">
            <strong><i class="bi bi-person me-2"></i>Artista:</strong>
            <span class="float-end">${songInfo.artist}</span>
        </div>
    `;

    if (songInfo.album) {
        html += `
            <div class="song-info-item">
                <strong><i class="bi bi-disc me-2"></i>Álbum:</strong>
                <span class="float-end">${songInfo.album}</span>
            </div>
        `;
    }

    if (songInfo.releaseDate) {
        html += `
            <div class="song-info-item">
                <strong><i class="bi bi-calendar-event me-2"></i>Fecha de Lanzamiento:</strong>
                <span class="float-end">${songInfo.releaseDate}</span>
            </div>
        `;
    }

    if (songInfo.label) {
        html += `
            <div class="song-info-item">
                <strong><i class="bi bi-building me-2"></i>Sello Discográfico:</strong>
                <span class="float-end">${songInfo.label}</span>
            </div>
        `;
    }

    html += '<div class="mt-4">';

    if (songInfo.spotifyUrl) {
        html += `
            <a href="${songInfo.spotifyUrl}" 
               target="_blank" 
               class="btn btn-success me-2 mb-2">
                <i class="bi bi-spotify me-2"></i>Escuchar en Spotify
            </a>
        `;
    }

    if (songInfo.appleMusicUrl) {
        html += `
            <a href="${songInfo.appleMusicUrl}" 
               target="_blank" 
               class="btn btn-dark me-2 mb-2">
                <i class="bi bi-music-note-beamed me-2"></i>Escuchar en Apple Music
            </a>
        `;
    }

    if (songInfo.amazonUrl) {
        html += `
            <a href="${songInfo.amazonUrl}" 
               target="_blank" 
               class="btn btn-warning mb-2">
                <i class="bi bi-cart-fill me-2"></i>Comprar en Amazon
            </a>
        `;
    }

    html += '</div>';

    if (songInfo.lyrics) {
        html += `
            <div class="mt-4">
                <h5><i class="bi bi-file-text me-2"></i>Letras</h5>
                <div class="lyrics-container">${songInfo.lyrics}</div>
            </div>
        `;
    }

    html += '</div>';

    resultsDiv.innerHTML = html;
}

function showError(message) {
    const alertDiv = document.createElement('div');
    alertDiv.className = 'alert alert-danger alert-dismissible fade show';
    alertDiv.innerHTML = `
        <i class="bi bi-exclamation-triangle-fill me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    const container = document.querySelector('.microphone-section');
    container.insertBefore(alertDiv, container.firstChild);

    setTimeout(() => {
        alertDiv.remove();
    }, 5000);
}

// Limpiar recursos cuando se cierra la página
window.addEventListener('beforeunload', () => {
    if (recorder) {
        recorder.cleanup();
    }
});
