# Copy review: Home, Audio, Microphone and the equaliser

The words on the pages this round rebuilds, checked against the
[copy principles](COPY.md). The changes are proposed; phase 3 applies the
ones the owner keeps. Where a screen check looks for the old words, it is
updated in the same commit.

## The change that matters most

Merging Audio and Microphone puts five "noise" names on one page: Noise
control, Noise cancellation, Blocking, Background noise and AI noise
reduction. The headset's feature becomes **Noise cancellation**, with modes
**Off, Cancelling and Transparency** and a **Strength** slider. The
microphone's features become **Noise reduction** and **Noise gate**. Each
description says whose sound it changes.

## Proposed changes

### Home

| Key | Now | Proposed | Why |
| --- | --- | --- | --- |
| `Quick_NoiseControl` | Noise control | Noise cancellation | One name everywhere |
| `Quick_Preset` | Equaliser | Game equaliser | Two equalisers exist |
| `Mix_KeysLink` | Keyboard shortcuts | Keyboard shortcuts, as a link with an arrow | It reads as stray text today |
| `Home_BatterySettling` | Settling… | Reading… | Plainer: it is reading a new battery |
| `Mix_Playing` | {0}  ·  playing | {0} is playing | A whole phrase, no separator carrying meaning |

### Audio: what you hear

| Key | Now | Proposed | Why |
| --- | --- | --- | --- |
| `Audio_AroundYouHeading` | Around you | What you hear | The section now holds volume and spatial sound too |
| `Noise_Title` | Noise control | Noise cancellation | One name everywhere |
| `Noise_Description` | Blocks the room around you, or lets it in. | Blocks the room in your headset, or lets it in. | Says whose sound it is |
| `Noise_Cancelling` | Noise cancellation | Cancelling | One word for a segment |
| `Noise_Blocking` | Blocking | Strength | The effect, not the mechanism |
| `Noise_BlockingDescription` | How much of the room noise cancellation keeps out. | (removed) | The name now says it |
| `Audio_Type`, `Audio_Intensity` | Type, Intensity | Sound, Strength | Out of context they say nothing |
| `Shh_Legacy` | Legacy | Everything, described as "All quiet sounds, as on older headsets." | Only Swarm II users know "Legacy" |
| `Audio_VoicePrompts.Description` | How loud the headset's spoken alerts are. | (removed) | Repeats the name |
| `Audio_LoudnessBoost.Header` | Max loudness boost (Stealth Pro II only) | Loudness boost | The model goes in the state where it's unavailable |
| `Spatial_Unrecognised` | Something else | Another format | Says what it is |
| `Spatial_Refused` | Your system wouldn't switch the headset to {0}. | Your system didn't switch to {0}. Try again, or choose it in your system's sound settings. | A next step |

### Audio: your microphone

| Key | Now | Proposed | Why |
| --- | --- | --- | --- |
| `Microphone_VoiceHeading` | Your voice | Your microphone | The section's name on the merged page |
| `Microphone_LevelNote` | Speak to see it move. | (removed) | The meter shows it |
| `Microphone_Sensitivity.Description` | How loud you come through. | (removed) | Repeats the name |
| `Microphone_MicMonitoring.Header` | Mic monitoring | Microphone monitoring | Microphone everywhere |
| `Microphone_AiTile.Title` | AI noise reduction | Noise reduction | Short; keeps it apart from noise cancellation |
| `Microphone_AiTile.Description` | Removes background noise from your voice. | Removes background noise from what others hear. | Says whose sound it is |
| `Microphone_Threshold.Description` | How loud you have to be before the gate opens. | How loud you have to be to be heard. | The effect, not the mechanism |

### Equaliser

| Key | Now | Proposed | Why |
| --- | --- | --- | --- |
| Page and tabs | Equaliser on two pages | Equaliser page, tabs Game and Microphone | One place, scope in the tab |
| `Equaliser_PresetPicker` (name) | Equaliser preset | Game equaliser preset, Microphone equaliser preset | A screen reader hears the scope |
| Factory names in `Presets.cs` | English in code | Resource keys | Translatable; text lives in the resource file |
| `Bass & Treble Boost` | Bass & Treble Boost | Kept: the name the headset shows | The device's own name |
| `Equaliser_FreeSlots`, `_OneFreeSlot`, `_NoFreeSlots` | Three keys | One plural-aware string | Translatable |
| `Preset_Full`, `Equaliser_SlotsFull` | "All 5 slots…" | The count as a placeholder | No number written into a sentence |
| `Preset_NotRead`, `Preset_Unreadable`, `Equaliser_Unreadable` | Bare failures | Couldn't read the presets. Try again. | A next step |
| `Preset_TooLong`, `Equaliser_NameAtLimit` | That's as long as a name can be. | Names can be {0} characters at most. | Plainer |
| `Equaliser_BackTo`, `Equaliser_PutBack` | Two keys, same words | One key | One name for one thing |
| `Tone_HowTo` | 25 words | Slide through the tones. Cut where one jumps out, boost where one fades. | One short how-to |
| `Parametric_Octaves` | {0} oct | {0} octaves | Words, not abbreviations |

### Shared states

| Key | Now | Proposed | Why |
| --- | --- | --- | --- |
| `Headset_LostContact` | Lost contact with the headset. | Lost contact with the headset. Switch it off and on again. | A next step |
| `Access_Failed` | That did not work | That didn't work. Try again. | Contraction, and a next step |
| `Routing_OpenSoundSettings`, `Spatial_*` | Shown on every system | Windows only | Never name what isn't there |

## Built in code, to fix with the page work

- **Joined pieces.** `Equaliser_SlotsFree` plus `Equaliser_NameAtLimit`,
  `Mix_ChatFromName`, `Parametric_Chip` and `Equaliser_Band` are put
  together in code.
- **Fixed sentences.** Each becomes one whole sentence with named
  placeholders.

## Words the checks look for

These change with the proposals above, so their checks change in the same
commit:

- **Audio:** `Noise cancellation`, `Blocking`, `Max loudness boost (Stealth
  Pro II only)`, `Noise control`, `Mic monitoring` and `Open the
  equaliser`.
- **Page names:** `Audio` and `Microphone` in `pretend.ps1`, `access.ps1`,
  `profiles.ps1` and `atlas.ps1`.
- **Controls page:** `Noise cancellation, transparency and off` and `Noise
  cancellation on/off`, which follow the noise names.
