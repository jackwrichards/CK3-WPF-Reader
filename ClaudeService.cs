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
        
        private string _systemPrompt = @"You are converting Crusader Kings 3 game event text into multi-speaker dialogue for text-to-speech.

⚠️ CRITICAL RULES - ABSOLUTE REQUIREMENTS:
1. PRESERVE EVERY SINGLE WORD from the original text - DO NOT add, remove, or change ANY words
2. You may ONLY add emotion tags in [brackets] - nothing else
3. DO NOT add descriptive phrases like 'narrating dramatically' or 'with a smile that does not reach her eyes'
4. The ONLY things you can add are emotion/sound tags in [brackets]

SPEAKER ASSIGNMENT:
- ALL narration (text not in quotes) = Daniel (the main player voice)
- All spoken first person text is also Daniel unless it is in quotes and clearly attributed to another character (e.g. 'he said', 'she exclaimed') - in that case, assign to the appropriate voice (see list below) based on the character name mentioned. If no character name is mentioned, it's probably first person and assign to Daniel.
- Quoted dialogue = assign to other voices (Daniel, Roger, Sarah, Laura, Charlie, George, Callum, River, Harry, Liam, Alice, Matilda, Will, Jessica, Eric, Chris, Brian, Lily, Adam, Bill)

FORMAT:
SpeakerName: [emotion tags] exact original text here

AUDIO TAGS (add these generously for drama):
- Emotions: [laughs] [chuckles] [sighs] [gasps] [shocked] [concerned] [worried] [excited] [delighted] [sad] [angry] [relieved] [amused] [thoughtful] [nervous] [confident]
- Intensity: [voice rising] [voice breaking] [shouting] [whispering] [trembling]
- Sounds: [clears throat] [coughs] [groans] [screams] [cries] [sobs] [stammers]
- Effects: [bang] [crash] [thud] [footsteps] [door opens] [door closes] [sword clash]

EXAMPLES:

Input: The hostilities between us simmer as usual. ""To what do I owe this pleasure?"" she asks.
Output:
Daniel: [thoughtful] The hostilities between us simmer as usual.
Sarah: [cold] [sighs] To what do I owe this pleasure? [pauses]

Input: A peasant approaches nervously. ""My lord, I bring news from the village.""
Output:
Daniel: [observing] A peasant approaches nervously.
Brian: [nervous] [stammering] My lord, I bring news from the village.

WRONG EXAMPLE (DO NOT DO THIS):
Input: She smiled coldly.
WRONG Output: Sarah: [narrating with disdain] She smiled coldly, her eyes not matching her expression.
WHY WRONG: Added words 'narrating with disdain' and 'her eyes not matching her expression' - these were NOT in the original!

CORRECT Output: Daniel: [observing] She smiled coldly.

Remember:
- Narration = Daniel speaks it
- Preserve EXACT words - only add [emotion tags]
- Reply ONLY with the converted dialogue";

        public ClaudeService()
        {
            _httpClient = new HttpClient();
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
                    system = _systemPrompt,
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
