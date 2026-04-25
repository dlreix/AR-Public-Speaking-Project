using System.Collections.Generic;
using UnityEngine;

namespace SpeechPipeline
{
    /// <summary>
    /// Heuristic scoring for a single utterance and overall session tone.
    /// All computation is offline — no network required.
    /// </summary>
    public static class SpeechScorer
    {
        // ── Tone (per utterance, 1–5 stars) ─────────────────────────────────────
        // Driven by pitch variation (σ) and volume appropriateness.

        public static (int stars, string label, string note) ScoreTone(UtteranceMetrics m)
        {
            if (m.AvgPitchHz <= 0f) return (0, "No pitch data", "");

            int    stars;
            string label;
            if      (m.PitchStdDevHz < 10f) { stars = 1; label = "Very Monotone";    }
            else if (m.PitchStdDevHz < 20f) { stars = 2; label = "Flat";             }
            else if (m.PitchStdDevHz < 35f) { stars = 3; label = "Natural";          }
            else if (m.PitchStdDevHz < 55f) { stars = 4; label = "Expressive";       }
            else                            { stars = 5; label = "Very Expressive";   }

            string note = "";
            if      (m.AvgRMS < 0.008f)     note = " (too quiet — speak up)";
            else if (m.AvgRMS > 0.10f)      note = " (too loud — ease back)";
            else if (m.PitchStdDevHz < 15f) note = " (try raising/lowering pitch more)";

            return (stars, label, note);
        }

        // ── Tone (per session, stars only) ──────────────────────────────────────

        public static (int stars, string label) ScoreToneSession(float avgStdDev)
        {
            if      (avgStdDev <= 0f)  return (0, "No data");
            if      (avgStdDev < 10f)  return (1, "Very Monotone");
            if      (avgStdDev < 20f)  return (2, "Flat");
            if      (avgStdDev < 35f)  return (3, "Natural");
            if      (avgStdDev < 55f)  return (4, "Expressive");
            return (5, "Very Expressive");
        }

        // ── Quality score (0–10) ─────────────────────────────────────────────────
        // Proxy metrics: word count, vocabulary diversity, filler ratio, pace.

        public static (float score, string label, List<string> tips) ScoreQuality(UtteranceMetrics m)
        {
            var   tips  = new List<string>();
            float score = 5f;

            // Word count
            if      (m.WordCount < 3)  { score -= 2f; tips.Add("Speak more — too few words."); }
            else if (m.WordCount < 8)    score -= 1f;
            else if (m.WordCount > 30)   score += 2f;
            else if (m.WordCount > 15)   score += 1f;

            // Vocabulary diversity (unique / total)
            float div = VocabDiversity(m.Text);
            if      (div > 0.85f) score += 2f;
            else if (div > 0.65f) score += 1f;
            else if (div < 0.40f) { score -= 1f; tips.Add("Try varying your word choice."); }

            // Filler ratio
            float fr = m.WordCount > 0 ? (float)m.FillerCount / m.WordCount : 0f;
            if      (fr == 0f)   score += 1f;
            else if (fr < 0.05f) { /* acceptable */ }
            else if (fr < 0.12f) { score -= 1f; tips.Add("Reduce filler words."); }
            else                 { score -= 2f; tips.Add("Too many fillers — pause instead of filling silence."); }

            // Pace
            if (m.WPM > 0f)
            {
                if      (m.WPM >= 120f && m.WPM <= 165f) score += 1f;
                else if (m.WPM < 80f)                    tips.Add("Pace is too slow.");
                else if (m.WPM > 200f)                   tips.Add("Pace is too fast — slow down.");
            }

            score = Mathf.Clamp(Mathf.Round(score * 10f) / 10f, 0f, 10f);

            string label = score >= 9f ? "Excellent"    :
                           score >= 7f ? "Good"         :
                           score >= 5f ? "Average"      :
                           score >= 3f ? "Needs Work"   : "Poor";

            return (score, label, tips);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        public static string Stars(int n, int max = 5) =>
            new string('★', Mathf.Clamp(n, 0, max)) +
            new string('☆', Mathf.Clamp(max - n, 0, max));

        public static string WPMLabel(float wpm)
        {
            if (wpm <= 0f)                        return "–";
            if (wpm < PaceTracker.WPM_Slow)       return "Too slow";
            if (wpm < PaceTracker.WPM_Normal)     return "Slow";
            if (wpm < PaceTracker.WPM_Fast)       return "Good";
            return "Fast";
        }

        private static float VocabDiversity(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0f;
            var words = text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\n', ',', '.', '!', '?' },
                       System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0f;
            var unique = new HashSet<string>(words);
            return (float)unique.Count / words.Length;
        }
    }
}
