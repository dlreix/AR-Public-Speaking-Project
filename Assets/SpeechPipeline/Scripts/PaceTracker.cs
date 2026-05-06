using UnityEngine;

namespace SpeechPipeline
{
    /// <summary>
    /// Measures Words Per Minute for a single utterance.
    /// Call StartUtterance() when the speaker first produces voiced audio.
    /// Call StopUtterance(wordCount) when the STT final result arrives.
    /// </summary>
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

        /// <summary>Cancels a running utterance without producing a result. Call when the session is externally paused mid-utterance.</summary>
        public void CancelUtterance()
        {
            _running = false;
        }

        /// <summary>Returns (wpm, durationSeconds). Both 0 if not started or word count is 0.</summary>
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
