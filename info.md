# SpeechPipeline — Project Info

Unity offline speech analysis pipeline. Runs entirely on PC, no cloud or API.
All output is currently printed to the Unity Console via `Debug.Log`.

---

## What the pipeline does

When the user presses **SPACE** to stop a recording session, two types of
structured data become available:

1. **Per-utterance metrics** — fired after each sentence (when Vosk detects a pause)
2. **Session summary metrics** — fired once when the session ends

---

## Key files

| File | Purpose |
|---|---|
| `Assets/SpeechPipeline/Scripts/SpeechData.cs` | Data models — `UtteranceMetrics` and `LiveState` |
| `Assets/SpeechPipeline/Scripts/ConsoleDisplay.cs` | All formatted output — every `Debug.Log` lives here |
| `Assets/SpeechPipeline/Scripts/SpeechPipelineController.cs` | Main MonoBehaviour — orchestrates everything, calls ConsoleDisplay |
| `Assets/SpeechPipeline/Scripts/SpeechScorer.cs` | Scoring logic — tone (1–5 stars) and quality (0–10) |

---

## Data structures

### UtteranceMetrics (`SpeechData.cs`)
Produced after every sentence. Fields:

```csharp
string       Text           // full transcribed sentence
float        WPM            // words per minute
int          WordCount
float        DurationSec    // speaking duration of this utterance
float        AvgPitchHz     // average fundamental frequency
float        PitchStdDevHz  // pitch variation (higher = more expressive)
int          PauseCount     // pauses detected during this utterance
float        LastPauseSec   // duration of the most recent pause
List<string> FillerWords    // list of detected filler words (with duplicates)
int          FillerCount
float        AvgRMS         // average volume (0.0 – 1.0 range)
float        PeakRMS        // peak volume
```

### Session-level values (computed in `EndSession()`, `SpeechPipelineController.cs`)
Passed directly to `ConsoleDisplay.SessionSummary()`:

```csharp
float        totalSec           // total session wall-clock time
float        speakingSec        // total time RMS was above noise floor
int          pauseCount         // total pauses across all utterances
float        pauseTotalSec      // cumulative pause duration
int          wordCount          // total words across all utterances
float        avgWpm             // average WPM across utterances
float        avgPitchStd        // average pitch std deviation (tone variety)
int          fillerCount        // total filler words
List<string> fillerWords        // all filler words found (with duplicates)
List<string> transcript         // each utterance as a separate string entry
```

---

## Tone rating scale (`SpeechScorer.cs`)

Based on pitch standard deviation (`PitchStdDevHz`):

| σ (Hz) | Stars | Label |
|---|---|---|
| < 10 | ★☆☆☆☆ | Very Monotone |
| 10 – 20 | ★★☆☆☆ | Flat |
| 20 – 35 | ★★★☆☆ | Natural |
| 35 – 55 | ★★★★☆ | Expressive |
| > 55 | ★★★★★ | Very Expressive |

Volume notes added when `AvgRMS < 0.008` (too quiet) or `AvgRMS > 0.10` (too loud).

---

## Quality score (`SpeechScorer.cs`)

Score 0–10, starts at 5.0, adjusted by:

| Factor | Effect |
|---|---|
| Word count < 3 | −2 |
| Word count 3–7 | −1 |
| Word count > 15 | +1 |
| Word count > 30 | +2 |
| Vocab diversity > 85% unique | +2 |
| Vocab diversity > 65% unique | +1 |
| Vocab diversity < 40% unique | −1 |
| No filler words | +1 |
| Filler ratio 5–12% | −1 |
| Filler ratio > 12% | −2 |
| WPM 120–165 (ideal range) | +1 |

Labels: `Poor` / `Needs Work` / `Average` / `Good` / `Excellent`

---

## Filler words detected (`FillerDetector.cs`)

```
um, uh, er, hm, hmm, hmmm,
like, you know, you see, i mean,
so, basically, literally, actually,
honestly, right, kind of, kinda,
sort of, sorta, at the end of the day, to be honest
```

---

## Best integration points

If you want to consume this data in another system **without parsing log text**,
the two cleanest hooks are inside `SpeechPipelineController.cs`:

### Per-utterance hook — `FinaliseUtterance(string text)`
Called every time Vosk finalises a sentence. At this point the fully populated
`UtteranceMetrics` object (`m`) is available before it goes to `ConsoleDisplay`.

```csharp
// Add your call here, just before or after:
ConsoleDisplay.Utterance(m);
```

### Session-end hook — `EndSession()`
Called when the user presses SPACE to stop. All session-level variables are
available at this point:

```csharp
_sessionSpeaking, _sessionPauses, _sessionPauseTotal,
_sessionWords, _sessionFillers, _sessionFillerList,
_sessionTranscript, avgWpm, avgPitchStd, totalSec
```

---

## STT model

**Model:** `vosk-model-en-us-0.42-gigaspeech` (3.8 GB — not in git)
**Download:** https://alphacephei.com/vosk/models
**Place at:** `Assets/StreamingAssets/vosk-model-en-us-0.42-gigaspeech/`

Plugin binaries (already in repo):
- `Assets/Plugins/Vosk/Vosk.dll` — managed C# wrapper
- `Assets/Plugins/Vosk/libvosk.dylib` — native macOS library

---

## GitHub

Repository: https://github.com/dlreix/AR-Public-Speaking-Project
Branch: `speech-pipeline`
