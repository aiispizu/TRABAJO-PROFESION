# AudioRecognitionApp - Aplicación de Reconocimiento de Audio

Aplicación web ASP.NET Core MVC para reconocer canciones a partir de archivos de audio MP3 y WAV.

## Características

- Subida y análisis de archivos de audio (.mp3, .wav)
- Reconocimiento automático de canciones usando AudD API (gratuita)
- Obtención de letras usando Genius API (gratuita)
- API REST interna para integración externa
- Interfaz moderna con Bootstrap 5
- Drag & drop para subir archivos

## APIs Gratuitas Utilizadas

### 1. AudD API (Reconocimiento de Audio)
- **Web:** https://audd.io/
- **Plan Gratuito:** 100 requests/día
- **Registro:** https://dashboard.audd.io/
- **Funcionalidad:** Identifica canciones, artista, álbum, links a Spotify/Apple Music

### 2. Genius API (Letras)
- **Web:** https://genius.com/api-clients
- **Plan Gratuito:** Disponible
- **Registro:** https://genius.com/signup_or_login
- **Funcionalidad:** Búsqueda de letras y metadata de canciones

## Configuración

### 1. Obtener API Keys

#### AudD API:
1. Ir a https://dashboard.audd.io/
2. Crear cuenta gratuita
3. Copiar el API Token

#### Genius API:
1. Ir a https://genius.com/api-clients
2. Crear una "API Client"
3. Generar un "Access Token"
4. Copiar el token

### 2. Configurar appsettings.json

```json
{
  "ApiSettings": {
    "AudDApiKey": "TU_AUDD_API_KEY_AQUI",
    "GeniusApiKey": "TU_GENIUS_API_KEY_AQUI"
  }
}
```

## Estructura del Proyecto

```
AudioRecognitionApp/
├── Controllers/
│   ├── AudioController.cs          # Controlador MVC principal
│   └── ApiAudioController.cs       # API REST
├── Models/
│   ├── SongInfo.cs                 # Modelo de información de canción
│   ├── AudioUploadViewModel.cs     # ViewModel para la vista
│   └── ApiResponse.cs              # Modelo de respuesta API
├── Services/
│   ├── IAudioRecognitionService.cs # Interfaz del servicio
│   ├── AudioRecognitionService.cs  # Lógica de reconocimiento
│   ├── ILyricsService.cs           # Interfaz del servicio de letras
│   └── LyricsService.cs            # Lógica de obtención de letras
├── Views/
│   ├── Audio/
│   │   └── Index.cshtml            # Vista principal
│   └── Shared/
│       └── _Layout.cshtml          # Layout principal
├── appsettings.json                # Configuración
└── Program.cs                      # Punto de entrada
```

## Ejecución Local

### Requisitos:
- Visual Studio 2022 o superior
- .NET 8.0 SDK
- Conexión a internet (para APIs)

### Pasos:

1. Abrir el proyecto en Visual Studio 2022
2. Restaurar paquetes NuGet (automático)
3. Configurar API keys en `appsettings.json`
4. Presionar F5 o ejecutar con IIS Express
5. La aplicación abrirá en https://localhost:xxxxx

### Sin Visual Studio (CLI):

```bash
dotnet restore
dotnet run
```

## Uso de la Aplicación

### Interfaz Web:
1. Navegar a la página principal
2. Arrastrar un archivo de audio o hacer clic en "Seleccionar Archivo"
3. Hacer clic en "Analizar Audio"
4. Ver los resultados: título, artista, álbum, letras, links a streaming

### API REST:

#### Endpoint: POST /api/apiaudio/recognize

**Request:**
```bash
curl -X POST https://localhost:5001/api/apiaudio/recognize \
  -F "audioFile=@cancion.mp3"
```

**Response:**
```json
{
  "success": true,
  "message": "Canción reconocida exitosamente.",
  "data": {
    "title": "Nombre de la Canción",
    "artist": "Artista",
    "album": "Álbum",
    "releaseDate": "2024",
    "label": "Sello Discográfico",
    "spotifyUrl": "https://open.spotify.com/...",
    "appleMusicUrl": "https://music.apple.com/...",
    "lyrics": "Letras...",
    "otherVersions": [],
    "coverArtUrl": "https://..."
  }
}
```

#### Endpoint: GET /api/apiaudio/health

Verifica el estado de la API.

## Extensiones Futuras

El código está preparado para agregar:

- ✅ Historial de búsquedas (usar Supabase)
- ✅ Sistema de favoritos
- ✅ Recomendaciones basadas en búsquedas
- ✅ Búsqueda de covers y remixes
- ✅ Integración con más servicios de streaming
- ✅ Exportación de resultados (PDF, JSON)

## Notas de Seguridad

- Las API keys deben estar en `appsettings.json` (nunca en el código)
- Para producción, usar User Secrets o Azure Key Vault
- Validación de tamaño y tipo de archivo implementada
- Protección CSRF habilitada en formularios

## Limitaciones del Plan Gratuito

- **AudD:** 100 reconocimientos/día
- **Genius:** Rate limit estándar (suficiente para uso normal)

Para uso intensivo, considerar planes de pago.

## Soporte

Para problemas o preguntas:
- Revisar la documentación de las APIs
- Verificar que las API keys sean correctas
- Comprobar límites de uso de las APIs gratuitas
