using System.Collections.Generic;
using UnityEngine;

namespace SpeechPipeline
{
    // Formats all console log output for the speech pipeline.
    // All methods are static — called from SpeechPipelineController.
    public static class ConsoleDisplay
    {
        public static void LoadingModel()
        {
            Debug.Log("[SpeechPipeline] Loading model in background...");
        }

        public static void ModelReady()
        {
            Debug.Log(
                "[SpeechPipeline] STT model ready. Full pipeline active.\n" +
                "Press SPACE to start recording once ready.\n\n" +
                "─────── Press SPACE ───────");
        }

        public static void RecordingStarted()
        {
            Debug.Log("\n● RECORDING  —  speak freely. Press SPACE to stop.\n");
        }

        // Printed once per TickInterval while the speaker is voiced.
        public static void LiveTick(float speakingSec, float pitchHz, float rms, string partial)
        {
            string pitchStr = pitchHz > 0f ? $"{pitchHz:F0} Hz" : "–";
            string tail     = !string.IsNullOrWhiteSpace(partial) ? $"  \"{partial}\"" : "";
            Debug.Log($"  ● {speakingSec:F1}s  |  Pitch {pitchStr}  |  Vol {rms:F3}{tail}");
        }

        public static void PartialTranscript(string text)
        {
            Debug.Log($"  [Transcript...] \"{text}\"");
        }

        public static void Utterance(UtteranceMetrics m)
        {
            var (toneStars, toneLabel, toneNote) = SpeechScorer.ScoreTone(m);
            var (quality, qualLabel, tips)        = SpeechScorer.ScoreQuality(m);

            string wpmLabel   = SpeechScorer.WPMLabel(m.WPM);
            string fillerLine = m.FillerCount > 0
                ? $"{m.FillerCount}× — {string.Join(", ", m.FillerWords)}"
                : "none ✓";
            string tipsLine   = tips.Count > 0
                ? $"\n│  Tips: {string.Join(" | ", tips)}"
                : "";

            Debug.Log(
                $"\n┌─── Utterance ───────────────────────────────\n" +
                $"│ \"{m.Text}\"\n" +
                $"├─ Pace     : {m.WPM:F0} WPM ({wpmLabel})  |  {m.WordCount} words  |  {m.DurationSec:F1}s\n" +
                $"├─ Tone     : {SpeechScorer.Stars(toneStars)} {toneLabel}{toneNote}\n" +
                $"│             avg {m.AvgPitchHz:F0} Hz  |  variation σ {m.PitchStdDevHz:F0} Hz\n" +
                $"├─ Pauses   : {m.PauseCount}x  |  last pause {m.LastPauseSec:F1}s\n" +
                $"├─ Fillers  : {fillerLine}\n" +
                $"└─ Quality  : {quality:F1}/10  {qualLabel}{tipsLine}\n\n" +
                $"─────── Press SPACE again ───────");
        }

        public static void SessionSummary(
            float        totalSec,
            float        speakingSec,
            int          pauseCount,
            float        pauseTotalSec,
            int          wordCount,
            float        avgWpm,
            float        avgPitchStd,
            int          fillerCount,
            List<string> fillerWords,
            List<string> transcript)
        {
            var (toneStars, toneLabel) = SpeechScorer.ScoreToneSession(avgPitchStd);

            string fillerSummary = fillerCount > 0
                ? $"{fillerCount}× ({string.Join(", ", fillerWords)})"
                : "none ✓";
            float fillerRatio = wordCount > 0 ? (float)fillerCount / wordCount : 0f;

            string fullTranscript = transcript.Count > 0
                ? string.Join(" ", transcript)
                : "(no speech detected)";

            Debug.Log(
                $"\n╔══ SESSION SUMMARY ══════════════════════════╗\n" +
                $"║  Total time     : {totalSec:F0}s\n" +
                $"║  Speaking time  : {speakingSec:F1}s\n" +
                $"║  Pauses         : {pauseCount}x  ({pauseTotalSec:F1}s total)\n" +
                $"║  Total words    : {wordCount}\n" +
                $"║  Avg pace       : {avgWpm:F0} WPM\n" +
                $"║  Tone           : {SpeechScorer.Stars(toneStars)} {toneLabel}  (σ avg {avgPitchStd:F0} Hz)\n" +
                $"║  Filler words   : {fillerSummary}\n" +
                $"║  Filler ratio   : {fillerRatio * 100f:F1}%\n" +
                $"╠══ FULL TRANSCRIPT ══════════════════════════╣\n" +
                $"║  {fullTranscript}\n" +
                $"╚═════════════════════════════════════════════╝\n" +
                $"Press SPACE to start a new session.");
        }
    }
}
