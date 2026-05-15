using UnityEngine;

namespace SpeechPipeline
{
    // Measures Words Per Minute for a single utterance.
    // Call StartUtterance() when the speaker first produces voiced audio,
    // then StopUtterance(wordCount) when the STT final result arrives.
    public sealed class PaceTracker
    {
        public const float WPM_Slow   = 100f;
        public const float WPM_Normal = 150f;
        public const float WPM_Fast   = 200f;

        private float _start;
        private bool  _running;

        public void StartUtterance()
        {
            _start   = Time.realtimeSinceStartup;
            _running = true;
        }

        // Call when the session is externally paused mid-utterance to avoid an orphaned timer.
        public void CancelUtterance()
        {
            _running = false;
        }

        // Returns (wpm, durationSeconds). Both 0 if not started or word count is 0.
        public (float wpm, float sec) StopUtterance(int wordCount)
        {
            if (!_running) return (0f, 0f);
            _running = false;

            float dur = Time.realtimeSinceStartup - _start;
            if (dur <= 0f || wordCount <= 0) return (0f, dur);
            return (wordCount / dur * 60f, dur);
        }
    }
}
