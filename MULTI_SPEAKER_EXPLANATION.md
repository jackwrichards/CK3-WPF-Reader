# Multi-Speaker Voice System Explanation

## How It Works

### 1. Claude Processes the Event
Claude receives the raw CK3 event text and is instructed to format it like this:
```
Eric: [appalled] Are you serious? I can't believe you did that!
Brian: [laughing] That's amazing!
Sarah: [sighs] I guess you're right.
```

**Key Format Requirements:**
- Each line must start with a voice name from the list
- Followed by a colon `:`
- Then the dialogue text

### 2. DialogueParser Parses the Response
The `ParseOpenAIResponse()` method (line 89-137 in DialogueParser.cs):
- Splits Claude's response by lines
- For each line, finds the first colon `:`
- Everything BEFORE the colon = speaker name (e.g., "Eric")
- Everything AFTER the colon = dialogue text
- Looks up the speaker name in the VoiceIdMap to get the ElevenLabs voice ID

**Example:**
```
Input:  "Eric: [appalled] Are you serious?"
Parse:  speakerName = "Eric"
        dialogueText = "[appalled] Are you serious?"
Lookup: voiceId = "cjVigY5qzO86Huf0OWal" (Eric's voice ID)
```

### 3. ElevenLabs Receives the Data
The parsed data is sent to ElevenLabs Text-to-Dialogue API as:
```json
{
  "inputs": [
    {
      "text": "[appalled] Are you serious?",
      "voice_id": "cjVigY5qzO86Huf0OWal"
    },
    {
      "text": "[laughing] That's amazing!",
      "voice_id": "nPczCjzI2devNBz1zQrb"
    }
  ],
  "model_id": "eleven_v3",
  "output_format": "mp3_44100_128"
}
```

ElevenLabs then generates a single audio file with multiple voices speaking in sequence.

## Common Issues & Solutions

### Issue 1: Voices Not Switching
**Cause:** Claude isn't formatting the output correctly with "Name: text" format

**Solution:** Check the "LLM TRANSLATION" box in the UI to see what Claude is returning. It should look like:
```
Eric: Some text here
Brian: More text here
```

NOT like:
```
Some text here (Eric speaking)
More text here (Brian speaking)
```

### Issue 2: All Same Voice
**Cause:** Parser can't find the colon `:` or the name doesn't match the voice list

**Solution:** 
- Ensure Claude uses EXACT names from the list: Roger, Sarah, Laura, Charlie, George, Callum, River, Harry, Liam, Alice, Matilda, Will, Jessica, Eric, Chris, Brian, Daniel, Lily, Adam, Bill
- If name not found, it defaults to "Adam"

### Issue 3: Lines Being Skipped
**Cause:** Lines without a colon or with empty text after the colon are skipped

**Solution:** Every dialogue line must have the format "Name: text"

## Available Voices

**Male:** Adam, Bill, Brian, Callum, Charlie, Chris, Daniel, Eric, George, Harry, Liam, Roger, Will

**Female:** Alice, Jessica, Laura, Lily, Matilda, River, Sarah

## Debugging Tips

1. **Check Claude's Output:** Look at the "LLM TRANSLATION" section in the app
2. **Verify Format:** Each line should be "VoiceName: dialogue text"
3. **Check Voice Names:** Must match exactly (case-insensitive)
4. **Audio Tags:** Tags like [laughing], [sighs] should be INSIDE the dialogue text, not before the name
