# Copy principles

How Neap writes the words people read and hear: labels, states, notices,
buttons and the names screen readers announce. The look is in the
[design principles](PRINCIPLES.md); this is the voice.

## Who is reading

- **Someone mid-game.** They glance at Neap between rounds and want the answer
  in a word: connected, 76%, toward chat. Every word they have to read is a
  moment away from the game.
- **Someone who has never had Swarm II.** On Linux, Neap's own words do all
  the explaining. A term only Turtle Beach's app uses means nothing to them.
- **Someone listening rather than looking.** A screen reader reads Neap's
  names aloud, in order, without the picture around them.

## The voice

Plain, calm and on your side. Neap sounds like a friend who knows headsets:
it says what is happening and what to do, then gets out of the way. It is
never cute, never alarmed and never blames anyone.

---

## 1. Name things by what they do for you

- **Say the effect, not the mechanism.** "Strength", not "Blocking". "How
  loud you have to be to be heard", not "Before the gate opens".
- **A Turtle Beach term stays only where the headset uses it,** as with
  Superhuman Hearing, CrossPlay and Signature Sound. Explain it once, where
  it is first met, in a few words: "the CrossPlay button on the headset".
- **No jargon you can avoid.** An expert word stays only when the plain one
  would mislead: frequency, gain and octaves on the equaliser.

## 2. One name for one thing, everywhere

- **A feature has one name** on its page, its tile, the Controls page, the
  notices, its screen-reader name and the screen checks. The glossary below
  is the list.
- **Two features never sound alike.** The headset's noise cancellation and
  the microphone's noise reduction share a page, so their names and
  descriptions keep them apart: one is about what you hear, the other about
  what others hear.

## 3. Short enough to read at a glance

- **Labels are one to three words.** A segmented control's options are one
  word each: Off, Cancelling, Transparency.
- **A description is one line,** about 60 characters at most, and only when
  it is needed (see 4).
- **Lead with the word that matters.** "Battery 76%", "Toward chat".
- **The model or the reason goes in the state, not the label.** Write
  "Pre-gain", and show "Stealth Pro II only" only where it isn't
  available.

## 4. Help text earns its place

- **No description that repeats the label.** "Voice prompts: how loud the
  headset's spoken alerts are" says nothing new, so it goes.
- **A description stays when the name alone can't be understood,** or when
  it warns of something that can hurt: "Turn it up slowly to protect your
  hearing".
- **A how-to is one sentence,** and is shown only where someone is about to
  do the thing.

## 5. Say what's happening, then the way out

- **A state is one sentence, then the next step if there is one.** "Lost
  contact with the headset. Switch it off and on again."
- **Never a bare failure.** "Couldn't read the presets. Try again" in place
  of "The presets haven't been read yet."
- **No blame, no drama.** Never "Error", "Failed" or "!", and never
  "please". The headset isn't wrong and neither is the person.
- **Progress uses an ellipsis,** and only progress does: "Reading the
  presets…".

## 6. Warm, not cute

- **Contractions, as people speak:** "can't", "isn't", "you'll".
- **Personality only where it helps you feel the change:** the dial's
  Balanced, Toward chat and Game only, and the meter that moves as you
  speak. A notice or a state is never playful.
- **Speak to the person as "you" and about the headset as "the headset."**
  Neap never says "I".

## 7. Write for translation and screen readers

- **Every word lives in the resource file,** including preset names, band
  labels and the names of the equaliser's two sides.
- **Whole sentences, with named placeholders,** never pieces joined in code
  and never a separator such as "·" that carries meaning.
- **One string per plural,** never three keys for "1 slot", "2 slots" and
  "no slots". No number such as "5" written into a sentence; it is a
  placeholder.
- **A screen-reader name says what the eye gets from the layout:** "Game
  equaliser preset", not just "Equaliser preset".

## 8. Honest to the system

- **Never name what isn't there.** Windows Sonic, Dolby Access and Windows'
  sound settings appear only on Windows. Linux gets its own words or none.
- **Say "your system"** where a step differs by system, and name the real
  place only when the app knows which system it is on.

## 9. Mechanics

- **British English,** as the app already is: equaliser, colour, centre.
- **Sentence case everywhere.** Proper names keep their own capitals:
  Superhuman Hearing, Charging Dock, CrossPlay.
- **No full stop on labels, headings or buttons.** Descriptions, states and
  notices are sentences and end with one.
- **Buttons start with a verb,** one to three words: Save as new, Open the
  equaliser, Refresh.
- **Write "and", not "&",** in Neap's own words. A factory preset keeps the
  name the headset shows.
- **Units:** 76% with no space; 6 dB, 1 kHz and 2.0 octaves with one. A sign
  goes on every gain: +3.0 dB, −2.0 dB, 0.0 dB.

---

## Patterns

| Where | Write | Example |
| --- | --- | --- |
| Page title | One word, the place's name | Audio |
| Section heading | What the section is about, from your side | What you hear |
| Setting name | The thing, one to three words | Master volume |
| Description | Only if needed; one line; a sentence | Hear your own voice in the headset. |
| Switch | Its state in a word beside it | On, Off |
| Segmented options | One word each | Off · Cancelling · Transparency |
| Button | A verb first | Save as new |
| State | What is happening, then what to do | Lost contact with the headset. Switch it off and on again. |
| Empty state | What will be here, then how to start | No profiles yet. Set the headset up the way you want, then choose New profile. |
| Confirmation | The consequence, then two plain choices | Delete Night Raid? You can't get it back. Delete / Keep |

## Glossary

| Use | Not | Means |
| --- | --- | --- |
| Noise cancellation | Noise control, ANC | The headset blocking or letting in the room |
| Cancelling, Transparency, Off | Noise cancellation (as a mode) | Its three modes |
| Strength | Blocking | How much noise cancellation keeps out |
| Noise reduction | AI noise reduction | The microphone removing background noise |
| Noise gate | Gate | The microphone staying silent until you speak |
| Game equaliser, Microphone equaliser | EQ, Equaliser (alone, where both show) | The two equalisers |
| Chat | Chat audio | The chat side of the mix |
| Chat apps | Chat sources | The apps that count as chat |
| Master volume | Volume | The headset's own volume |
| Microphone | Mic | Everywhere in the interface, including Microphone monitoring |
| Pre-gain | Loudness boost, Max loudness boost | The level the Stealth Pro II applies before its equaliser |
| Preset | Profile (for the equaliser) | A saved equaliser curve |
| Profile | Preset (for the headset) | A saved set of all the headset's settings |
