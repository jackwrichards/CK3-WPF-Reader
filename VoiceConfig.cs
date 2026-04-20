using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CK3_Reader
{
    /// <summary>
    /// Represents a custom voice configuration for ElevenLabs TTS
    /// </summary>
    public class VoiceConfig : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _voiceId = string.Empty;
        private string _gender = "Male";
        private string _description = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VoiceId
        {
            get => _voiceId;
            set
            {
                if (_voiceId != value)
                {
                    _voiceId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Gender
        {
            get => _gender;
            set
            {
                if (_gender != value)
                {
                    _gender = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public VoiceConfig()
        {
        }

        public VoiceConfig(string name, string voiceId, string gender, string description = "")
        {
            Name = name;
            VoiceId = voiceId;
            Gender = gender;
            Description = description;
        }

        public override string ToString()
        {
            return $"{Name} ({Gender}) - {Description}";
        }
    }

    /// <summary>
    /// Collection of voice configurations
    /// </summary>
    public class VoiceConfigCollection
    {
        public List<VoiceConfig> Voices { get; set; } = new List<VoiceConfig>();

        /// <summary>
        /// Gets default voice configuration
        /// </summary>
        public static VoiceConfigCollection GetDefault()
        {
            return new VoiceConfigCollection
            {
                Voices = new List<VoiceConfig>
                {
                    // Male voices
                    new VoiceConfig("Daniel", "yhf80q1381zd2JJQ4tM7", "Male"),
                    new VoiceConfig("Eric", "cjVigY5qzO86Huf0OWal", "Male"),
                    new VoiceConfig("Brian", "nPczCjzI2devNBz1zQrb", "Male"),
                    
                    // Female voices
                    new VoiceConfig("Sarah", "EXAVITQu4vr4xnSDxMaL", "Female"),
                    new VoiceConfig("Laura", "FGY2WhTYpPnrIDTdsKH5", "Female"),
                    new VoiceConfig("Alice", "Xb7hH8MSUJpSbSDYk0k2", "Female")
                }
            };
        }

        /// <summary>
        /// Validates that there is at least 1 male and 1 female voice
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            int maleCount = 0;
            int femaleCount = 0;

            foreach (var voice in Voices)
            {
                if (string.IsNullOrWhiteSpace(voice.Name) || string.IsNullOrWhiteSpace(voice.VoiceId))
                {
                    errorMessage = "All voices must have a name and voice ID";
                    return false;
                }

                if (voice.Gender == "Male") maleCount++;
                else if (voice.Gender == "Female") femaleCount++;
            }

            if (maleCount < 1)
            {
                errorMessage = "You must have at least 1 male voice";
                return false;
            }

            if (femaleCount < 1)
            {
                errorMessage = "You must have at least 1 female voice";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Gets a comma-separated list of voice names for the AI prompt
        /// </summary>
        public string GetVoiceNamesList()
        {
            var names = new List<string>();
            foreach (var voice in Voices)
            {
                if (!string.IsNullOrWhiteSpace(voice.Name))
                {
                    names.Add(voice.Name);
                }
            }
            return string.Join(", ", names);
        }

        /// <summary>
        /// Gets lists of male and female voice names with descriptions
        /// </summary>
        public (List<string> males, List<string> females) GetVoiceNamesByGender()
        {
            var males = new List<string>();
            var females = new List<string>();

            foreach (var voice in Voices)
            {
                if (!string.IsNullOrWhiteSpace(voice.Name) && !string.IsNullOrWhiteSpace(voice.VoiceId))
                {
                    string voiceInfo = voice.Name;
                    if (!string.IsNullOrWhiteSpace(voice.Description))
                    {
                        voiceInfo += $" ({voice.Description})";
                    }
                    
                    if (voice.Gender == "Male")
                        males.Add(voiceInfo);
                    else if (voice.Gender == "Female")
                        females.Add(voiceInfo);
                }
            }

            return (males, females);
        }
    }
}
