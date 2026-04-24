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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CK3_Reader
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ClaudeService _claudeService;
        private readonly ElevenLabsService _elevenLabsService;
        
        // Flag to prevent saving during initialization
        private bool _isInitializing = true;
        
        // Loading indicator animation
        private DispatcherTimer? _loadingTimer;
        private int _loadingDots = 0;
        
        // Voice quality settings (kept for internal use)
        private double _stability = 0.5;
        private double _similarityBoost = 0.75;
        private double _style = 0.0;
        private bool _useSpeakerBoost = true;
        
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
            
            // Hook up playback progress callback
            _elevenLabsService.OnPlaybackProgress = OnAudioPlaybackProgress;
            
            // Initialize loading indicator timer
            _loadingTimer = new DispatcherTimer();
            _loadingTimer.Interval = TimeSpan.FromMilliseconds(500);
            _loadingTimer.Tick += LoadingTimer_Tick;
            
            // Initialize voice configuration with defaults
            _voiceConfig = GetDefaultVoices();
            DialogueParser.UpdateVoiceMapping(_voiceConfig);
            _claudeService.SetVoiceConfiguration(_voiceConfig);

            // Load saved settings (including voices)
            LoadSettings();

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logPath = documents + "\\Paradox Interactive\\Crusader Kings III\\logs\\";
            string debugLog = logPath + "debug.log";

            if (File.Exists(debugLog))
            {
                SetStatus("Ready", false);
            }
            else
            {
                SetStatus("Error - CK3 debug.log not found", false);
            }

            // Mark initialization as complete
            _isInitializing = false;

            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Callback for audio playback progress updates
        /// </summary>
        private void OnAudioPlaybackProgress(double currentSeconds, double totalSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatus("Playing audio", true);
            });
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
                    txtApiStatus.Text = "✔️ API key set";
                    txtApiStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                }

                // Load ElevenLabs API key
                string savedElevenLabsKey = Properties.Settings.Default.ElevenLabsApiKey;
                if (!string.IsNullOrWhiteSpace(savedElevenLabsKey))
                {
                    txtElevenLabsApiKey.Password = savedElevenLabsKey;
                    _elevenLabsService.SetApiKey(savedElevenLabsKey);
                    txtElevenLabsStatus.Text = "✔️ API key set";
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
            }
            catch (Exception ex)
            {
                SetStatus($"Error - {ex.Message}", false);
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
                // Only save user-configurable settings
                Properties.Settings.Default.OpenAIApiKey = txtApiKey.Password;
                Properties.Settings.Default.ElevenLabsApiKey = txtElevenLabsApiKey.Password;
                Properties.Settings.Default.Volume = volumeSlider.Value;
                Properties.Settings.Default.PlaybackSpeed = speedSlider.Value;
                
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                SetStatus($"Error - {ex.Message}", false);
            }
        }

        private VoiceConfigCollection GetDefaultVoices()
        {
            return new VoiceConfigCollection
            {
                Voices = new System.Collections.Generic.List<VoiceConfig>
                {
                    // Main narrator voice
                    new VoiceConfig { Name = "Main", VoiceId = "goT3UYdM9bhm0n2lmKQx", Gender = "Male", Description = "Narrator - bold, authoritative male" },
                    
                    // Female voices
                    new VoiceConfig { Name = "Seraphina", VoiceId = "4tRn1lSkEn13EVTuqb0g", Gender = "Female", Description = "Seductive, alluring woman" },
                    new VoiceConfig { Name = "Elara", VoiceId = "WtA85syCrJwasGeHGH2p", Gender = "Female", Description = "Happy, cheerful woman" },
                    new VoiceConfig { Name = "Isolde", VoiceId = "nDJIICjR9zfJExIFeSCN", Gender = "Female", Description = "Normal, neutral woman" },
                    new VoiceConfig { Name = "Lyra", VoiceId = "pPdl9cQBQq4p6mRkZy2Z", Gender = "Female", Description = "Young girl, child" },
                    new VoiceConfig { Name = "Morgana", VoiceId = "si0svtk05vPEuvwAW93c", Gender = "Female", Description = "Angry, stern woman" },
                    new VoiceConfig { Name = "Eldara", VoiceId = "USEQXnsXRJlw2k9LUzG4", Gender = "Female", Description = "Wise old woman, sage" },
                    new VoiceConfig { Name = "Quirina", VoiceId = "eppqEXVumQ3CfdndcIBd", Gender = "Female", Description = "Odd, eccentric woman" },
                    new VoiceConfig { Name = "Ravenna", VoiceId = "flHkNRp1BlvT73UL6gyz", Gender = "Female", Description = "Villainous woman, scheming" },
                    
                    // Male voices
                    new VoiceConfig { Name = "Theron", VoiceId = "IRHApOXLvnW57QJPQH2P", Gender = "Male", Description = "Deep, commanding male" },
                    new VoiceConfig { Name = "Malachar", VoiceId = "2gPFXx8pN3Avh27Dw5Ma", Gender = "Male", Description = "Evil, quiet menace" },
                    new VoiceConfig { Name = "Gorath", VoiceId = "6sFKzaJr574YWVu4UuJF", Gender = "Male", Description = "Deep, bold warrior" },
                    new VoiceConfig { Name = "Draven", VoiceId = "cPoqAvGWCPfCfyPMwe4z", Gender = "Male", Description = "Evil, deep villain" },
                    new VoiceConfig { Name = "Pip", VoiceId = "zYcjlYFOd3taleS0gkk3", Gender = "Male", Description = "Silly, animated jester" },
                    new VoiceConfig { Name = "Edmund", VoiceId = "2ajXGJNYBR0iNHpS4VZb", Gender = "Male", Description = "Normal, everyday man" },
                    new VoiceConfig { Name = "Borin", VoiceId = "DGzg6RaUqxGRTHSBjfgF", Gender = "Male", Description = "Loud, yelling warrior" },
                    new VoiceConfig { Name = "Aldric", VoiceId = "fbIG6gEosVIM95R5qOna", Gender = "Male", Description = "Elderly man, aged" },
                    new VoiceConfig { Name = "Percival", VoiceId = "7cOBG34AiHrAzs842Rdi", Gender = "Male", Description = "Posh, refined nobleman" },
                    new VoiceConfig { Name = "Mortis", VoiceId = "wXvR48IpOq9HACltTmt7", Gender = "Male", Description = "Ancient, sinister elder" },
                    new VoiceConfig { Name = "Whisper", VoiceId = "3SF4rB1fGBMXU9xRM7pz", Gender = "Male", Description = "Whispering evil presence" },
                    new VoiceConfig { Name = "Vex", VoiceId = "xYWUvKNK6zWCgsdAK7Wi", Gender = "Male", Description = "Evil, malevolent" },
                    new VoiceConfig { Name = "Gribble", VoiceId = "Z7RrOqZFTyLpIlzCgfsp", Gender = "Male", Description = "Goblin-like, raspy" },
                    new VoiceConfig { Name = "Grumwald", VoiceId = "MKlLqCItoCkvdhrxgtLv", Gender = "Male", Description = "Cranky old man" },
                    new VoiceConfig { Name = "Barnaby", VoiceId = "BBfN7Spa3cqLPH1xAS22", Gender = "Male", Description = "Kindly old man" },
                    new VoiceConfig { Name = "Rollo", VoiceId = "LG95yZDEHg6fCZdQjLqj", Gender = "Male", Description = "Loud, boisterous" },
                    new VoiceConfig { Name = "Maleficus", VoiceId = "bwCXcoVxWNYMlC6Esa8u", Gender = "Male", Description = "Deeply evil ancient" },
                    new VoiceConfig { Name = "Jester", VoiceId = "dHd5gvgSOzSfduK4CvEg", Gender = "Male", Description = "Silly, playful fool" },
                    new VoiceConfig { Name = "Oddwin", VoiceId = "yjJ45q8TVCrtMhEKurxY", Gender = "Male", Description = "Odd, peculiar man" },
                    new VoiceConfig { Name = "Chill", VoiceId = "Xb3zeLrTi6F4ziIcXdwk", Gender = "Male", Description = "Relaxed, laid-back" },
                    new VoiceConfig { Name = "Scarface", VoiceId = "9yzdeviXkFddZ4Oz8Mok", Gender = "Male", Description = "Battle-scarred veteran" },
                    new VoiceConfig { Name = "Nibbles", VoiceId = "ouL9IsyrSnUkCmfnD02u", Gender = "Male", Description = "Gnome-like, small" }
                }
            };
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
            int eventMarkersFound = 0;

            try
            {
                // Initial file info
                FileInfo fileInfo = new FileInfo(debugLog);
                lastFileSize = fileInfo.Length;
                lastModified = fileInfo.LastWriteTime;
                
                Dispatcher.Invoke(() =>
                {
                    SetStatus("Waiting for CK3 events", true);
                });

                using (FileStream stream = new FileStream(debugLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    StreamReader reader = new StreamReader(stream);
                    long startPosition = stream.Length;
                    stream.Seek(0, SeekOrigin.End); // Start reading from the end of the file

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
                                
                                // Play custom notification sound
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        string soundPath = @"C:\Users\jack\Documents\CK3VoiceMod\CK3-WPF-Reader\subtle_notification,_#2-1776915984022.wav";
                                        
                                        if (File.Exists(soundPath))
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Sound] File found, playing: {soundPath}");
                                            var player = new System.Media.SoundPlayer(soundPath);
                                            player.Load(); // Load the file first
                                            player.PlaySync(); // Then play it synchronously
                                            System.Diagnostics.Debug.WriteLine($"[Sound] Playback completed");
                                        }
                                        else
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Sound] File NOT found at: {soundPath}");
                                            // Fallback beep to confirm code is running
                                            Console.Beep(400, 50);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Sound] Error: {ex.Message}");
                                        System.Diagnostics.Debug.WriteLine($"[Sound] Stack trace: {ex.StackTrace}");
                                    }
                                });
                                
                                Dispatcher.Invoke(() =>
                                {
                                    SetStatus("Event detected", true);
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
                                       
                                       // Automatically process with Claude if API key is set
                                       if (_claudeService.HasApiKey())
                                       {
                                           try
                                           {
                                               SetStatus("Processing with AI", true);
                                               
                                               string aiResponse = await _claudeService.ProcessEventTextAsync(eventText.Trim());
                                                
                                                // Automatically play with ElevenLabs if API key is set
                                                if (_elevenLabsService.HasApiKey())
                                                {
                                                    try
                                                    {
                                                        // Parse the dialogue to detect speaker count
                                                        var parsedEntries = DialogueParser.ParseOpenAIResponse(aiResponse);
                                                        
                                                        // Get unique speakers and their voice IDs
                                                        var uniqueSpeakers = parsedEntries.Select(e => e.VoiceName).Distinct().Count();
                                                        var speakerNames = string.Join(", ", parsedEntries.Select(e => e.VoiceName).Distinct());
                                                        var uniqueVoiceIds = parsedEntries.Select(e => e.VoiceId).Distinct().Count();
                                                        
                                                        // Debug: Log the voice mapping
                                                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Speaker → Voice ID mapping:");
                                                        foreach (var entry in parsedEntries.GroupBy(e => e.VoiceName).Select(g => g.First()))
                                                        {
                                                            System.Diagnostics.Debug.WriteLine($"  {entry.VoiceName} → {entry.VoiceId}");
                                                        }
                                                        
                                                        // Route based on speaker count
                                                        if (uniqueSpeakers <= 1)
                                                        {
                                                            // Single speaker - use Flash v2.5 for speed
                                                            SetStatus("Generating speech", true);
                                                            SetModeInfo("Using: Flash v2.5 (Single Speaker)");
                                                            
                                                            // Get the Main voice ID
                                                            string mainVoiceId = GetMainVoiceId();
                                                            
                                                            // Extract just the text (remove speaker names and tags for cleaner output)
                                                            string cleanText = string.Join(" ", parsedEntries.Select(e => e.Text));
                                                            
                                                            await _elevenLabsService.TextToSpeechFlashAsync(
                                                                cleanText,
                                                                mainVoiceId,
                                                                _stability,
                                                                _similarityBoost);
                                                        }
                                                        else
                                                        {
                                                            // Multiple speakers - use v3 for quality
                                                            SetStatus("Generating speech", true);
                                                            SetModeInfo($"Using: Eleven v3 ({uniqueSpeakers} Speakers: {speakerNames}) | {uniqueVoiceIds} unique voice IDs");
                                                            
                                                            await _elevenLabsService.TextToSpeechFromOpenAIAsync(
                                                                aiResponse,
                                                                _stability,
                                                                _similarityBoost,
                                                                _style,
                                                                _useSpeakerBoost);
                                                        }
                                                        
                                                        SetStatus("Ready", false);
                                                    }
                                                    catch (Exception ttsEx)
                                                    {
                                                        SetStatus($"Error - {ttsEx.Message}", false);
                                                    }
                                                }
                                                else
                                                {
                                                    SetStatus("Ready", false);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                SetStatus($"Error - {ex.Message}", false);
                                            }
                                        }
                                        else
                                        {
                                            SetStatus("Error - API key required", false);
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

                        // Update status counter
                        if (!startMessage && linesRead % 50 == 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                SetStatus("Waiting for CK3 events", true);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    SetStatus("Error - " + ex.Message, false);
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
                    txtApiStatus.Text = "✔️ API key set";
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
                    txtElevenLabsStatus.Text = "✔️ API key set";
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

        private void BtnStopVoice_Click(object sender, RoutedEventArgs e)
        {
            _elevenLabsService.StopPlayback();
            SetStatus("Ready", false);
        }
        
        /// <summary>
        /// Updates the mode info text
        /// </summary>
        private void SetModeInfo(string info)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetModeInfo(info), System.Windows.Threading.DispatcherPriority.Send);
                return;
            }
            
            if (!string.IsNullOrWhiteSpace(info))
            {
                txtModeInfo.Text = info;
                txtModeInfo.Visibility = Visibility.Visible;
            }
            else
            {
                txtModeInfo.Visibility = Visibility.Collapsed;
            }
            
            // Force UI update
            txtModeInfo.InvalidateVisual();
        }
        
        /// <summary>
        /// Timer tick event for animating the loading indicator
        /// </summary>
        private void LoadingTimer_Tick(object? sender, EventArgs e)
        {
            _loadingDots = (_loadingDots % 3) + 1;
            loadingSpinner.Text = new string('.', _loadingDots);
        }
        
        /// <summary>
        /// Updates the status text and shows/hides the loading indicator
        /// </summary>
        private void SetStatus(string message, bool showSpinner)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetStatus(message, showSpinner), System.Windows.Threading.DispatcherPriority.Send);
                return;
            }
            
            txtStatus.Text = "Status: " + message;
            
            if (showSpinner)
            {
                loadingSpinner.Visibility = Visibility.Visible;
                _loadingDots = 0;
                loadingSpinner.Text = ".";
                _loadingTimer?.Start();
            }
            else
            {
                _loadingTimer?.Stop();
                loadingSpinner.Visibility = Visibility.Collapsed;
                loadingSpinner.Text = "";
            }
            
            // Force UI update
            txtStatus.InvalidateVisual();
            loadingSpinner.InvalidateVisual();
        }
        
        /// <summary>
        /// Gets the voice ID for the Main narrator voice from the current voice configuration
        /// </summary>
        private string GetMainVoiceId()
        {
            // Find the Main voice in the configuration
            var mainVoice = _voiceConfig.Voices.FirstOrDefault(v => v.Name == "Main");
            if (mainVoice != null && !string.IsNullOrWhiteSpace(mainVoice.VoiceId))
            {
                return mainVoice.VoiceId.Trim();
            }
            
            // Fallback: use the first male voice if Main doesn't exist
            var firstMaleVoice = _voiceConfig.Voices.FirstOrDefault(v => v.Gender == "Male" && !string.IsNullOrWhiteSpace(v.VoiceId));
            if (firstMaleVoice != null)
            {
                return firstMaleVoice.VoiceId.Trim();
            }
            
            // Last resort: return default Daniel voice ID
            return "yhf80q1381zd2JJQ4tM7";
        }
    }
}