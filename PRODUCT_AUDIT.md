# CK3 Voice Mod - Product Audit

## Hardcoded Values Found

### API URLs
- ✅ Claude API: `https://api.anthropic.com/v1/messages` (ClaudeService.cs:13)
- ✅ ElevenLabs API: `https://api.elevenlabs.io` (ElevenLabsService.cs:18)

### Model Names
- ⚠️ Claude Model: `claude-sonnet-4-20250514` (ClaudeService.cs:187) - Should be configurable
- ⚠️ ElevenLabs Models: `eleven_v3`, `eleven_flash_v2_5` - Should be configurable

### File Paths
- ⚠️ CK3 Log Path: `%USERPROFILE%\Documents\Paradox Interactive\Crusader Kings III\logs\debug.log`
  - Hardcoded in MainWindow.xaml.cs (lines 100-102, 242-244)
  - Should allow custom path for non-standard installations

### Voice IDs (DialogueParser.cs)
- ✅ 20 predefined voices with IDs - Good for defaults
- ⚠️ Daniel voice ID configurable but others are not

### Default Settings
- Volume: 100% (good)
- Playback Speed: 1.0x (good)
- Stability: 0.5 (good)
- Similarity Boost: 0.75 (good)
- Style: 0.0 (good)
- Speaker Boost: true (good)

### UI Text
- ⚠️ Many status messages hardcoded
- ⚠️ Error messages could be more user-friendly

## Current UI Issues

### Too Technical for End Users
1. **Raw CK3 Log Content** - Most users don't need to see this
2. **Cleaned Text** - Debug info
3. **Parsed Dialogue** - Debug info
4. **System Prompt Editor** - Advanced feature, should be hidden by default
5. **Voice Quality Settings** - Too many sliders for beginners

### Missing Features
1. No "Getting Started" guide
2. No validation for API keys
3. No clear indication of what's required vs optional
4. No preset configurations (Simple/Advanced modes)

### Confusing Elements
1. "Skip Claude" checkbox - confusing name
2. "Daniel Voice ID" - unclear what this does
3. Multiple status boxes showing similar info

## Recommended Changes

### Priority 1: Essential for Release
1. ✅ Add Debug Mode toggle
2. ✅ Hide technical sections by default (Raw Log, Cleaned Text, Parsed Dialogue)
3. ✅ Simplify main view to show only:
   - Status
   - Current event being read
   - Simple settings (Volume, Speed)
4. ✅ Add "Advanced Settings" expander
5. ✅ Better labels and tooltips
6. ✅ Add Getting Started section

### Priority 2: Nice to Have
1. Preset voice quality profiles (Balanced, High Quality, Fast)
2. Custom CK3 log path selector
3. Model selection dropdown
4. Voice preview/test button
5. Save/Load configuration profiles

### Priority 3: Future Enhancements
1. Auto-detect CK3 installation
2. Voice customization UI
3. Event filtering options
4. Audio output device selection
5. Hotkey support

## Proposed New UI Layout

```
┌─────────────────────────────────────────────────────────┐
│ CK3 Voice Reader                                        │
├─────────────────────────────────────────────────────────┤
│ GETTING STARTED                                         │
│ 1. Enter your Claude API key                           │
│ 2. Enter your ElevenLabs API key                       │
│ 3. Launch CK3 and trigger an event!                    │
├─────────────────────────────────────────────────────────┤
│ QUICK SETTINGS                                          │
│ ├─ Claude API Key: [**********]                        │
│ ├─ ElevenLabs API Key: [**********]                    │
│ ├─ Volume: [====|----] 100%                            │
│ └─ Speed: [====|----] 1.0x                             │
├─────────────────────────────────────────────────────────┤
│ STATUS                                                  │
│ ✔️ Ready - Monitoring CK3 debug.log                    │
├─────────────────────────────────────────────────────────┤
│ CURRENT EVENT                                           │
│ [Event text being read...]                             │
├─────────────────────────────────────────────────────────┤
│ ▼ Advanced Settings                                    │
│   ├─ Voice Quality                                     │
│   ├─ System Prompt                                     │
│   ├─ Voice Customization                               │
│   └─ Processing Mode                                   │
├─────────────────────────────────────────────────────────┤
│ ☑ Debug Mode (Show technical details)                  │
└─────────────────────────────────────────────────────────┘
```

## Settings That Should Be Configurable

### Currently Hardcoded, Should Be Settings:
1. ⚠️ Claude Model (currently: claude-sonnet-4-20250514)
2. ⚠️ CK3 Log File Path
3. ⚠️ Temp file location for audio
4. ⚠️ Max event text length (currently: 10000 chars)
5. ⚠️ File polling interval (currently: 20ms)

### Currently Settings, Good:
1. ✅ API Keys
2. ✅ System Prompt
3. ✅ Volume
4. ✅ Playback Speed
5. ✅ Voice Quality Settings
6. ✅ Daniel Voice ID
7. ✅ Skip Claude mode

## Action Items

1. Create Debug Mode toggle
2. Reorganize UI into Simple/Advanced sections
3. Add collapsible expanders for advanced features
4. Improve status messages
5. Add tooltips everywhere
6. Create Getting Started guide
7. Add API key validation
8. Better error messages
