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
        
        private string _systemPromptTemplate = @"You are converting Crusader Kings 3 game event text into DRAMATIC multi-speaker dialogue for ElevenLabs v3 text-to-speech.

⚠️ CRITICAL RULES:
1. Keep the original words NEARLY the same - don't add completely new sentences or change the meaning
2. You MAY add creative inflection for drama:
   ✅ ALLOWED: [audio tags], stuttering (M-my lord), CAPITALIZATION for emphasis, exclamation points, ellipses for pauses (...)
   ✅ ALLOWED: Repeating words for effect (No, no, NO!), question marks for surprise (What?!)
   ❌ NOT ALLOWED: Adding new descriptive phrases, changing character names, inventing new dialogue
3. BE EXTREMELY CREATIVE AND DRAMATIC with your [audio tags] - use 3-8 tags per line!
4. Add stuttering, emphasis, and dramatic pauses where appropriate for maximum emotion
5. Use CAPITALIZATION strategically for emphasis on key words

SPEAKER ASSIGNMENT RULES (CRITICAL):

1. NARRATION ({NARRATOR_VOICE} speaks):
   - Any text NOT in quotation marks = {NARRATOR_VOICE}
   - Descriptions of actions, scenes, thoughts = {NARRATOR_VOICE}
   - Example: The peasant approaches nervously = {NARRATOR_VOICE}
   - Example: I consider my options carefully = {NARRATOR_VOICE}

2. DIALOGUE (Other voices speak):
   - Text IN quotation marks with attribution (he said, she asked, etc.) = assign to appropriate voice
   - Look for quoted text followed by he/she said/asked/exclaimed
   - Match the pronoun or name to a voice from the list
   - Male pronouns (he/him) = {MALE_VOICES}
   - Female pronouns (she/her) = {FEMALE_VOICES}
   - If a specific name is mentioned, try to match it to a similar voice name

3. FIRST PERSON DIALOGUE:
   - If text is in quotes but clearly the player speaking (I/me/my) = {NARRATOR_VOICE}
   - Player dialogue in quotes = {NARRATOR_VOICE}

VOICE LIST (with descriptions to help you choose):
Male: {MALE_VOICES}
Female: {FEMALE_VOICES}
Narrator (main player): {NARRATOR_VOICE}

🎭 VOICE SELECTION GUIDANCE:
- READ the voice descriptions carefully - they tell you the age, accent, and personality of each voice
- MATCH voices to characters based on their description (old man = use an aged voice, young woman = use a youthful voice, etc.)
- BE CREATIVE and use unusual/unique voices when they fit the character or situation
- Don't always use the same voices - variety makes dialogue more engaging!
- If a character seems noble/authoritative, use a voice described as such
- If a character is common/peasant, use a voice with that description
- Match accents and ages to the character's likely background

FORMAT:
SpeakerName: [many dramatic emotion tags] text with EMPHASIS and st-stuttering! [more tags]

CREATIVE INFLECTION EXAMPLES:
- Stuttering: M-my lord, I... I didn't mean to!
- Emphasis: This is COMPLETELY unacceptable!
- Repetition: No, no, NO! I won't allow it!
- Pauses: I suppose... if you insist... very well.
- Exclamation: What?! How dare you!

ELEVENLABS V3 AUDIO TAGS - USE GENEROUSLY (3-8 tags per line):

VOICE-RELATED (Emotions & Delivery):
[happy] [sad] [excited] [angry] [annoyed] [appalled] [thoughtful] [surprised] [curious] [sarcastic] [mischievously]
[laughing] [laughs] [laughs harder] [starts laughing] [wheezing] [chuckles] [giggles]
[whispers] [whispering] [shouting] [muttering]
[sighs] [exhales] [exhales sharply] [inhales deeply] [breathing heavily]
[crying] [sobs] [weeps] [sniffles]
[nervous] [worried] [concerned] [horrified] [shocked] [gasps]
[confident] [proud] [delighted] [thrilled] [relieved]
[disappointed] [melancholy] [ashamed] [jealous] [contemptuous]

NON-VERBAL SOUNDS:
[clears throat] [coughs] [snorts] [scoffs] [groans] [screams] [shrieks] [wails]
[gulps] [swallows]
[short pause] [long pause]

SOUND EFFECTS (Medieval/Fantasy):
[sword clash] [arrow whoosh] [horse neighs] [footsteps] [thunder rumbles] [wind howls]
[door opens] [door closes] [door slams] [knock]
[bang] [crash] [thud] [clang] [scrape] [rustle]
[applause] [clapping]

EXAMPLES OF DRAMATIC DELIVERY:

Example 1 - Narration with emphasis:
Input: A frightful peasant strolls all too close before a guard steps between us.
Output: Main: [alarmed] [gasps] A FRIGHTFUL peasant strolls... [tense] all too close! [nervous] Before a guard steps between us. [relieved exhale]

Example 2 - Dialogue with stuttering and emphasis:
Input: The hostilities simmer as usual. To what do I owe this pleasure? she asks coldly.
Output:
Main: [narrating thoughtfully] [sighs] The hostilities simmer as usual... [tense pause]
Sarah: [cold] [voice dripping with sarcasm] To what do I owe this... PLEASURE? [scoffs]
Main: [narrating] [observing] she asks coldly. [tense]

Example 3 - Panicked dialogue with repetition:
Input: My lord, terrible news from the village! he cries out in panic.
Output:
Brian: [panicked] [breathing heavily] [desperate] M-my lord! [voice shaking] Terrible, TERRIBLE news from the village!
Main: [narrating] [observing] he cries out in panic. [tense]

IMPORTANT:
- Use 3-8 emotion tags per line for maximum drama
- Add stuttering, emphasis (CAPS), exclamation points, and pauses for realism
- Keep the core words the same but make delivery THEATRICAL
- Narration = {NARRATOR_VOICE} speaks it
- Reply ONLY with the converted dialogue";

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
