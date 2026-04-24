using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK3_Reader
{
    public class ClaudeService
    {
        private string? _apiKey;
        private readonly HttpClient _httpClient;
        private const string API_URL = "https://api.anthropic.com/v1/messages";
        private const string API_VERSION = "2023-06-01";
        
        private string _systemPromptTemplate = @"You are formatting Crusader Kings 3 game events for text-to-speech.

CRITICAL: You MUST use ONLY these exact speaker names (case-sensitive):
- Narration: {NARRATOR_VOICE}
- Male characters: {MALE_VOICES}
- Female characters: {FEMALE_VOICES}

DO NOT make up new names. DO NOT use character names from the game. ONLY use the names listed above.

SPEAKER RULES:
- Narration (descriptions, actions, thoughts) → {NARRATOR_VOICE}
- Quoted dialogue with male attribution (he said/asked) → Pick ONE from: {MALE_VOICES}
- Quoted dialogue with female attribution (she said/asked) → Pick ONE from: {FEMALE_VOICES}
- Player's own dialogue in quotes → {NARRATOR_VOICE}

FORMAT:
SpeakerName: [optional tags] The dialogue text here.

TEXT FORMATTING:
- Keep the original meaning and action words
- Make it natural and expressive when spoken
- Use CAPS for emphasis, ellipses (...) for pauses
- Add punctuation (! ?) to convey emotion

AUDIO TAGS:
- If ONLY {NARRATOR_VOICE} speaks (single speaker): NO TAGS - just clean text
- If MULTIPLE speakers: Use 0-1 tags per line when it adds value

Available tags (multi-speaker only): [happy] [sad] [angry] [nervous] [excited] [worried] [surprised] [whispers] [shouting] [laughs] [sighs] [gasps] [sarcastic] [cold]

EXAMPLES:

Single speaker (NO tags, just expressive text):
Input: I consider my options carefully. This could change everything.
Output:
Main: I consider my options carefully. This could change... everything.

Input: A frightful peasant approaches nervously before a guard steps between us.
Output:
Main: A frightful peasant approaches nervously... before a guard steps between us.

Multiple speakers (minimal tags):
Input: To what do I owe this pleasure? she asks coldly. I remain silent.
Output:
Main: I remain silent.
Sarah: [cold] To what do I owe this pleasure?

Input: My lord, terrible news! he cries out.
Output:
Clyde: [panicked] My lord, terrible news!

Reply ONLY with the formatted dialogue.";

        private string _currentSystemPrompt = string.Empty;

        public ClaudeService()
        {
            _httpClient = new HttpClient();
            // Initialize with default prompt (will be updated when voices are set)
            _currentSystemPrompt = _systemPromptTemplate;
        }

        /// <summary>
        /// Updates the system prompt with the current voice configuration
        /// </summary>
        public void SetVoiceConfiguration(VoiceConfigCollection voiceConfig)
        {
            var (males, females) = voiceConfig.GetVoiceNamesByGender();
            
            // Find the narrator voice (prefer "Main" if it exists, otherwise use first male voice)
            string narratorVoice = "Main";
            if (males.Contains("Main"))
            {
                narratorVoice = "Main";
            }
            else if (males.Count > 0)
            {
                narratorVoice = males[0];
            }
            
            string maleVoicesList = string.Join(", ", males);
            string femaleVoicesList = string.Join(", ", females);
            
            // Replace placeholders in the template
            _currentSystemPrompt = _systemPromptTemplate
                .Replace("{MALE_VOICES}", maleVoicesList)
                .Replace("{FEMALE_VOICES}", femaleVoicesList)
                .Replace("{NARRATOR_VOICE}", narratorVoice);
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey?.Trim();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Remove("x-api-key");
                _httpClient.DefaultRequestHeaders.Remove("anthropic-version");
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", API_VERSION);
            }
        }

        public bool HasApiKey()
        {
            return !string.IsNullOrWhiteSpace(_apiKey);
        }

        public async Task<string> ProcessEventTextAsync(string eventText)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("Claude API key is not set");
            }

            if (string.IsNullOrWhiteSpace(eventText))
            {
                return "No event text to process";
            }

            try
            {
                var requestBody = new
                {
                    model = "claude-sonnet-4-20250514",
                    max_tokens = 4096,
                    temperature = 1.0,
                    system = _currentSystemPrompt,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = eventText
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(API_URL, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Claude API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (jsonResponse.TryGetProperty("content", out var contentArray) && 
                    contentArray.GetArrayLength() > 0)
                {
                    var firstContent = contentArray[0];
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? "No response from Claude";
                    }
                }

                return "No response from Claude";
            }
            catch (Exception ex)
            {
                return $"Error processing with Claude: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
