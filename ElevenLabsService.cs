using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CK3_Reader
{
    public class ElevenLabsService
    {
        private string? _apiKey;
        private readonly HttpClient _httpClient;
        private readonly MediaPlayer _mediaPlayer;
        private const string API_BASE_URL = "https://api.elevenlabs.io";
        
        // Callback for playback progress updates
        public Action<double, double>? OnPlaybackProgress { get; set; } // (currentSeconds, totalSeconds)

        public ElevenLabsService()
        {
            _httpClient = new HttpClient();
            _mediaPlayer = new MediaPlayer();
        }
        
        /// <summary>
        /// Gets the current audio duration in seconds (0 if no audio loaded)
        /// </summary>
        public double GetAudioDuration()
        {
            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                return _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }
            return 0;
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey?.Trim();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Remove("xi-api-key");
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", _apiKey);
            }
        }

        public bool HasApiKey()
        {
            return !string.IsNullOrWhiteSpace(_apiKey);
        }

        /// <summary>
        /// Sets the volume for audio playback (0.0 to 1.0)
        /// </summary>
        public void SetVolume(double volume)
        {
            _mediaPlayer.Volume = Math.Max(0.0, Math.Min(1.0, volume));
        }

        /// <summary>
        /// Sets the playback speed (0.5 to 2.0, where 1.0 is normal speed)
        /// </summary>
        public void SetSpeed(double speed)
        {
            _mediaPlayer.SpeedRatio = Math.Max(0.5, Math.Min(2.0, speed));
        }

        /// <summary>
        /// Converts text to speech using ElevenLabs Text to Dialogue API with Eleven v3 model
        /// This method accepts a single text string with a single voice
        /// </summary>
        /// <param name="text">The text to convert to speech (supports audio tags like [laughing], [sad], etc.)</param>
        /// <param name="voiceId">The ElevenLabs voice ID (default: Adam)</param>
        public async Task<bool> TextToSpeechAsync(string text, string voiceId = "pNInz6obpgDQGcFmaJgB")
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key is not set");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                // Prepare the request payload for Text to Dialogue API
                // The API expects an "inputs" array with dialogue entries
                var requestBody = new
                {
                    inputs = new[]
                    {
                        new
                        {
                            text = text,
                            voice_id = voiceId
                        }
                    },
                    model_id = "eleven_v3",
                    output_format = "mp3_44100_128"
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Use the Text to Dialogue API endpoint for Eleven v3
                var response = await _httpClient.PostAsync(
                    $"{API_BASE_URL}/v1/text-to-dialogue",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
                }

                // Get the audio stream
                var audioStream = await response.Content.ReadAsStreamAsync();

                // Save to temporary file
                var tempFile = Path.Combine(Path.GetTempPath(), $"ck3_tts_{Guid.NewGuid()}.mp3");
                using (var fileStream = File.Create(tempFile))
                {
                    await audioStream.CopyToAsync(fileStream);
                }

                // Play the audio
                await PlayAudioAsync(tempFile);

                // Clean up temp file after playback
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating speech: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts multi-speaker dialogue to speech using ElevenLabs Text to Dialogue API with Eleven v3 model
        /// This method accepts parsed dialogue with multiple speakers
        /// </summary>
        /// <param name="dialogue">The parsed dialogue structure from DialogueParser</param>
        public async Task<bool> TextToSpeechMultiSpeakerAsync(DialogueParser.ElevenLabsDialogue dialogue)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key is not set");
            }

            if (dialogue == null || dialogue.Inputs == null || dialogue.Inputs.Count == 0)
            {
                return false;
            }

            try
            {
                // Prepare the request payload for Text to Dialogue API
                var requestBody = new
                {
                    inputs = dialogue.Inputs.Select(input => new
                    {
                        text = input.Text,
                        voice_id = input.VoiceId
                    }).ToArray(),
                    model_id = dialogue.ModelId,
                    output_format = "mp3_44100_128",
                    settings = dialogue.Settings != null ? new
                    {
                        stability = dialogue.Settings.Stability,
                        similarity_boost = dialogue.Settings.SimilarityBoost,
                        style = dialogue.Settings.Style,
                        use_speaker_boost = dialogue.Settings.UseSpeakerBoost
                    } : null
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Use the Text to Dialogue API endpoint for Eleven v3
                var response = await _httpClient.PostAsync(
                    $"{API_BASE_URL}/v1/text-to-dialogue",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
                }

                // Get the audio stream
                var audioStream = await response.Content.ReadAsStreamAsync();

                // Save to temporary file
                var tempFile = Path.Combine(Path.GetTempPath(), $"ck3_tts_{Guid.NewGuid()}.mp3");
                using (var fileStream = File.Create(tempFile))
                {
                    await audioStream.CopyToAsync(fileStream);
                }

                // Play the audio
                await PlayAudioAsync(tempFile);

                // Clean up temp file after playback
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating multi-speaker speech: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts OpenAI response directly to speech by parsing and using multi-speaker TTS
        /// </summary>
        /// <param name="openAiResponse">The raw OpenAI response with speaker names</param>
        /// <param name="stability">Voice stability setting (0.0 to 1.0, default 0.5)</param>
        /// <param name="similarityBoost">Similarity boost setting (0.0 to 1.0, default 0.75)</param>
        /// <param name="style">Style/expressiveness setting (0.0 to 1.0, default 0.0)</param>
        /// <param name="useSpeakerBoost">Whether to use speaker boost (default true)</param>
        public async Task<bool> TextToSpeechFromOpenAIAsync(
            string openAiResponse,
            double stability = 0.5,
            double similarityBoost = 0.75,
            double style = 0.0,
            bool useSpeakerBoost = true)
        {
            if (string.IsNullOrWhiteSpace(openAiResponse))
            {
                return false;
            }

            // Parse the OpenAI response into dialogue format
            var dialogue = DialogueParser.ParseAndConvert(openAiResponse, stability, similarityBoost, style, useSpeakerBoost);
            
            // Use the multi-speaker TTS method
            return await TextToSpeechMultiSpeakerAsync(dialogue);
        }

        /// <summary>
        /// Converts text to speech using ElevenLabs Flash v2.5 model (standard TTS endpoint)
        /// This method bypasses the dialogue API and uses the faster flash model
        /// </summary>
        /// <param name="text">The text to convert to speech</param>
        /// <param name="voiceId">The ElevenLabs voice ID (default: Daniel)</param>
        /// <param name="stability">Voice stability setting (0.0 to 1.0, default 0.5)</param>
        /// <param name="similarityBoost">Similarity boost setting (0.0 to 1.0, default 0.75)</param>
        public async Task<bool> TextToSpeechFlashAsync(
            string text,
            string voiceId = "yhf80q1381zd2JJQ4tM7",
            double stability = 0.5,
            double similarityBoost = 0.75)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key is not set");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                // Prepare the request payload for standard TTS API with flash model
                var requestBody = new
                {
                    text = text,
                    model_id = "eleven_flash_v2_5",
                    voice_settings = new
                    {
                        stability = stability,
                        similarity_boost = similarityBoost
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Use the standard text-to-speech endpoint with flash model
                var response = await _httpClient.PostAsync(
                    $"{API_BASE_URL}/v1/text-to-speech/{voiceId}",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"ElevenLabs API error: {response.StatusCode} - {errorContent}");
                }

                // Get the audio stream
                var audioStream = await response.Content.ReadAsStreamAsync();

                // Save to temporary file
                var tempFile = Path.Combine(Path.GetTempPath(), $"ck3_tts_{Guid.NewGuid()}.mp3");
                using (var fileStream = File.Create(tempFile))
                {
                    await audioStream.CopyToAsync(fileStream);
                }

                // Play the audio
                await PlayAudioAsync(tempFile);

                // Clean up temp file after playback
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating speech with Flash model: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Plays an audio file using MediaPlayer with progress reporting
        /// </summary>
        private Task PlayAudioAsync(string filePath)
        {
            var tcs = new TaskCompletionSource<bool>();
            System.Windows.Threading.DispatcherTimer? progressTimer = null;

            // Define event handlers that can be removed later
            EventHandler? mediaOpenedHandler = null;
            EventHandler? mediaEndedHandler = null;
            EventHandler<ExceptionEventArgs>? mediaFailedHandler = null;

            mediaOpenedHandler = (s, e) =>
            {
                // Apply speed ratio after media is opened and ready
                // This ensures the current SpeedRatio setting is used
                _mediaPlayer.SpeedRatio = _mediaPlayer.SpeedRatio;
                
                // Start progress reporting timer
                if (_mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    double totalSeconds = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    
                    progressTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100) // Update every 100ms
                    };
                    
                    progressTimer.Tick += (ts, te) =>
                    {
                        double currentSeconds = _mediaPlayer.Position.TotalSeconds;
                        OnPlaybackProgress?.Invoke(currentSeconds, totalSeconds);
                    };
                    
                    progressTimer.Start();
                }
            };

            mediaEndedHandler = (s, e) =>
            {
                // Stop progress timer
                progressTimer?.Stop();
                
                // Remove event handlers to prevent memory leaks and duplicate calls
                _mediaPlayer.MediaOpened -= mediaOpenedHandler;
                _mediaPlayer.MediaEnded -= mediaEndedHandler;
                _mediaPlayer.MediaFailed -= mediaFailedHandler;
                
                // Don't call Stop() here - the media has already ended naturally
                // Calling Stop() can cut off the last bit of audio
                // The MediaPlayer will be ready for the next file when Open() is called again
                tcs.TrySetResult(true);
            };

            mediaFailedHandler = (s, e) =>
            {
                // Stop progress timer
                progressTimer?.Stop();
                
                // Remove event handlers to prevent memory leaks and duplicate calls
                _mediaPlayer.MediaOpened -= mediaOpenedHandler;
                _mediaPlayer.MediaEnded -= mediaEndedHandler;
                _mediaPlayer.MediaFailed -= mediaFailedHandler;
                
                _mediaPlayer.Stop();
                tcs.TrySetException(new Exception($"Media playback failed: {e.ErrorException?.Message}"));
            };

            // Attach event handlers
            _mediaPlayer.MediaOpened += mediaOpenedHandler;
            _mediaPlayer.MediaEnded += mediaEndedHandler;
            _mediaPlayer.MediaFailed += mediaFailedHandler;

            _mediaPlayer.Open(new Uri(filePath));
            _mediaPlayer.Play();

            return tcs.Task;
        }

        /// <summary>
        /// Stops any currently playing audio
        /// </summary>
        public void StopPlayback()
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }

        public void Dispose()
        {
            _mediaPlayer?.Close();
            _httpClient?.Dispose();
        }
    }
}