using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CK3_Reader
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ClaudeService _claudeService;
        private readonly ElevenLabsService _elevenLabsService;
        
        // Flag to prevent saving during initialization
        private bool _isInitializing = true;
        
        // Voice quality settings
        private double _stability = 0.5;
        private double _similarityBoost = 0.75;
        private double _style = 0.0;
        private bool _useSpeakerBoost = true;
        private bool _skipOpenAI = false;
        private string _danielVoiceId = "yhf80q1381zd2JJQ4tM7";
        
        // Voice configuration
        private VoiceConfigCollection _voiceConfig = new VoiceConfigCollection();
        private ObservableCollection<VoiceConfig> _voices = new ObservableCollection<VoiceConfig>();
        
        string eventText = "";
        string[] formatting = [
            // Tooltip patterns
            @"TOOLTIP:SCALED_STATIC_MODIFIER,\w+,\d+\.\d+,\w+,\w+",
            @"TOOLTIP:\w+,\w+,\d+",
            @"TOOLTIP:\w+,\d+",
            @"TOOLTIP:\w+,\w+",
            @"TOOLTIP:\w+",
            
            // OnClick patterns
            @"ONCLICK:\w+,\d+",
            @"ONCLICK:\w+,\w+",
            @"ONCLICK:\w+",
            
            // Formatting codes
            @"indent_newline:\d+",
            @"newline:\d+",
            
            // Icon patterns (more comprehensive)
            @"portrait_punishment_icon!",
            @"death_icon!",
            @"skill_\w+_icon!",
            @"\w+_icon_\w+!",
            @"\w+_icon!",
            @"_icon!",
            @"icon_\w+!",
            
            // Value and color patterns
            @"positive_value",
            @"negative_value",
            @"COLOR_\w+_\w+",
            @"COLOR_\w+",
            
            // Game-specific patterns
            @"stress_\w+",
            @"skill_\w+",
            @"trait_\w+",
            @"modifier_\w+",
            @"\bEMP\b",                // EMP formatting code
            @"\b[A-Z]{2,}\b(?![a-z])", // All-caps words (2+ letters) that aren't followed by lowercase
            
            // Hidden/Special characters (IMPORTANT - add these first!)
            @"[\x00-\x1F\x7F-\x9F]",     // Control characters (non-printable)
            @"\u200B",                    // Zero-width space
            @"\u200C",                    // Zero-width non-joiner
            @"\u200D",                    // Zero-width joiner
            @"\uFEFF",                    // Zero-width no-break space (BOM)
            @"\u00A0",                    // Non-breaking space
            @"\u2028",                    // Line separator
            @"\u2029",                    // Paragraph separator
            @"[\u2000-\u200F]",           // Various Unicode spaces
            @"[\u202A-\u202E]",           // Bidirectional text control
            
            // Cleanup patterns (order matters!)
            @"\bL\b",              // Single 'L' character
            @";\s*",               // Semicolon with optional spaces
            @"!+",                 // One or more exclamation marks
            @"_{2,}",              // Multiple underscores
            @"\[[\w\s]+\]",        // Square bracket tags like [GetTrait]
            @"#\w+",               // Hash tags like #high
            @"\$\w+\$",            // Dollar sign variables
            @"@\w+!",              // At-sign references
            @"\s{2,}"              // Multiple whitespace characters (keep at end to collapse spaces)
        ];

        public MainWindow()
        {
            InitializeComponent();
            _claudeService = new ClaudeService();
            _elevenLabsService = new ElevenLabsService();

            // Initialize voice DataGrid
            voicesDataGrid.ItemsSource = _voices;

            // Load saved settings (including voices)
            LoadSettings();

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logPath = documents + "\\Paradox Interactive\\Crusader Kings III\\logs\\";
            string debugLog = logPath + "debug.log";

            if (File.Exists(debugLog))
            {
                txtStatus.Text = "✔️ Ready - Monitoring CK3 debug.log";
            }
            else
            {
                txtStatus.Text = "❌ CK3 debug.log not found at: " + debugLog;
            }

            // Mark initialization as complete
            _isInitializing = false;

            Loaded += MainWindow_Loaded;
        }

        private void LoadSettings()
        {
            try
            {
                // Load Claude API key
                string savedApiKey = Properties.Settings.Default.OpenAIApiKey;
                if (!string.IsNullOrWhiteSpace(savedApiKey))
                {
                    txtApiKey.Password = savedApiKey;
                    _claudeService.SetApiKey(savedApiKey);
                    txtApiStatus.Text = "✔️ API key set - Auto-processing enabled";
                    txtApiStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                }

                // Load ElevenLabs API key
                string savedElevenLabsKey = Properties.Settings.Default.ElevenLabsApiKey;
                if (!string.IsNullOrWhiteSpace(savedElevenLabsKey))
                {
                    txtElevenLabsApiKey.Password = savedElevenLabsKey;
                    _elevenLabsService.SetApiKey(savedElevenLabsKey);
                    txtElevenLabsStatus.Text = "✔️ API key set - TTS enabled";
                    txtElevenLabsStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                }

                // Load volume setting
                double savedVolume = Properties.Settings.Default.Volume;
                if (savedVolume > 0)
                {
                    volumeSlider.Value = savedVolume;
                    _elevenLabsService.SetVolume(savedVolume / 100.0);
                }

                // Load speed setting
                double savedSpeed = Properties.Settings.Default.PlaybackSpeed;
                if (savedSpeed > 0)
                {
                    speedSlider.Value = savedSpeed;
                    _elevenLabsService.SetSpeed(savedSpeed / 100.0);
                }

                // Load voice quality settings
                _stability = Properties.Settings.Default.Stability;
                stabilitySlider.Value = _stability * 100;
                
                _similarityBoost = Properties.Settings.Default.SimilarityBoost;
                similaritySlider.Value = _similarityBoost * 100;
                
                _style = Properties.Settings.Default.Style;
                styleSlider.Value = _style * 100;
                
                _useSpeakerBoost = Properties.Settings.Default.UseSpeakerBoost;
                speakerBoostCheckbox.IsChecked = _useSpeakerBoost;

                // Load skip OpenAI setting
                _skipOpenAI = Properties.Settings.Default.SkipOpenAI;
                skipOpenAICheckbox.IsChecked = _skipOpenAI;
                UpdateModeInfo();

                // Load custom voices
                string savedVoicesJson = Properties.Settings.Default.CustomVoicesJson;
                if (!string.IsNullOrWhiteSpace(savedVoicesJson))
                {
                    try
                    {
                        _voiceConfig = JsonSerializer.Deserialize<VoiceConfigCollection>(savedVoicesJson) ?? new VoiceConfigCollection();
                    }
                    catch
                    {
                        _voiceConfig = GetDefaultVoices();
                    }
                }
                else
                {
                    _voiceConfig = GetDefaultVoices();
                }

                // Populate the ObservableCollection for the DataGrid
                _voices.Clear();
                foreach (var voice in _voiceConfig.Voices)
                {
                    _voices.Add(voice);
                }

                // Update DialogueParser and ClaudeService with voice configuration
                DialogueParser.UpdateVoiceMapping(_voiceConfig);
                _claudeService.SetVoiceConfiguration(_voiceConfig);
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"⚠️ Error loading settings: {ex.Message}";
            }
        }

        private void SaveSettings()
        {
            // Don't save during initialization
            if (_isInitializing)
            {
                return;
            }

            try
            {
                Properties.Settings.Default.OpenAIApiKey = txtApiKey.Password;
                Properties.Settings.Default.ElevenLabsApiKey = txtElevenLabsApiKey.Password;
                Properties.Settings.Default.Volume = volumeSlider.Value;
                Properties.Settings.Default.PlaybackSpeed = speedSlider.Value;
                Properties.Settings.Default.Stability = _stability;
                Properties.Settings.Default.SimilarityBoost = _similarityBoost;
                Properties.Settings.Default.Style = _style;
                Properties.Settings.Default.UseSpeakerBoost = _useSpeakerBoost;
                Properties.Settings.Default.SkipOpenAI = _skipOpenAI;
                
                // Save custom voices
                _voiceConfig.Voices = new System.Collections.Generic.List<VoiceConfig>(_voices);
                string voicesJson = JsonSerializer.Serialize(_voiceConfig);
                Properties.Settings.Default.CustomVoicesJson = voicesJson;
                
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"⚠️ Error saving settings: {ex.Message}";
            }
        }

        private VoiceConfigCollection GetDefaultVoices()
        {
            return new VoiceConfigCollection
            {
                Voices = new System.Collections.Generic.List<VoiceConfig>
                {
                    new VoiceConfig { Name = "Main", VoiceId = "yhf80q1381zd2JJQ4tM7", Gender = "Male", Description = "Narrator/Player - mature male, authoritative" },
                    new VoiceConfig { Name = "Sarah", VoiceId = "EXAVITQu4vr4xnSDxMaL", Gender = "Female", Description = "Young woman, soft-spoken" },
                    new VoiceConfig { Name = "Clyde", VoiceId = "2EiwWnXFnvU5JabPnv8n", Gender = "Male", Description = "Middle-aged man, warm" }
                }
            };
        }

        private void UpdateVoiceConfiguration()
        {
            // Filter out incomplete voices (skip rows with missing name or voice ID)
            var validVoices = _voices.Where(v =>
                !string.IsNullOrWhiteSpace(v.Name) &&
                !string.IsNullOrWhiteSpace(v.VoiceId)).ToList();
            
            _voiceConfig.Voices = validVoices;

            // Silently validate - only check if we have at least 1 male and 1 female among valid voices
            if (_voiceConfig.IsValid(out string errorMessage))
            {
                // Update DialogueParser and ClaudeService only if valid
                DialogueParser.UpdateVoiceMapping(_voiceConfig);
                _claudeService.SetVoiceConfiguration(_voiceConfig);
            }

            // Always save settings (even if validation fails, we save what we have)
            SaveSettings();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await Task.Run(() => RunLoop(_cancellationTokenSource.Token));
        }

        private void RunLoop(CancellationToken token)
        {
            string counter = string.Empty;
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logPath = documents + "\\Paradox Interactive\\Crusader Kings III\\logs\\";
            string debugLog = logPath + "debug.log";
            string beginPattern = "<event-text>";
            string endPattern = "</event-text>";
            bool startMessage = false;
            
            // Debug tracking
            long lastFileSize = 0;
            DateTime lastModified = DateTime.MinValue;
            int linesRead = 0;
            string lastLine = "";
            int eventMarkersFound = 0;

            try
            {
                // Initial file info
                FileInfo fileInfo = new FileInfo(debugLog);
                lastFileSize = fileInfo.Length;
                lastModified = fileInfo.LastWriteTime;
                
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = $"📂 Log file: {debugLog}\n" +
                                   $"📊 Size: {lastFileSize:N0} bytes\n" +
                                   $"🕒 Modified: {lastModified:HH:mm:ss}\n" +
                                   $"✅ Monitoring from end of file...";
                });

                using (FileStream stream = new FileStream(debugLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    StreamReader reader = new StreamReader(stream);
                    long startPosition = stream.Length;
                    stream.Seek(0, SeekOrigin.End); // Start reading from the end of the file

                    Dispatcher.Invoke(() =>
                    {
                        txtEvent.Text = $"🔍 DEBUG INFO:\n" +
                                       $"Starting position: {startPosition:N0} bytes\n" +
                                       $"Waiting for new events...\n" +
                                       $"Looking for: {beginPattern} and {endPattern}\n\n" +
                                       $"💡 TIP: The app only detects NEW events after it starts.\n" +
                                       $"Trigger an event in CK3 to test!";
                    });

                    while (!token.IsCancellationRequested)
                    {
                        string? line = reader.ReadLine();

                        // Visual counter to show the app is running
                        counter += ".";
                        if (counter.Length > 6)
                        {
                            counter = string.Empty;
                        }

                        if (line != null)
                        {
                            linesRead++;
                            lastLine = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                            
                            // Check file size changes
                            if (linesRead % 100 == 0)
                            {
                                fileInfo.Refresh();
                                if (fileInfo.Length != lastFileSize || fileInfo.LastWriteTime != lastModified)
                                {
                                    lastFileSize = fileInfo.Length;
                                    lastModified = fileInfo.LastWriteTime;
                                }
                            }
                            // Check for start of event
                            if (line.Contains(beginPattern))
                            {
                                eventMarkersFound++;
                                eventText = string.Empty;
                                startMessage = true;
                                
                                Dispatcher.Invoke(() =>
                                {
                                    txtStatus.Text = $"🎯 Event marker found! (#{eventMarkersFound})";
                                    txtEvent.Text = $"📥 Collecting event text...\nLine: {line}";
                                });
                            }

                            // Collect event text
                            if (startMessage)
                            {
                                eventText += "\n";
                                eventText += Regex.Replace(line, @".*\<event-text\>", "");

                                // Check for end of event or max length
                                if (line.Contains(endPattern) || eventText.Length > 10000)
                                {
                                    // Remove end tag
                                    eventText = eventText.Replace(endPattern, "");
                                    startMessage = false;

                                    // Store raw text before cleanup for debugging
                                    string rawEventText = eventText;

                                    // Apply formatting filters to clean up CK3 markup
                                    foreach (var format in formatting)
                                    {
                                        eventText = Regex.Replace(eventText, format, " ");
                                    }

                                    // Update UI with both raw and cleaned event text
                                    Dispatcher.Invoke(async () =>
                                    {
                                        // Stop any currently playing audio when a new event is detected
                                        _elevenLabsService.StopPlayback();
                                        
                                        txtStatus.Text = "✔️ Event found";
                                        
                                        // Show user-friendly current event
                                        txtCurrentEvent.Text = eventText.Trim();
                                        
                                        // Show debug info only if debug mode is enabled
                                        if (debugModeCheckbox.IsChecked == true)
                                        {
                                            txtEvent.Text = "=== RAW CK3 LOG CONTENT ===\n" + rawEventText.Trim() +
                                                           "\n\n=== CLEANED TEXT ===\n" + eventText.Trim();
                                        }
                                        
                                        // Check if we should skip OpenAI and go directly to ElevenLabs Flash
                                        if (_skipOpenAI && _elevenLabsService.HasApiKey())
                                        {
                                            try
                                            {
                                                txtStatus.Text = "🔊 Generating speech with Flash v2.5...";
                                                txtLlmTranslation.Text = "⚡ Skipping OpenAI - Using direct Flash v2.5 mode";
                                                txtElevenLabsTtsStatus.Text = "🔊 Generating speech with ElevenLabs Flash v2.5...";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                                
                                                // Use Flash model directly with the raw event text
                                                await _elevenLabsService.TextToSpeechFlashAsync(
                                                    eventText.Trim(),
                                                    _danielVoiceId, // Daniel voice (configurable)
                                                    _stability,
                                                    _similarityBoost);
                                                
                                                txtStatus.Text = "✔️ Speech playback complete (Flash mode)";
                                                txtElevenLabsTtsStatus.Text = "✔️ Flash v2.5 speech playback complete";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                                            }
                                            catch (Exception ttsEx)
                                            {
                                                txtStatus.Text = $"⚠️ TTS Error: {ttsEx.Message}";
                                                txtElevenLabsTtsStatus.Text = $"❌ Error: {ttsEx.Message}";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                                            }
                                        }
                                        // Automatically process with Claude if API key is set
                                        else if (_claudeService.HasApiKey())
                                        {
                                            try
                                            {
                                                txtLlmTranslation.Text = "Processing with Claude...";
                                                txtElevenLabsTtsStatus.Text = "Waiting for Claude processing...";
                                                
                                                string aiResponse = await _claudeService.ProcessEventTextAsync(eventText.Trim());
                                                
                                                // Show in current event (user-friendly)
                                                txtCurrentEvent.Text = aiResponse;
                                                
                                                // Show debug info only if debug mode is enabled
                                                if (debugModeCheckbox.IsChecked == true)
                                                {
                                                    txtLlmTranslation.Text = aiResponse;
                                                    
                                                    // Parse the dialogue to show what was extracted
                                                    var parsedEntries = DialogueParser.ParseOpenAIResponse(aiResponse);
                                                    var parsedText = new System.Text.StringBuilder();
                                                    parsedText.AppendLine($"Total speakers found: {parsedEntries.Count}\n");
                                                    
                                                    foreach (var entry in parsedEntries)
                                                    {
                                                        var textPreview = entry.Text.Length > 60 ? entry.Text.Substring(0, 60) + "..." : entry.Text;
                                                        parsedText.AppendLine($"🎤 {entry.VoiceName}");
                                                        parsedText.AppendLine($"   Voice ID: {entry.VoiceId}");
                                                        parsedText.AppendLine($"   Text: {textPreview}");
                                                        parsedText.AppendLine();
                                                    }
                                                    
                                                    txtParsedDialogue.Text = parsedText.ToString();
                                                }

                                                // Automatically play with ElevenLabs if API key is set
                                                if (_elevenLabsService.HasApiKey())
                                                {
                                                    try
                                                    {
                                                        txtStatus.Text = "🔊 Generating multi-speaker speech...";
                                                        txtElevenLabsTtsStatus.Text = "🔊 Generating multi-speaker speech with ElevenLabs v3...";
                                                        txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                                        
                                                        // Use the new multi-speaker method that parses OpenAI response with voice quality settings
                                                        await _elevenLabsService.TextToSpeechFromOpenAIAsync(
                                                            aiResponse,
                                                            _stability,
                                                            _similarityBoost,
                                                            _style,
                                                            _useSpeakerBoost);
                                                        
                                                        txtStatus.Text = "✔️ Speech playback complete";
                                                        txtElevenLabsTtsStatus.Text = "✔️ Multi-speaker speech playback complete";
                                                        txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                                                    }
                                                    catch (Exception ttsEx)
                                                    {
                                                        txtStatus.Text = $"⚠️ TTS Error: {ttsEx.Message}";
                                                        txtElevenLabsTtsStatus.Text = $"❌ Error: {ttsEx.Message}";
                                                        txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                                                    }
                                                }
                                                else
                                                {
                                                    txtStatus.Text = "✔️ Event processed (TTS disabled - set ElevenLabs API key)";
                                                    txtElevenLabsTtsStatus.Text = "⚠️ Set ElevenLabs API key in the sidebar to enable TTS";
                                                    txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                txtLlmTranslation.Text = $"Error processing with AI: {ex.Message}";
                                                txtElevenLabsTtsStatus.Text = "⚠️ Claude processing failed";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                                            }
                                        }
                                        else
                                        {
                                            if (_skipOpenAI)
                                            {
                                                txtLlmTranslation.Text = "⚠️ Set ElevenLabs API key in the sidebar to enable Flash mode.";
                                                txtElevenLabsTtsStatus.Text = "⚠️ ElevenLabs API key required for Flash mode";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                                            }
                                            else
                                            {
                                                txtLlmTranslation.Text = "⚠️ Set Claude API key in the sidebar to enable automatic translation.";
                                                txtElevenLabsTtsStatus.Text = "⚠️ Claude API key required";
                                                txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                                            }
                                        }
                                    });
                                    
                                    // Reset for next event
                                    eventText = string.Empty;
                                }
                            }
                        }
                        else
                        {
                            // No new line available, wait a bit
                            Thread.Sleep(20); // Check every 20ms
                        }

                        // Update status counter with debug info
                        if (!startMessage && linesRead % 50 == 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (!txtStatus.Text.Contains("Event found") && !txtStatus.Text.Contains("Event marker"))
                                {
                                    txtStatus.Text = $"📡 Monitoring{counter}\n" +
                                                   $"Lines read: {linesRead:N0}\n" +
                                                   $"File size: {lastFileSize:N0} bytes\n" +
                                                   $"Events found: {eventMarkersFound}";
                                    
                                    if (!string.IsNullOrEmpty(lastLine))
                                    {
                                        txtEvent.Text = $"🔍 DEBUG INFO:\n" +
                                                       $"Lines scanned: {linesRead:N0}\n" +
                                                       $"Events detected: {eventMarkersFound}\n" +
                                                       $"File size: {lastFileSize:N0} bytes\n" +
                                                       $"Last modified: {lastModified:HH:mm:ss}\n\n" +
                                                       $"Last line read:\n{lastLine}\n\n" +
                                                       $"💡 Waiting for '{beginPattern}' marker...";
                                    }
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = "❌ Error: " + ex.Message;
                });
            }
        }

        private void StopLoop()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void Window_closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopLoop();
            _elevenLabsService.StopPlayback();
            _elevenLabsService.Dispose();
            SaveSettings();
        }

        private void TxtApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as System.Windows.Controls.PasswordBox;
            if (passwordBox != null)
            {
                string apiKey = passwordBox.Password;
                _claudeService.SetApiKey(apiKey);
                
                if (_claudeService.HasApiKey())
                {
                    txtApiStatus.Text = "✔️ API key set - Auto-processing enabled";
                    txtApiStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                }
                else
                {
                    txtApiStatus.Text = "⚠️ No API key set";
                    txtApiStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                }
                
                SaveSettings();
            }
        }

        private void TxtElevenLabsApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as System.Windows.Controls.PasswordBox;
            if (passwordBox != null)
            {
                string apiKey = passwordBox.Password;
                _elevenLabsService.SetApiKey(apiKey);
                
                if (_elevenLabsService.HasApiKey())
                {
                    txtElevenLabsStatus.Text = "✔️ API key set - TTS enabled";
                    txtElevenLabsStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                }
                else
                {
                    txtElevenLabsStatus.Text = "⚠️ No API key set";
                    txtElevenLabsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                }
                
                SaveSettings();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (volumeLabel != null && _elevenLabsService != null)
            {
                volumeLabel.Text = $"{(int)e.NewValue}%";
                _elevenLabsService.SetVolume(e.NewValue / 100.0);
                SaveSettings();
            }
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (speedLabel != null && _elevenLabsService != null)
            {
                double speed = e.NewValue / 100.0;
                speedLabel.Text = $"{speed:F1}x";
                _elevenLabsService.SetSpeed(speed);
                SaveSettings();
            }
        }

        private void StabilitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (stabilityLabel != null)
            {
                _stability = e.NewValue / 100.0;
                stabilityLabel.Text = $"{_stability:F2}";
                SaveSettings();
            }
        }

        private void SimilaritySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (similarityLabel != null)
            {
                _similarityBoost = e.NewValue / 100.0;
                similarityLabel.Text = $"{_similarityBoost:F2}";
                SaveSettings();
            }
        }

        private void StyleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (styleLabel != null)
            {
                _style = e.NewValue / 100.0;
                styleLabel.Text = $"{_style:F2}";
                SaveSettings();
            }
        }

        private void SpeakerBoostCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (speakerBoostCheckbox != null)
            {
                _useSpeakerBoost = speakerBoostCheckbox.IsChecked ?? true;
                SaveSettings();
            }
        }

        private void SkipOpenAICheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (skipOpenAICheckbox != null)
            {
                _skipOpenAI = skipOpenAICheckbox.IsChecked ?? false;
                UpdateModeInfo();
                SaveSettings();
            }
        }

        private void UpdateModeInfo()
        {
            if (txtModeInfo != null)
            {
                if (_skipOpenAI)
                {
                    txtModeInfo.Text = "⚡ Flash Mode: Events will be sent directly to ElevenLabs Flash v2.5 (faster, no Claude processing). Settings are saved automatically.";
                }
                else
                {
                    txtModeInfo.Text = "Events will be automatically processed with Claude and played via ElevenLabs TTS. Settings are saved automatically.";
                }
            }
        }

        private void BtnAddVoice_Click(object sender, RoutedEventArgs e)
        {
            // Add a new empty voice to the collection
            var newVoice = new VoiceConfig
            {
                Name = "NewVoice",
                VoiceId = "",
                Gender = "Male"
            };
            _voices.Add(newVoice);
            
            // Select the new row for editing
            voicesDataGrid.SelectedItem = newVoice;
            voicesDataGrid.ScrollIntoView(newVoice);
        }

        private void BtnRemoveVoice_Click(object sender, RoutedEventArgs e)
        {
            if (voicesDataGrid.SelectedItem is VoiceConfig selectedVoice)
            {
                // Prevent removing the "Main" voice
                if (selectedVoice.Name == "Main")
                {
                    MessageBox.Show("The 'Main' voice cannot be removed. This is the narrator/player voice and is required.",
                                  "Cannot Remove Main Voice",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }
                
                // Just remove it - validation will happen silently in UpdateVoiceConfiguration
                _voices.Remove(selectedVoice);
                UpdateVoiceConfiguration();
            }
        }

        private void BtnResetVoices_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset all voices to the default configuration. Are you sure?",
                "Reset Voices",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _voiceConfig = GetDefaultVoices();
                _voices.Clear();
                foreach (var voice in _voiceConfig.Voices)
                {
                    _voices.Add(voice);
                }
                UpdateVoiceConfiguration();
            }
        }

        private async void BtnTestVoice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is VoiceConfig voice)
            {
                // Trim whitespace from voice ID
                string voiceId = voice.VoiceId?.Trim() ?? "";
                
                if (string.IsNullOrWhiteSpace(voiceId))
                {
                    txtStatus.Text = "⚠️ Cannot test voice - Voice ID is empty";
                    return;
                }

                // Update the voice ID in case it had whitespace
                voice.VoiceId = voiceId;

                string testText = txtTestVoiceText?.Text ?? "Hello! This is a test of the voice.";
                
                try
                {
                    txtStatus.Text = $"🔊 Testing voice: {voice.Name}...";
                    await _elevenLabsService.TextToSpeechAsync(testText, voiceId);
                    txtStatus.Text = $"✔️ Voice test complete: {voice.Name}";
                }
                catch (Exception ex)
                {
                    // Provide helpful error messages
                    if (ex.Message.Contains("401") || ex.Message.Contains("403"))
                    {
                        txtStatus.Text = "❌ Authentication failed - Check your ElevenLabs API key";
                    }
                    else
                    {
                        txtStatus.Text = $"❌ Voice test failed: {ex.Message}";
                    }
                }
            }
        }

        private void VoicesDataGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            // Prevent changing the "Main" voice name
            if (e.Column.Header.ToString() == "Name" && e.Row.Item is VoiceConfig voice)
            {
                var editingElement = e.EditingElement as System.Windows.Controls.TextBox;
                if (editingElement != null)
                {
                    // Find the original voice in the collection
                    var originalVoice = _voices.FirstOrDefault(v => v == voice);
                    if (originalVoice != null && originalVoice.Name == "Main" && editingElement.Text != "Main")
                    {
                        // Cancel the edit and restore the original value
                        e.Cancel = true;
                        MessageBox.Show("The 'Main' voice name cannot be changed. This is the narrator/player voice.",
                                      "Cannot Rename Main Voice",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                        return;
                    }
                }
            }
            
            // Update configuration when user finishes editing a cell
            if (!_isInitializing && !e.Cancel)
            {
                // Use Dispatcher to ensure the edit is committed before updating
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateVoiceConfiguration();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void BtnStopVoice_Click(object sender, RoutedEventArgs e)
        {
            _elevenLabsService.StopPlayback();
            txtStatus.Text = "⏹️ Voice playback stopped";
            txtElevenLabsTtsStatus.Text = "⏹️ Playback stopped by user";
            txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
        }

        private void BtnResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset ALL settings to defaults including:\n\n" +
                "• API Keys (you'll need to re-enter them)\n" +
                "• Custom Voices (reset to Main, Sarah, Clyde)\n" +
                "• Volume and Speed settings\n" +
                "• Voice quality settings\n" +
                "• All other preferences\n\n" +
                "Are you sure you want to continue?",
                "Reset All Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Reset all settings to defaults
                    Properties.Settings.Default.Reset();
                    Properties.Settings.Default.Save();

                    // Reset voices to defaults
                    _voiceConfig = GetDefaultVoices();
                    _voices.Clear();
                    foreach (var voice in _voiceConfig.Voices)
                    {
                        _voices.Add(voice);
                    }

                    // Clear API keys from UI
                    txtApiKey.Password = string.Empty;
                    txtElevenLabsApiKey.Password = string.Empty;

                    // Reset sliders to defaults
                    volumeSlider.Value = 100;
                    speedSlider.Value = 100;
                    stabilitySlider.Value = 25;
                    similaritySlider.Value = 85;
                    styleSlider.Value = 65;
                    speakerBoostCheckbox.IsChecked = true;
                    skipOpenAICheckbox.IsChecked = false;

                    // Update services
                    UpdateVoiceConfiguration();

                    // Update status
                    txtApiStatus.Text = "⚠️ No API key set";
                    txtApiStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));
                    txtElevenLabsStatus.Text = "⚠️ No API key set";
                    txtElevenLabsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 204, 0));

                    MessageBox.Show(
                        "All settings have been reset to defaults!\n\n" +
                        "Please re-enter your API keys to continue using the app.",
                        "Settings Reset Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error resetting settings: {ex.Message}",
                        "Reset Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void DebugModeCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (debugModeCheckbox != null && debugPanel != null)
            {
                // Show or hide debug panel based on checkbox state
                debugPanel.Visibility = debugModeCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}