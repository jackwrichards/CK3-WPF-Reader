using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CK3_Reader
{
    /// <summary>
    /// Parses OpenAI dialogue responses into ElevenLabs-compatible format
    /// </summary>
    public class DialogueParser
    {
        // Dynamic mapping of voice names to ElevenLabs voice IDs
        private static Dictionary<string, string> VoiceIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Updates the voice mapping from a VoiceConfigCollection
        /// </summary>
        public static void UpdateVoiceMapping(VoiceConfigCollection voiceConfig)
        {
            VoiceIdMap.Clear();
            
            foreach (var voice in voiceConfig.Voices)
            {
                if (!string.IsNullOrWhiteSpace(voice.Name) && !string.IsNullOrWhiteSpace(voice.VoiceId))
                {
                    VoiceIdMap[voice.Name] = voice.VoiceId;
                }
            }
        }

        /// <summary>
        /// Gets the current voice mapping
        /// </summary>
        public static Dictionary<string, string> GetVoiceMapping()
        {
            return new Dictionary<string, string>(VoiceIdMap, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Represents a single dialogue entry
        /// </summary>
        public class DialogueEntry
        {
            public string Text { get; set; } = string.Empty;
            public string VoiceId { get; set; } = string.Empty;
            public string VoiceName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Represents the complete dialogue structure for ElevenLabs
        /// </summary>
        public class ElevenLabsDialogue
        {
            public List<DialogueInput> Inputs { get; set; } = new List<DialogueInput>();
            public string ModelId { get; set; } = "eleven_v3";
            public DialogueSettings? Settings { get; set; }
        }

        public class DialogueInput
        {
            public string Text { get; set; } = string.Empty;
            public string VoiceId { get; set; } = string.Empty;
        }

        public class DialogueSettings
        {
            public double Stability { get; set; } = 0.5;
            public double SimilarityBoost { get; set; } = 0.75;
            public double Style { get; set; } = 0.0;
            public bool UseSpeakerBoost { get; set; } = true;
        }

        /// <summary>
        /// Parses OpenAI response into structured dialogue entries
        /// Format: "Name: dialogue text"
        /// </summary>
        public static List<DialogueEntry> ParseOpenAIResponse(string openAiResponse)
        {
            var entries = new List<DialogueEntry>();
            
            if (string.IsNullOrWhiteSpace(openAiResponse))
            {
                return entries;
            }

            // Split by lines
            var lines = openAiResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            System.Diagnostics.Debug.WriteLine($"[DialogueParser] Parsing {lines.Length} lines");
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // Match pattern: "Name: dialogue text"
                // The name is everything before the first colon
                var colonIndex = trimmedLine.IndexOf(':');
                
                if (colonIndex > 0 && colonIndex < trimmedLine.Length - 1)
                {
                    var speakerName = trimmedLine.Substring(0, colonIndex).Trim();
                    var dialogueText = trimmedLine.Substring(colonIndex + 1).Trim();
                    
                    // Remove markdown formatting from speaker name (**, *, _, etc.)
                    speakerName = speakerName.Replace("**", "").Replace("*", "").Replace("_", "").Trim();
                    
                    System.Diagnostics.Debug.WriteLine($"[DialogueParser] Found speaker: '{speakerName}' with text: '{dialogueText.Substring(0, Math.Min(50, dialogueText.Length))}...'");
                    
                    // Skip if no dialogue text
                    if (string.IsNullOrWhiteSpace(dialogueText))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DialogueParser] Skipping - empty dialogue text");
                        continue;
                    }

                    // Get voice ID for the speaker
                    var voiceId = GetVoiceId(speakerName);
                    
                    System.Diagnostics.Debug.WriteLine($"[DialogueParser] Mapped '{speakerName}' to voice ID: {voiceId}");
                    
                    entries.Add(new DialogueEntry
                    {
                        Text = dialogueText,
                        VoiceId = voiceId,
                        VoiceName = speakerName
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DialogueParser] Skipping line (no colon or invalid format): '{trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}...'");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[DialogueParser] Total entries parsed: {entries.Count}");
            return entries;
        }

        /// <summary>
        /// Converts parsed dialogue entries into ElevenLabs API format
        /// </summary>
        public static ElevenLabsDialogue ConvertToElevenLabsFormat(
            List<DialogueEntry> entries,
            double stability = 0.25,
            double similarityBoost = 0.85,
            double style = 0.65,
            bool useSpeakerBoost = true)
        {
            var dialogue = new ElevenLabsDialogue
            {
                ModelId = "eleven_v3",
                Settings = new DialogueSettings
                {
                    Stability = stability,
                    SimilarityBoost = similarityBoost,
                    Style = style,
                    UseSpeakerBoost = useSpeakerBoost
                }
            };

            foreach (var entry in entries)
            {
                // Skip entries with empty or whitespace-only text
                if (string.IsNullOrWhiteSpace(entry.Text))
                {
                    System.Diagnostics.Debug.WriteLine($"[DialogueParser] Skipping entry with empty text for voice: {entry.VoiceName}");
                    continue;
                }
                
                dialogue.Inputs.Add(new DialogueInput
                {
                    Text = entry.Text.Trim(),
                    VoiceId = entry.VoiceId
                });
            }

            return dialogue;
        }

        /// <summary>
        /// Gets the ElevenLabs voice ID for a given speaker name
        /// </summary>
        public static string GetVoiceId(string speakerName)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
            {
                // Default to Main (narrator) or first available voice if no name provided
                if (VoiceIdMap.TryGetValue("Main", out var mainVoice))
                {
                    return mainVoice;
                }
                // Fallback to first available voice
                return VoiceIdMap.Values.FirstOrDefault() ?? "pNInz6obpgDQGcFmaJgB";
            }

            // Try exact match first
            if (VoiceIdMap.TryGetValue(speakerName, out var voiceId))
            {
                return voiceId;
            }

            // If not found, default to Main (narrator) or first available voice
            if (VoiceIdMap.TryGetValue("Main", out var fallbackVoice))
            {
                return fallbackVoice;
            }
            // Fallback to first available voice
            return VoiceIdMap.Values.FirstOrDefault() ?? "pNInz6obpgDQGcFmaJgB";
        }

        /// <summary>
        /// Parses OpenAI response and converts directly to ElevenLabs format
        /// </summary>
        public static ElevenLabsDialogue ParseAndConvert(
            string openAiResponse,
            double stability = 0.5,
            double similarityBoost = 0.75,
            double style = 0.0,
            bool useSpeakerBoost = true)
        {
            var entries = ParseOpenAIResponse(openAiResponse);
            return ConvertToElevenLabsFormat(entries, stability, similarityBoost, style, useSpeakerBoost);
        }

        /// <summary>
        /// Gets all available voice names
        /// </summary>
        public static List<string> GetAvailableVoiceNames()
        {
            return new List<string>(VoiceIdMap.Keys);
        }

        /// <summary>
        /// Validates if a voice name exists in the mapping
        /// </summary>
        public static bool IsValidVoiceName(string voiceName)
        {
            return VoiceIdMap.ContainsKey(voiceName);
        }
    }
}