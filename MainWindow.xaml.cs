using System;
using System.IO;
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

            // Load saved settings
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

                // Load Daniel voice ID setting
                string savedDanielVoiceId = Properties.Settings.Default.DanielVoiceId;
                if (!string.IsNullOrWhiteSpace(savedDanielVoiceId))
                {
                    _danielVoiceId = savedDanielVoiceId;
                    txtDanielVoiceId.Text = _danielVoiceId;
                    DialogueParser.SetDanielVoiceId(_danielVoiceId);
                }
                else
                {
                    txtDanielVoiceId.Text = _danielVoiceId;
                }
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
                Properties.Settings.Default.DanielVoiceId = _danielVoiceId;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"⚠️ Error saving settings: {ex.Message}";
            }
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

        private void TxtDanielVoiceId_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (txtDanielVoiceId != null && !_isInitializing)
            {
                _danielVoiceId = txtDanielVoiceId.Text.Trim();
                if (!string.IsNullOrWhiteSpace(_danielVoiceId))
                {
                    DialogueParser.SetDanielVoiceId(_danielVoiceId);
                    SaveSettings();
                }
            }
        }

        private void BtnStopVoice_Click(object sender, RoutedEventArgs e)
        {
            _elevenLabsService.StopPlayback();
            txtStatus.Text = "⏹️ Voice playback stopped";
            txtElevenLabsTtsStatus.Text = "⏹️ Playback stopped by user";
            txtElevenLabsTtsStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
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